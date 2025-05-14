using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Elfenlabs.String
{
    [Flags]
    public enum MarkupRuleFlag
    {
        None = 0,
        AllowElementValue = 1 << 0,        // Allows <tag=value> syntax
        AllowValuelessAttribute = 1 << 1,  // Allows <tag attribute> syntax
        AllowEmptyTag = 1 << 2,           // Allows <tag/> syntax
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Range : IEquatable<Range>
    {
        public int Start;
        public int Length;
        public bool IsEmpty => Length <= 0; // Changed to <= 0 for invalid/empty

        public Range(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public bool Equals(Range other) => Start == other.Start && Length == other.Length;
        public override int GetHashCode() => Start.GetHashCode() ^ (Length.GetHashCode() << 2);
        public override string ToString() => IsEmpty ? "Empty" : $"[{Start}..{Start + Length - 1}] ({Length})";

        public static readonly Range Empty = new Range(-1, 0); // Represents an invalid or non-existent range
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct Element : IEquatable<Element>
    {
        public Range Content;
        public Range TagName;
        public Range FullOpeningTag;

        public bool Equals(Element other) => Content.Equals(other.Content) && TagName.Equals(other.TagName) && FullOpeningTag.Equals(other.FullOpeningTag);
        public override int GetHashCode() => Content.GetHashCode() ^ (TagName.GetHashCode() << 2) ^ (FullOpeningTag.GetHashCode() << 4);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ElementAttribute : IEquatable<ElementAttribute>
    {
        public int ElementIndex;
        public Range Key;
        public Range Value;

        public bool Equals(ElementAttribute other) => ElementIndex == other.ElementIndex && Key.Equals(other.Key) && Value.Equals(other.Value);
        public override int GetHashCode() => ElementIndex.GetHashCode() ^ (Key.GetHashCode() << 2) ^ (Value.GetHashCode() << 4);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OpenElementInfo : IEquatable<OpenElementInfo>
    {
        public int ElementListIndex;
        public Range TagName;
        public int ContentStartIndex;

        public bool Equals(OpenElementInfo other) =>
            ElementListIndex == other.ElementListIndex &&
            TagName.Equals(other.TagName) &&
            ContentStartIndex == other.ContentStartIndex;

        public override int GetHashCode() => ElementListIndex.GetHashCode() ^ (TagName.GetHashCode() << 2) ^ (ContentStartIndex.GetHashCode() << 4);
    }

    public static unsafe class MarkupParser
    {
        public static void ParseMarkup(
            string str,
            Allocator allocator,
            out NativeList<Element> elements,
            out NativeList<ElementAttribute> attributes,
            MarkupRuleFlag rules = MarkupRuleFlag.AllowValuelessAttribute)
        {
            if (string.IsNullOrEmpty(str))
            {
                elements = new NativeList<Element>(0, allocator);
                attributes = new NativeList<ElementAttribute>(0, allocator);
                return;
            }

            fixed (char* charPtr = str)
            {
                ParseMarkup(charPtr, str.Length, allocator, out elements, out attributes, rules);
            }
        }

        [BurstCompile]
        public static void ParseMarkup(
            char* charPtr,
            int charLength,
            Allocator allocator,
            out NativeList<Element> elements,
            out NativeList<ElementAttribute> attributes,
            MarkupRuleFlag rules = MarkupRuleFlag.AllowValuelessAttribute)
        {
            elements = new NativeList<Element>(16, allocator);
            attributes = new NativeList<ElementAttribute>(32, allocator);

            if (charPtr == null || charLength == 0)
            {
                return;
            }

            // Stack to keep track of open elements for nesting
            var openElementStack = new NativeList<OpenElementInfo>(8, Allocator.Temp);

            try
            {
                int i = 0;
                // int currentTextContentStart = 0; // Not needed if ContentStartIndex is on stack

                while (i < charLength)
                {
                    if (charPtr[i] == '<')
                    {
                        int tagStartIndex = i;
                        i++; // Move past '<'

                        if (i >= charLength) break;

                        if (charPtr[i] == '/') // Closing Tag
                        {
                            i++; // Move past '/'
                            int closingTagNameStart = i;
                            while (i < charLength && charPtr[i] != '>') i++;

                            if (i >= charLength) break; // Unterminated closing tag

                            Range closingTagName = new Range(closingTagNameStart, i - closingTagNameStart);
                            i++; // Move past '>'

                            if (openElementStack.Length > 0)
                            {
                                OpenElementInfo lastOpenElement = openElementStack[openElementStack.Length - 1];
                                var namesCmp = CompareRanges(charPtr, lastOpenElement.TagName, charPtr, closingTagName);

                                if (namesCmp == 0)
                                {
                                    Element elementToUpdate = elements[lastOpenElement.ElementListIndex];
                                    elementToUpdate.Content = new Range(lastOpenElement.ContentStartIndex, tagStartIndex - lastOpenElement.ContentStartIndex);
                                    elements[lastOpenElement.ElementListIndex] = elementToUpdate;
                                    openElementStack.RemoveAt(openElementStack.Length - 1);
                                }
                                // else { // Mismatched closing tag - ignore or log }
                            }
                            // currentTextContentStart = i; // Not needed
                            continue;
                        }

                        // Opening Tag
                        int openingTagNameStart = i;
                        while (i < charLength && !IsWhiteSpaceOrTagChar(charPtr[i]) && charPtr[i] != '=') i++;


                        if (i >= charLength) break;

                        Range tagName = new Range(openingTagNameStart, i - openingTagNameStart);
                        if (tagName.IsEmpty)
                        { // e.g. << or <>
                          // Skip malformed tag start
                            while (i < charLength && charPtr[i] != '>') i++;
                            if (i < charLength) i++; // move past >
                            continue;
                        }

                        int currentElementIndex = elements.Length;
                        Element newElement = new Element
                        {
                            TagName = tagName,
                            FullOpeningTag = new Range(tagStartIndex, 0), // Updated later
                            Content = Range.Empty
                        };
                        elements.Add(newElement);

                        // Parse Attributes / Element Value
                        while (i < charLength && charPtr[i] != '>' && !(charPtr[i] == '/' && i + 1 < charLength && charPtr[i + 1] == '>'))
                        {
                            while (i < charLength && char.IsWhiteSpace(charPtr[i])) i++;
                            if (i >= charLength || charPtr[i] == '>' || (charPtr[i] == '/' && i + 1 < charLength && charPtr[i + 1] == '>')) break;

                            if (rules.HasFlag(MarkupRuleFlag.AllowElementValue) && charPtr[i] == '=' && elements[currentElementIndex].TagName.Start == openingTagNameStart)
                            {
                                i++; // Move past '='
                                int elementValueStart = i;
                                char quoteChar = '\0';
                                if (i < charLength && (charPtr[i] == '"' || charPtr[i] == '\''))
                                {
                                    quoteChar = charPtr[i]; i++; elementValueStart = i;
                                }
                                while (i < charLength)
                                {
                                    if (quoteChar != '\0' && charPtr[i] == quoteChar) break;
                                    if (quoteChar == '\0' && (IsWhiteSpaceOrTagChar(charPtr[i]))) break;
                                    i++;
                                }
                                Range elementValue = new Range(elementValueStart, i - elementValueStart);
                                attributes.Add(new ElementAttribute
                                {
                                    ElementIndex = currentElementIndex,
                                    Key = elements[currentElementIndex].TagName, // Tag name as key
                                    Value = elementValue
                                });
                                if (quoteChar != '\0' && i < charLength && charPtr[i] == quoteChar) i++;
                                continue;
                            }

                            int attrNameStart = i;
                            while (i < charLength && !IsWhiteSpaceOrTagChar(charPtr[i]) && charPtr[i] != '=') i++;
                            Range attrName = new Range(attrNameStart, i - attrNameStart);

                            if (attrName.IsEmpty)
                            {
                                if (i < charLength && !IsWhiteSpaceOrTagChar(charPtr[i])) i++; continue;
                            }

                            while (i < charLength && char.IsWhiteSpace(charPtr[i])) i++;

                            Range attrValue = Range.Empty;
                            if (i < charLength && charPtr[i] == '=')
                            {
                                i++; while (i < charLength && char.IsWhiteSpace(charPtr[i])) i++;
                                if (i >= charLength) break;
                                int attrValueStart = i; char quoteChar = '\0';
                                if (charPtr[i] == '"' || charPtr[i] == '\'') { quoteChar = charPtr[i]; i++; attrValueStart = i; }
                                while (i < charLength)
                                {
                                    if (quoteChar != '\0' && charPtr[i] == quoteChar) break;
                                    if (quoteChar == '\0' && (IsWhiteSpaceOrTagChar(charPtr[i]))) break;
                                    i++;
                                }
                                attrValue = new Range(attrValueStart, i - attrValueStart);
                                if (quoteChar != '\0' && i < charLength && charPtr[i] == quoteChar) i++;
                            }
                            else if (!rules.HasFlag(MarkupRuleFlag.AllowValuelessAttribute))
                            {
                                // throw new ArgumentException($"Valueless attribute '{attrName}' not allowed in this context.");
                            }

                            if (!attrName.IsEmpty && (!attrValue.IsEmpty || rules.HasFlag(MarkupRuleFlag.AllowValuelessAttribute)))
                            {
                                attributes.Add(new ElementAttribute { ElementIndex = currentElementIndex, Key = attrName, Value = attrValue });
                            }
                        }

                        bool selfClosing = false;
                        if (i < charLength && charPtr[i] == '/') { i++; if (i < charLength && charPtr[i] == '>') selfClosing = true; }
                        if (i < charLength && charPtr[i] == '>')
                        {
                            i++;
                            Element currentElem = elements[currentElementIndex]; // Get again as it's a struct
                            currentElem.FullOpeningTag = new Range(tagStartIndex, i - tagStartIndex);
                            if (selfClosing)
                            {
                                currentElem.Content = Range.Empty;
                            }
                            else
                            {
                                // Content starts after '>', length TBD by closing tag or EOF
                                currentElem.Content = new Range(i, 0);
                                openElementStack.Add(new OpenElementInfo { ElementListIndex = currentElementIndex, TagName = tagName, ContentStartIndex = i });
                            }
                            elements[currentElementIndex] = currentElem; // Write back
                                                                         // currentTextContentStart = i; // Not needed
                        }
                        else { break; } // Unterminated opening tag
                    }
                    else
                    {
                        // Regular character
                        i++;
                    }
                }

                // Handle unclosed tags: content runs to end of string
                for (int k = 0; k < openElementStack.Length; ++k) // Iterate through any remaining open tags
                {
                    OpenElementInfo openElement = openElementStack[k];
                    // Check if content was already set by a nested closing tag
                    // This check is a bit tricky. A simpler approach is to always set it here
                    // if the stack wasn't empty, assuming the outermost unclosed tag's content goes to EOF.
                    // For this example, we'll assume the last one on stack gets content to EOF if not closed.
                    if (k == openElementStack.Length - 1)
                    { // Only for the very last open element
                        Element elementToUpdate = elements[openElement.ElementListIndex];
                        // If content wasn't set by a proper closing tag, set it to run to the end
                        if (elementToUpdate.Content.Start == openElement.ContentStartIndex && elementToUpdate.Content.Length == 0)
                        {
                            if (openElement.ContentStartIndex < charLength)
                            {
                                elementToUpdate.Content = new Range(openElement.ContentStartIndex, charLength - openElement.ContentStartIndex);
                                elements[openElement.ElementListIndex] = elementToUpdate;
                            }
                        }
                    }
                }
            }
            finally
            {
                // Dispose the temporary stack
                if (openElementStack.IsCreated)
                {
                    openElementStack.Dispose();
                }
            }
        }

        // Helper to compare ranges within char pointers (case-sensitive)
        private static int CompareRanges(char* ptr1, Range range1, char* ptr2, Range range2)
        {
            if (range1.Length != range2.Length) return range2.Length - range1.Length;
            if (range1.IsEmpty) return 0;

            return UnsafeUtility.MemCmp(
                ptr1 + range1.Start,
                ptr2 + range2.Start,
                range1.Length * sizeof(char));
        }

        // Helper to identify characters that break attribute names/values or signify tag parts
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWhiteSpaceOrTagChar(char c)
        {
            return char.IsWhiteSpace(c) || c == '>' || c == '/';
        }
    }
}