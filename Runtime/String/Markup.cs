using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

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
        public Range TagName;
        public Range TagValue; // For <tag=value> syntax

        public bool Equals(Element other) => TagName.Equals(other.TagName) && TagValue.Equals(other.TagValue);
        public override int GetHashCode() => TagName.GetHashCode() ^ (TagValue.GetHashCode() << 2);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ElementAttribute : IEquatable<ElementAttribute>
    {
        public int OwnerElementIndex; // Index into features.Elements
        public Range Key;
        public Range Value;

        public bool Equals(ElementAttribute other) => OwnerElementIndex == other.OwnerElementIndex && Key.Equals(other.Key) && Value.Equals(other.Value);
        public override int GetHashCode() => OwnerElementIndex.GetHashCode() ^ (Key.GetHashCode() << 2) ^ (Value.GetHashCode() << 4);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ElementContent : IEquatable<ElementContent>
    {
        public int OwnerElementIndex;   // Index of the parent element in features.Elements (-1 for root content if supported)
        public int ContentElementIndex; // Index of a child element in features.Elements if this content IS an element. -1 if it's text.
        public Range Content;           // Range of plain text in the original input string. Empty if ContentElementIndex is valid.

        public bool IsText => ContentElementIndex < 0;
        public bool IsNestedElement => ContentElementIndex >= 0;

        public bool Equals(ElementContent other) =>
            OwnerElementIndex == other.OwnerElementIndex &&
            ContentElementIndex == other.ContentElementIndex &&
            Content.Equals(other.Content);

        public override int GetHashCode() => OwnerElementIndex.GetHashCode() ^ (ContentElementIndex.GetHashCode() << 2) ^ (Content.GetHashCode() << 4);
    }


    // Internal struct for the parser's open tag stack
    [StructLayout(LayoutKind.Sequential)]
    internal struct OpenTagInfo // Renamed from OpenElementInfo to avoid confusion with output Element
    {
        public int ElementIndexInOutput; // Index in the 'features.Elements' list
        public Range TagName;            // For matching closing tags
    }

    // Output structure
    public struct TextMarkupFeatures : IDisposable
    {
        public NativeList<Element> Elements;
        public NativeList<ElementAttribute> Attributes;
        public NativeList<ElementContent> Contents;

        public TextMarkupFeatures(Allocator allocator, int initialElementCapacity = 16, int initialAttributeCapacity = 32, int initialContentCapacity = 16)
        {
            Elements = new NativeList<Element>(initialElementCapacity, allocator);
            Attributes = new NativeList<ElementAttribute>(initialAttributeCapacity, allocator);
            Contents = new NativeList<ElementContent>(initialContentCapacity, allocator);
        }

        public void Dispose()
        {
            if (Elements.IsCreated) Elements.Dispose();
            if (Attributes.IsCreated) Attributes.Dispose();
            if (Contents.IsCreated) Contents.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            var h1 = Elements.IsCreated ? Elements.Dispose(inputDeps) : inputDeps;
            var h2 = Attributes.IsCreated ? Attributes.Dispose(h1) : h1;
            return Contents.IsCreated ? Contents.Dispose(h2) : h2;
        }
    }


    // --- Custom Burst-Friendly Exception Types ---
    public class MarkupParseException : ArgumentException { public Range ErrorRange { get; } public Range ContextRange { get; } public MarkupParseException(Range errorRange, Range contextRange = default) : base() { ErrorRange = errorRange; ContextRange = contextRange; } protected MarkupParseException(string message, Range errorRange, Range contextRange = default) : base(message) { ErrorRange = errorRange; ContextRange = contextRange; } }
    public class ValuelessAttributeNotAllowedException : MarkupParseException { public ValuelessAttributeNotAllowedException(Range attributeNameRange, Range tagNameRange) : base(attributeNameRange, tagNameRange) { } }
    public class ElementValueNotAllowedException : MarkupParseException { public ElementValueNotAllowedException(Range tagNameRange, Range elementValueOperatorRange) : base(elementValueOperatorRange, tagNameRange) { } }
    public class SelfClosingTagNotAllowedException : MarkupParseException { public SelfClosingTagNotAllowedException(Range selfClosingTagRange, Range tagNameRange) : base(selfClosingTagRange, tagNameRange) { } }
    public class EmptyTagNameException : MarkupParseException { public EmptyTagNameException(Range errorRange) : base(errorRange) { } }
    public class MismatchedClosingTagException : MarkupParseException { public Range ExpectedTagNameRange { get; } public Range ActualTagNameRange { get; } public MismatchedClosingTagException(Range actualTagNameRange, Range expectedTagNameRange) : base(actualTagNameRange, expectedTagNameRange) { ExpectedTagNameRange = expectedTagNameRange; ActualTagNameRange = actualTagNameRange; } }
    public class UnclosedTagException : MarkupParseException { public UnclosedTagException(Range tagNameRange, Range fullOpeningTagRange) : base(fullOpeningTagRange, tagNameRange) { } }
    public class UnterminatedTagException : MarkupParseException { public enum TagType { Opening, Closing, Attribute, ElementValue } public TagType TypeOfTag { get; } public UnterminatedTagException(Range errorRange, TagType type, Range contextTagRange = default) : base(errorRange, contextTagRange) { TypeOfTag = type; } }
    public class InvalidCharacterInTagException : MarkupParseException { public InvalidCharacterInTagException(Range errorRange, Range contextTagRange = default) : base(errorRange, contextTagRange) { } }
    public class MultipleElementValuesException : MarkupParseException { public MultipleElementValuesException(Range errorRange, Range tagNameRange) : base(errorRange, tagNameRange) { } }


    public static unsafe class MarkupParser
    {
        public static void ParseMarkup(
            string str,
            Allocator allocator,
            out TextMarkupFeatures features,
            MarkupRuleFlag rules = MarkupRuleFlag.AllowValuelessAttribute | MarkupRuleFlag.AllowEmptyTag)
        {
            if (string.IsNullOrEmpty(str))
            {
                features = new TextMarkupFeatures(allocator, 0, 0, 0);
                return;
            }

            fixed (char* charPtr = str)
            {
                try
                {
                    ParseMarkupInternal(charPtr, str.Length, allocator, out features, rules);
                }
                catch (ValuelessAttributeNotAllowedException e) { throw new ArgumentException($"Valueless attribute '{e.ErrorRange.GetString(charPtr)}' not allowed for tag '{e.ContextRange.GetString(charPtr)}' at index {e.ErrorRange.Start}. Rule 'AllowValuelessAttribute' is not set.", e); }
                catch (ElementValueNotAllowedException e) { throw new ArgumentException($"Element value syntax not allowed for tag '{e.ContextRange.GetString(charPtr)}' near '=' at index {e.ErrorRange.Start}. Rule 'AllowElementValue' is not set.", e); }
                catch (SelfClosingTagNotAllowedException e) { throw new ArgumentException($"Self-closing tag '{(e.ContextRange.GetString(charPtr))}' (full tag: '{e.ErrorRange.GetString(charPtr)}') not allowed at index {e.ErrorRange.Start}. Rule 'AllowEmptyTag' is not set.", e); }
                catch (EmptyTagNameException e) { throw new ArgumentException($"Empty tag name at index {e.ErrorRange.Start}. Found '{charPtr[e.ErrorRange.Start]}'.", e); }
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
                    throw new ArgumentException($"Unterminated {tagTypeStr} tag{contextStr} starting near index {e.ErrorRange.Start}.", e);
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
            out TextMarkupFeatures features,
            MarkupRuleFlag rules = MarkupRuleFlag.AllowValuelessAttribute | MarkupRuleFlag.AllowEmptyTag)
        {
            features = new TextMarkupFeatures(allocator);

            if (charPtr == null || charLength == 0) return;

            var openElementStack = new NativeList<OpenTagInfo>(8, Allocator.Temp);
            try
            {
                int i = 0;
                int currentPlainTextStart = 0;

                while (i < charLength)
                {
                    if (charPtr[i] == '<')
                    {
                        if (i > currentPlainTextStart)
                        {
                            int ownerIdx = openElementStack.Length > 0 ? openElementStack[openElementStack.Length - 1].ElementIndexInOutput : -1;
                            features.Contents.Add(new ElementContent
                            {
                                OwnerElementIndex = ownerIdx,
                                ContentElementIndex = -1,
                                Content = new Range(currentPlainTextStart, i - currentPlainTextStart)
                            });
                        }

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
                                OpenTagInfo lastOpenElement = openElementStack[openElementStack.Length - 1];
                                if (CompareRanges(charPtr, lastOpenElement.TagName, charPtr, closingTagName) == 0)
                                {
                                    openElementStack.RemoveAt(openElementStack.Length - 1);
                                }
                                else throw new MismatchedClosingTagException(closingTagName, lastOpenElement.TagName);
                            }
                            else throw new MismatchedClosingTagException(closingTagName, Range.Empty);
                            currentPlainTextStart = i;
                            continue;
                        }

                        // Opening Tag
                        int openingTagNameStart = i;
                        if (i >= charLength || !IsValidTagNameStartChar(charPtr[i])) throw new EmptyTagNameException(new Range(i, (i < charLength && charPtr[i] == '>') ? 1 : 0));
                        while (i < charLength && IsValidTagNameChar(charPtr[i])) i++;
                        Range tagName = new Range(openingTagNameStart, i - openingTagNameStart);

                        int currentElementListIndex = features.Elements.Length;
                        Range elementValue = Range.Empty;
                        features.Elements.Add(new Element { TagName = tagName, TagValue = Range.Empty });

                        bool elementValueSet = false;
                        while (i < charLength && charPtr[i] != '>' && !(charPtr[i] == '/' && i + 1 < charLength && charPtr[i + 1] == '>'))
                        {
                            while (i < charLength && char.IsWhiteSpace(charPtr[i])) i++;
                            if (i >= charLength || charPtr[i] == '>' || (charPtr[i] == '/' && i + 1 < charLength && charPtr[i + 1] == '>')) break;

                            bool isPotentialElementValue = charPtr[i] == '=' && features.Elements[currentElementListIndex].TagName.Start == openingTagNameStart && AttributesLengthForElement(currentElementListIndex, features.Attributes) == 0;

                            if (isPotentialElementValue)
                            {
                                if (!rules.HasFlag(MarkupRuleFlag.AllowElementValue)) throw new ElementValueNotAllowedException(tagName, new Range(i, 1));
                                if (elementValueSet) throw new MultipleElementValuesException(new Range(i, 1), tagName);

                                i++;
                                int valStart = i; // Corrected: Declare elementValueStart as valStart here
                                char quoteChar = '\0';
                                if (i < charLength && (charPtr[i] == '"' || charPtr[i] == '\'')) { quoteChar = charPtr[i]; i++; valStart = i; }

                                int valueContentStart = valStart; // Store the actual start of value content
                                while (i < charLength)
                                {
                                    if (quoteChar != '\0' && charPtr[i] == quoteChar) break;
                                    if (quoteChar == '\0' && (IsWhiteSpaceOrTagChar(charPtr[i]))) break;
                                    i++;
                                }
                                // Use valueContentStart for Range, and valStart for exception context if needed
                                if (i >= charLength && quoteChar != '\0') throw new UnterminatedTagException(new Range(valStart > 0 ? valStart - 1 : valStart, charLength - (valStart > 0 ? valStart - 1 : valStart)), UnterminatedTagException.TagType.ElementValue, tagName);

                                elementValue = new Range(valueContentStart, i - valueContentStart);
                                Element tempElem = features.Elements[currentElementListIndex]; tempElem.TagValue = elementValue; features.Elements[currentElementListIndex] = tempElem;
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

                                int valStart = i; // Declare here for attribute value
                                char quoteChar = '\0';
                                if (charPtr[i] == '"' || charPtr[i] == '\'') { quoteChar = charPtr[i]; i++; valStart = i; }

                                int valueContentStart = valStart;
                                while (i < charLength)
                                {
                                    if (quoteChar != '\0' && charPtr[i] == quoteChar) break;
                                    if (quoteChar == '\0' && (IsWhiteSpaceOrTagChar(charPtr[i]))) break;
                                    i++;
                                }
                                if (i >= charLength && quoteChar != '\0') throw new UnterminatedTagException(new Range(valueContentStart > 0 ? valueContentStart - 1 : valueContentStart, charLength - (valueContentStart > 0 ? valueContentStart - 1 : valueContentStart)), UnterminatedTagException.TagType.Attribute, attrName);
                                attrValue = new Range(valueContentStart, i - valueContentStart);
                                if (quoteChar != '\0' && i < charLength && charPtr[i] == quoteChar) i++;
                            }
                            else if (!rules.HasFlag(MarkupRuleFlag.AllowValuelessAttribute))
                            {
                                throw new ValuelessAttributeNotAllowedException(attrName, tagName);
                            }
                            features.Attributes.Add(new ElementAttribute { OwnerElementIndex = currentElementListIndex, Key = attrName, Value = attrValue });
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
                            if (openElementStack.Length > 0)
                            {
                                OpenTagInfo parentInfo = openElementStack[openElementStack.Length - 1];
                                features.Contents.Add(new ElementContent
                                {
                                    OwnerElementIndex = parentInfo.ElementIndexInOutput,
                                    ContentElementIndex = currentElementListIndex,
                                    Content = Range.Empty
                                });
                            }
                            if (!selfClosing)
                            {
                                openElementStack.Add(new OpenTagInfo { ElementIndexInOutput = currentElementListIndex, TagName = tagName });
                            }
                            currentPlainTextStart = i;
                        }
                        else throw new UnterminatedTagException(new Range(tagStartIndex, charLength - tagStartIndex), UnterminatedTagException.TagType.Opening, tagName);
                    }
                    else
                    {
                        i++;
                    }
                }

                if (currentPlainTextStart < charLength)
                {
                    int ownerIdx = openElementStack.Length > 0 ? openElementStack[openElementStack.Length - 1].ElementIndexInOutput : -1;
                    features.Contents.Add(new ElementContent
                    {
                        OwnerElementIndex = ownerIdx,
                        ContentElementIndex = -1,
                        Content = new Range(currentPlainTextStart, charLength - currentPlainTextStart)
                    });
                }

                if (openElementStack.Length > 0)
                {
                    OpenTagInfo lastOpenElement = openElementStack[openElementStack.Length - 1];
                    throw new UnclosedTagException(lastOpenElement.TagName, lastOpenElement.TagName); // Context for FullOpeningTag needs to be found if needed
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
            return IsValidTagNameStartChar(c);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidAttributeNameContinueChar(char c)
        {
            return IsValidTagNameChar(c) || c == ':';
        }
        private static int AttributesLengthForElement(int elementIndex, in NativeList<ElementAttribute> allAttributes)
        {
            int count = 0;
            for (int i = 0; i < allAttributes.Length; ++i)
            {
                if (allAttributes[i].OwnerElementIndex == elementIndex) count++;
            }
            return count;
        }
    }
}
