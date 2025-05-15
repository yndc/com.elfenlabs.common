using System;
using System.Runtime.InteropServices;
using Codice.Utils;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.VisualScripting.YamlDotNet.Core;
using UnityEngine.UI;

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

        public Range Union(Range other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;
            return new Range(
                Math.Min(Start, other.Start),
                Math.Max(Start + Length, other.Start + other.Length) - Math.Min(Start, other.Start));
        }

        public Range Intersect(Range other)
        {
            if (IsEmpty || other.IsEmpty) return Empty;
            int start = Math.Max(Start, other.Start);
            int end = Math.Min(Start + Length, other.Start + other.Length);
            return new Range(start, Math.Max(0, end - start));
        }

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
    public struct Element
    {
        public int Index;
        public Range TagName;
        public Range TagValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ElementAttribute
    {
        public int OwnerElementIndex; // Index into features.Elements
        public Range Key;
        public Range Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ElementContent
    {
        public int OwnerElementIndex;
        public int ContentElementIndex;
        public Range Content;

        public bool IsText => ContentElementIndex < 0;
        public bool IsNestedElement => ContentElementIndex >= 0;
    }

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

    public unsafe struct TextMarkupParser
    {
        byte* textPtr;
        int textLength;
        TextMarkupFeatures result;
        int charSize;
        MarkupRuleFlag flags;

        int index;

        NativeList<Element> stack;

        public TextMarkupParser(byte* ptr, int length, int charSize, Allocator allocator, MarkupRuleFlag flags)
        {
            this.textPtr = ptr;
            this.textLength = length;
            this.charSize = charSize;
            this.result = new TextMarkupFeatures(allocator);
            this.flags = flags;
            this.stack = new NativeList<Element>(16, allocator);
            index = 0;
        }

        public void Parse(MarkupRuleFlag flags)
        {
            do
            {
                var c = CurrentChar();
                switch (c)
                {
                    case '<':
                        if (TryParseElement())
                        {

                        }
                        break;
                }
            } while (Advance());
        }

        bool Advance(int count = 1)
        {
            index += count * charSize;
            if (index >= textLength)
            {
                index = textLength - 1;
                return false;
            }
            return true;
        }

        void SkipWhiteSpace()
        {
            Advance();
            while (index < textLength && IsWhiteSpace(CurrentChar()))
            {
                Advance();
            }
        }

        char CurrentChar()
        {
            if (index >= textLength) return '\0';
            return (char)textPtr[index];
        }

        void ParseTagOpening()
        {
            var start = index; // '<
            Advance();

            // Closing tag
            if (CurrentChar() == '/')
            {
                Advance();
                if (TryParseTagName(out var tagName))
                {
                    SkipWhiteSpace();
                    if (CurrentChar() == '>')
                    {
                        // Check with the top of the stack
                        var lastElementIndex = stack.Length - 1;
                        var lastElement = stack[lastElementIndex];
                        if (CompareRangeContent(lastElement.TagName, tagName) == 0)
                        {
                            result.Elements.Add(new Element
                            {
                                Index = lastElement.Index,
                                TagName = tagName,
                                TagValue = lastElement.TagValue
                            });
                            stack.RemoveAt(lastElementIndex);
                            return;
                        }

                        // Trying to close a tag that is not the last one
                        // Interleaved tags are not supported

                        throw new Exception($"Invalid closing tag:");
                    }
                }
            }

            if (TryParseTagName(out var tagName))
            {
                SkipWhiteSpace();
                if (CurrentChar() == '>')
                {
                    Advance();
                    result.Elements.Add(new Element
                    {
                        TagName = tagName,
                        Index = result.Elements.Length - 1,
                    });
                }
                else
                {
                    index = start;
                }
            }
        }

        bool TryParseElement(out Element element)
        {
            element = new Element();

            var start = index; // '<'
            Advance();
            if (TryParseTagName(out var tagName))
            {
                element.TagName = tagName;
                SkipWhiteSpace();

                if (flags.HasFlag(MarkupRuleFlag.AllowElementValue) && CurrentChar() == '=')
                {
                    SkipWhiteSpace();
                    var value = ParseValue();
                    element.TagValue = value;
                }

                if (CurrentChar() == '>')
                {
                    stack.Add(element);
                    Advance();
                    ParseContent(ref element);
                    return true;
                }

                // Self-closing tag
                else if (CurrentChar() == '/')
                {
                    Advance();
                    if (CurrentChar() == '>')
                    {
                        Advance();
                        result.Elements.Add(element);
                        return true;
                    }
                    else
                    {
                        index = start;
                        return false;
                    }
                }
                else
                {
                    index = start;
                    return false;
                }
            }

            return false;
        }

        bool TryParseTagName(out Range tagName)
        {
            tagName = Range.Empty;
            if (!IsValidIdentifierStart(CurrentChar()))
            {
                return false;
            }

            var start = index;
            while (Advance() && IsValidIdentifier(CurrentChar())) { }
            tagName = new Range(start, index - start);
            return true;
        }

        bool TryParseTagValue()
        {

        }

        void ParseContent(ref Element element)
        {
            var start = index;
            do
            {
                var c = CurrentChar();
                if (c == '<')
                {
                    var endContentIndex = index;
                    if (TryParseElement(out var nested))
                    {
                        result.Contents.Add(new ElementContent
                        {
                            OwnerElementIndex = element.Index,
                            Content = new Range(start, index - endContentIndex),
                        });
                        result.Contents.Add(new ElementContent
                        {
                            OwnerElementIndex = element.Index,
                            ContentElementIndex = result.Elements.Length - 1,
                        });
                    }
                    else
                    {

                    }
                }
            }
            while (Advance() && CurrentChar() != '<');
        }

        void ParseClosingTag()
        {
            var start = index;
            if (CurrentChar() != '/')
            {
                Advance();
                SkipWhiteSpace();
                if (TryParseTagName(out var tagName))
                {
                    SkipWhiteSpace();
                    if (CurrentChar() == '>')
                    {
                        Advance();
                        // Handle closing tag
                    }
                }
            }
        }

        Range ParseValue()
        {
            var start = index + charSize;
            while (Advance() && CurrentChar() != '"')
            {

            }
            return new Range(start, index - start - charSize);
        }

        int CompareTag(Element a, Element b)
        {
            return CompareRangeContent(a.TagName, b.TagName);
        }

        int CompareRangeContent(Range a, Range b)
        {
            if (a.Length != b.Length)
                return a.Length - b.Length;
            if (a.Start == b.Start)
                return 0;
            return UnsafeUtility.MemCmp(
                textPtr + a.Start,
                textPtr + b.Start,
                a.Length);
        }

        bool IsValidIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_';
        }

        bool IsValidIdentifier(char c)
        {
            return char.IsLetter(c) || char.IsDigit(c) || c == '_' || c == '-';
        }

        bool IsWhiteSpace(char c)
        {
            return c == ' ' || c == '\t' || c == '\n' || c == '\r';
        }
    }
}