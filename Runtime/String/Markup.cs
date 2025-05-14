using System;
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
        AllowElementValue = 1 << 0,
        AllowValuelessAttribute = 1 << 1,
        AllowEmptyTag = 1 << 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Range : IEquatable<Range>
    {
        public int Start;
        public int Length;
        public bool IsEmpty => Length <= 0;

        public Range(int start, int length) { Start = start; Length = length; }
        public bool Equals(Range other) => Start == other.Start && Length == other.Length;
        public override int GetHashCode() => Start.GetHashCode() ^ (Length.GetHashCode() << 2);
        public override string ToString() => IsEmpty ? "Empty" : $"[{Start}..{Start + Length - 1}] ({Length})";
        public static readonly Range Empty = new Range(-1, 0);
        internal unsafe string GetString(char* basePtr)
        {
            if (IsEmpty || basePtr == null) return string.Empty;
            // Basic bounds check for safety, though ideally ranges are always valid
            // if (Start < 0 || Length < 0 /*|| Start + Length > totalLengthOfOriginalString*/) return "[Invalid Range]";
            return new string(basePtr, Start, Length);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Element : IEquatable<Element>
    {
        public Range Content;
        public Range TagName;
        public Range FullOpeningTag;
        public Range Value;

        public bool Equals(Element other) => Content.Equals(other.Content) && TagName.Equals(other.TagName) && FullOpeningTag.Equals(other.FullOpeningTag) && Value.Equals(other.Value);
        public override int GetHashCode() => Content.GetHashCode() ^ (TagName.GetHashCode() << 2) ^ (FullOpeningTag.GetHashCode() << 4) ^ (Value.GetHashCode() << 6);
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

        public bool Equals(OpenElementInfo other) => ElementListIndex == other.ElementListIndex && TagName.Equals(other.TagName) && ContentStartIndex == other.ContentStartIndex;
        public override int GetHashCode() => ElementListIndex.GetHashCode() ^ (TagName.GetHashCode() << 2) ^ (ContentStartIndex.GetHashCode() << 4);
    }

    // --- Custom Burst-Friendly Exception Types ---
    public class MarkupParseException : ArgumentException
    {
        public Range ErrorRange { get; }
        public Range ContextRange { get; }
        public MarkupParseException(Range errorRange, Range contextRange = default) : base() { ErrorRange = errorRange; ContextRange = contextRange; }
        protected MarkupParseException(string message, Range errorRange, Range contextRange = default) : base(message) { ErrorRange = errorRange; ContextRange = contextRange; }
    }

    public class ValuelessAttributeNotAllowedException : MarkupParseException
    {
        public ValuelessAttributeNotAllowedException(Range attributeNameRange, Range tagNameRange) : base(attributeNameRange, tagNameRange) { }
    }
    public class ElementValueNotAllowedException : MarkupParseException
    {
        public ElementValueNotAllowedException(Range tagNameRange, Range elementValueOperatorRange) : base(elementValueOperatorRange, tagNameRange) { }
    }
    public class SelfClosingTagNotAllowedException : MarkupParseException
    {
        public SelfClosingTagNotAllowedException(Range selfClosingTagRange, Range tagNameRange) : base(selfClosingTagRange, tagNameRange) { }
    }
    public class EmptyTagNameException : MarkupParseException
    {
        public EmptyTagNameException(Range errorRange) : base(errorRange) { } // ErrorRange points to where tag name was expected
    }
    public class MismatchedClosingTagException : MarkupParseException
    {
        public Range ExpectedTagNameRange { get; }
        public Range ActualTagNameRange { get; }
        public MismatchedClosingTagException(Range actualTagNameRange, Range expectedTagNameRange) : base(actualTagNameRange, expectedTagNameRange) { ExpectedTagNameRange = expectedTagNameRange; ActualTagNameRange = actualTagNameRange; }
    }
    public class UnclosedTagException : MarkupParseException
    {
        public UnclosedTagException(Range tagNameRange, Range fullOpeningTagRange) : base(fullOpeningTagRange, tagNameRange) { }
    }
    public class UnterminatedTagException : MarkupParseException
    {
        public enum TagType { Opening, Closing, Attribute, ElementValue }
        public TagType TypeOfTag { get; }
        public UnterminatedTagException(Range errorRange, TagType type, Range contextTagRange = default) : base(errorRange, contextTagRange) { TypeOfTag = type; }
    }
    public class InvalidCharacterInTagException : MarkupParseException
    {
        public InvalidCharacterInTagException(Range errorRange, Range contextTagRange = default) : base(errorRange, contextTagRange) { }
    }
    public class MultipleElementValuesException : MarkupParseException
    {
        public MultipleElementValuesException(Range errorRange, Range tagNameRange) : base(errorRange, tagNameRange) { }
    }


    public static unsafe class MarkupParser
    {
        public static void ParseMarkup(
            string str,
            Allocator allocator,
            out NativeList<Element> elements,
            out NativeList<ElementAttribute> attributes,
            MarkupRuleFlag rules = MarkupRuleFlag.AllowValuelessAttribute | MarkupRuleFlag.AllowEmptyTag)
        {
            if (string.IsNullOrEmpty(str))
            {
                elements = new NativeList<Element>(0, allocator);
                attributes = new NativeList<ElementAttribute>(0, allocator);
                return;
            }

            fixed (char* charPtr = str)
            {
                try
                {
                    ParseMarkupInternal(charPtr, str.Length, allocator, out elements, out attributes, rules);
                }
                catch (ValuelessAttributeNotAllowedException e) { throw new ArgumentException($"Valueless attribute '{e.ErrorRange.GetString(charPtr)}' not allowed for tag '{e.ContextRange.GetString(charPtr)}' at index {e.ErrorRange.Start}. Rule 'AllowValuelessAttribute' is not set.", e); }
                catch (ElementValueNotAllowedException e) { throw new ArgumentException($"Element value syntax not allowed for tag '{e.ContextRange.GetString(charPtr)}' near '=' at index {e.ErrorRange.Start}. Rule 'AllowElementValue' is not set.", e); }
                catch (SelfClosingTagNotAllowedException e) { throw new ArgumentException($"Self-closing tag '{(e.ContextRange.GetString(charPtr))}' (full tag: '{e.ErrorRange.GetString(charPtr)}') not allowed at index {e.ErrorRange.Start}. Rule 'AllowEmptyTag' is not set.", e); }
                catch (EmptyTagNameException e) { throw new ArgumentException($"Empty tag name at index {e.ErrorRange.Start}. Found '{charPtr[e.ErrorRange.Start]}'.", e); } // Corrected message
                catch (MismatchedClosingTagException e)
                {
                    string expected = e.ExpectedTagNameRange.IsEmpty ? "[NO TAG OPEN]" : $"</{e.ExpectedTagNameRange.GetString(charPtr)}>";
                    throw new ArgumentException($"Mismatched closing tag at index {e.ActualTagNameRange.Start}. Expected '{expected}' but got '</{e.ActualTagNameRange.GetString(charPtr)}>'.", e);
                }
                catch (UnclosedTagException e) { throw new ArgumentException($"Unclosed tag '{e.ContextRange.GetString(charPtr)}' starting at index {e.ErrorRange.Start}.", e); }
                catch (UnterminatedTagException e)
                {
                    string tagTypeStr = e.TypeOfTag.ToString().ToLowerInvariant();
                    string contextStr = e.ContextRange.IsEmpty ? "" : $" for {(e.TypeOfTag == UnterminatedTagException.TagType.Attribute ? "attribute '" + e.ContextRange.GetString(charPtr) + "'" : "tag '" + e.ContextRange.GetString(charPtr) + "'")}";
                    throw new ArgumentException($"Unterminated {tagTypeStr}{contextStr} starting near index {e.ErrorRange.Start}.", e);
                }
                catch (InvalidCharacterInTagException e)
                {
                    string contextStr = e.ContextRange.IsEmpty ? "" : $" within tag '{e.ContextRange.GetString(charPtr)}'";
                    char invalidChar = (e.ErrorRange.Length > 0 && charPtr != null && e.ErrorRange.Start < str.Length) ? charPtr[e.ErrorRange.Start] : '?';
                    throw new ArgumentException($"Invalid character '{invalidChar}' found{contextStr} at index {e.ErrorRange.Start}.", e);
                }
                catch (MultipleElementValuesException e) { throw new ArgumentException($"Multiple element values ('=value') defined for tag '{e.ContextRange.GetString(charPtr)}' near index {e.ErrorRange.Start}.", e); }
                catch (MarkupParseException e) { throw new ArgumentException($"Markup parsing error near index {e.ErrorRange.Start} (length {e.ErrorRange.Length}). Context: '{e.ContextRange.GetString(charPtr)}'", e); }
                catch (ArgumentException) { throw; }
                catch (Exception e) { throw new ArgumentException($"An unexpected error occurred during markup parsing: {e.Message}", e); }
            }
        }

        [BurstCompile]
        public static void ParseMarkupInternal(
            char* charPtr, int charLength, Allocator allocator,
            out NativeList<Element> elements, out NativeList<ElementAttribute> attributes,
            MarkupRuleFlag rules = MarkupRuleFlag.AllowValuelessAttribute | MarkupRuleFlag.AllowEmptyTag)
        {
            elements = new NativeList<Element>(16, allocator);
            attributes = new NativeList<ElementAttribute>(32, allocator);

            if (charPtr == null || charLength == 0) return;

            var openElementStack = new NativeList<OpenElementInfo>(8, Allocator.Temp);
            try
            {
                int i = 0;
                while (i < charLength)
                {
                    if (charPtr[i] == '<')
                    {
                        int tagStartIndex = i;
                        i++;
                        if (i >= charLength) throw new UnterminatedTagException(new Range(tagStartIndex, 1), UnterminatedTagException.TagType.Opening);

                        if (charPtr[i] == '/') // Closing Tag
                        {
                            i++;
                            int closingTagNameStart = i;
                            while (i < charLength && charPtr[i] != '>') i++;
                            if (i >= charLength) throw new UnterminatedTagException(new Range(closingTagNameStart - 2, charLength - (closingTagNameStart - 2)), UnterminatedTagException.TagType.Closing);
                            Range closingTagName = new Range(closingTagNameStart, i - closingTagNameStart);
                            if (closingTagName.IsEmpty) throw new EmptyTagNameException(new Range(closingTagNameStart, 0));
                            i++;

                            if (openElementStack.Length > 0)
                            {
                                OpenElementInfo lastOpenElement = openElementStack[openElementStack.Length - 1];
                                if (CompareRanges(charPtr, lastOpenElement.TagName, charPtr, closingTagName) == 0)
                                {
                                    Element elementToUpdate = elements[lastOpenElement.ElementListIndex];
                                    elementToUpdate.Content = new Range(lastOpenElement.ContentStartIndex, tagStartIndex - lastOpenElement.ContentStartIndex);
                                    elements[lastOpenElement.ElementListIndex] = elementToUpdate;
                                    openElementStack.RemoveAt(openElementStack.Length - 1);
                                }
                                else throw new MismatchedClosingTagException(closingTagName, lastOpenElement.TagName);
                            }
                            else throw new MismatchedClosingTagException(closingTagName, Range.Empty); // No open tag to close
                            continue;
                        }

                        // Opening Tag
                        int openingTagNameStart = i;
                        // Tag name must start with a valid character
                        if (i >= charLength || !IsValidTagNameStartChar(charPtr[i]))
                            throw new EmptyTagNameException(new Range(i, (i < charLength && charPtr[i] == '>') ? 1 : 0)); // Catches <> and < >

                        while (i < charLength && IsValidTagNameChar(charPtr[i])) i++; // Consume valid tag name characters

                        // Check if anything was consumed for tag name after initial valid char
                        if (i == openingTagNameStart) // Only one char and it wasn't valid for continuation or was a special char
                            throw new InvalidCharacterInTagException(new Range(openingTagNameStart, 1), new Range(openingTagNameStart, 0));


                        Range tagName = new Range(openingTagNameStart, i - openingTagNameStart);
                        // Redundant check if IsValidTagNameStartChar and IsValidTagNameChar are good.
                        // if (tagName.IsEmpty) throw new EmptyTagNameException(new Range(openingTagNameStart, 0));

                        int currentElementIndex = elements.Length;
                        Element newElement = new Element { TagName = tagName, FullOpeningTag = Range.Empty, Content = Range.Empty, Value = Range.Empty };
                        elements.Add(newElement);

                        bool elementValueSet = false;
                        while (i < charLength && charPtr[i] != '>' && !(charPtr[i] == '/' && i + 1 < charLength && charPtr[i + 1] == '>'))
                        {
                            while (i < charLength && char.IsWhiteSpace(charPtr[i])) i++;
                            if (i >= charLength || charPtr[i] == '>' || (charPtr[i] == '/' && i + 1 < charLength && charPtr[i + 1] == '>')) break;

                            bool isPotentialElementValue = charPtr[i] == '=' && elements[currentElementIndex].TagName.Start == openingTagNameStart && AttributesLengthForElement(currentElementIndex, attributes) == 0;

                            if (isPotentialElementValue)
                            {
                                if (!rules.HasFlag(MarkupRuleFlag.AllowElementValue)) throw new ElementValueNotAllowedException(tagName, new Range(i, 1));
                                if (elementValueSet) throw new MultipleElementValuesException(new Range(i, 1), tagName);

                                i++; int elementValueStart = i; char quoteChar = '\0';
                                if (i < charLength && (charPtr[i] == '"' || charPtr[i] == '\'')) { quoteChar = charPtr[i]; i++; elementValueStart = i; }
                                while (i < charLength)
                                {
                                    if (quoteChar != '\0' && charPtr[i] == quoteChar) break;
                                    if (quoteChar == '\0' && (IsWhiteSpaceOrTagChar(charPtr[i]))) break;
                                    i++;
                                }
                                if (i >= charLength && quoteChar != '\0') throw new UnterminatedTagException(new Range(elementValueStart - 1, charLength - (elementValueStart - 1)), UnterminatedTagException.TagType.ElementValue, tagName);

                                Range elementValueRange = new Range(elementValueStart, i - elementValueStart);
                                Element tempElem = elements[currentElementIndex]; tempElem.Value = elementValueRange; elements[currentElementIndex] = tempElem;
                                elementValueSet = true;
                                if (quoteChar != '\0' && i < charLength && charPtr[i] == quoteChar) i++;
                                continue;
                            }

                            int attrNameStart = i;
                            if (!IsValidAttributeNameStartChar(charPtr[i])) throw new InvalidCharacterInTagException(new Range(i, 1), tagName);
                            i++;
                            while (i < charLength && IsValidAttributeNameContinueChar(charPtr[i])) i++;
                            Range attrName = new Range(attrNameStart, i - attrNameStart);

                            while (i < charLength && char.IsWhiteSpace(charPtr[i])) i++;

                            Range attrValue = Range.Empty;
                            if (i < charLength && charPtr[i] == '=')
                            {
                                i++; while (i < charLength && char.IsWhiteSpace(charPtr[i])) i++;
                                if (i >= charLength) throw new UnterminatedTagException(attrName, UnterminatedTagException.TagType.Attribute, tagName);

                                int attrValueStart = i; char quoteChar = '\0';
                                if (charPtr[i] == '"' || charPtr[i] == '\'') { quoteChar = charPtr[i]; i++; attrValueStart = i; }
                                while (i < charLength)
                                {
                                    if (quoteChar != '\0' && charPtr[i] == quoteChar) break;
                                    if (quoteChar == '\0' && (IsWhiteSpaceOrTagChar(charPtr[i]))) break;
                                    i++;
                                }
                                if (i >= charLength && quoteChar != '\0') throw new UnterminatedTagException(new Range(attrValueStart > 0 ? attrValueStart - 1 : attrValueStart, charLength - (attrValueStart > 0 ? attrValueStart - 1 : attrValueStart)), UnterminatedTagException.TagType.Attribute, attrName); // Context is attrName
                                attrValue = new Range(attrValueStart, i - attrValueStart);
                                if (quoteChar != '\0' && i < charLength && charPtr[i] == quoteChar) i++;
                            }
                            else if (!rules.HasFlag(MarkupRuleFlag.AllowValuelessAttribute))
                            {
                                throw new ValuelessAttributeNotAllowedException(attrName, tagName);
                            }
                            attributes.Add(new ElementAttribute { ElementIndex = currentElementIndex, Key = attrName, Value = attrValue });
                        }

                        bool selfClosing = false;
                        if (i < charLength && charPtr[i] == '/')
                        {
                            i++;
                            if (i < charLength && charPtr[i] == '>')
                            {
                                if (!rules.HasFlag(MarkupRuleFlag.AllowEmptyTag)) throw new SelfClosingTagNotAllowedException(new Range(tagStartIndex, i - tagStartIndex + 1), tagName);
                                selfClosing = true;
                            }
                            else throw new UnterminatedTagException(new Range(tagStartIndex, i - tagStartIndex), UnterminatedTagException.TagType.Opening, tagName);
                        }

                        if (i < charLength && charPtr[i] == '>')
                        {
                            i++;
                            Element currentElem = elements[currentElementIndex];
                            currentElem.FullOpeningTag = new Range(tagStartIndex, i - tagStartIndex);
                            if (selfClosing) currentElem.Content = Range.Empty;
                            else
                            {
                                currentElem.Content = new Range(i, 0);
                                openElementStack.Add(new OpenElementInfo { ElementListIndex = currentElementIndex, TagName = tagName, ContentStartIndex = i });
                            }
                            elements[currentElementIndex] = currentElem;
                        }
                        else throw new UnterminatedTagException(new Range(tagStartIndex, charLength - tagStartIndex), UnterminatedTagException.TagType.Opening, tagName);
                    }
                    else
                    {
                        i++;
                    }
                }

                if (openElementStack.Length > 0)
                {
                    OpenElementInfo lastOpenElement = openElementStack[openElementStack.Length - 1];
                    Element elementToReport = elements[lastOpenElement.ElementListIndex];
                    throw new UnclosedTagException(elementToReport.TagName, elementToReport.FullOpeningTag);
                }
            }
            finally
            {
                if (openElementStack.IsCreated) openElementStack.Dispose();
            }
        }

        private static int CompareRanges(char* ptr1, Range range1, char* ptr2, Range range2)
        {
            if (range1.Length != range2.Length) return range1.Length - range2.Length;
            if (range1.IsEmpty) return 0;
            return UnsafeUtility.MemCmp(ptr1 + range1.Start, ptr2 + range2.Start, (long)range1.Length * sizeof(char));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWhiteSpaceOrTagChar(char c)
        {
            return char.IsWhiteSpace(c) || c == '>' || c == '/';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidTagNameStartChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidTagNameChar(char c)
        {
            return IsValidTagNameStartChar(c) || (c >= '0' && c <= '9') || c == '-' || c == '.';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidAttributeNameStartChar(char c)
        {
            return IsValidTagNameStartChar(c); // Attributes can start with same chars as tags
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidAttributeNameContinueChar(char c)
        {
            // Attributes can also contain ':', e.g. for namespaces, but not '=', '>', '/' or whitespace
            return IsValidTagNameChar(c) || c == ':';
        }
        private static int AttributesLengthForElement(int elementIndex, in NativeList<ElementAttribute> allAttributes)
        {
            int count = 0;
            for (int i = 0; i < allAttributes.Length; ++i)
            {
                if (allAttributes[i].ElementIndex == elementIndex) count++;
            }
            return count;
        }
    }
}
