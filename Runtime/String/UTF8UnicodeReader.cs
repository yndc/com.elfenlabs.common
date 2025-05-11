using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Elfenlabs.String
{
    public unsafe struct UTF8UnicodeReader : IEnumerable<int>
    {
        private int length;
        private byte* ptr;

        public struct Enumerator : IEnumerator<int>
        {
            private int index;
            private UTF8UnicodeReader reader;
            private int currentCodepoint;

            public int Current => currentCodepoint;

            object IEnumerator.Current => Current;

            public Enumerator(UTF8UnicodeReader reader)
            {
                this.reader = reader;
                this.index = -1; // Position before the first element
                this.currentCodepoint = 0;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                if (index == -1) // First call to MoveNext
                {
                    index = 0;
                }
                else
                {
                    // Advance index based on the length of the previous character
                    if (reader.IsASCII(index))
                    {
                        index += 1;
                    }
                    else if (reader.Is2ByteSequence(index))
                    {
                        index += 2;
                    }
                    else if (reader.Is3ByteSequence(index))
                    {
                        index += 3;
                    }
                    else if (reader.Is4ByteSequence(index))
                    {
                        index += 4;
                    }
                    else
                    {
                        // Invalid UTF-8 sequence or end of string, advance by 1 to be safe or handle error
                        index += 1;
                    }
                }

                if (index >= reader.length)
                {
                    currentCodepoint = 0; // Or some other indicator for end
                    return false;
                }

                if (reader.IsASCII(index))
                {
                    currentCodepoint = reader.GetASCII(index);
                }
                else if (reader.Is2ByteSequence(index))
                {
                    if (index + 1 < reader.length)
                        currentCodepoint = (int)reader.GetUnicodeFrom2ByteSequence(index);
                    else { currentCodepoint = 0xFFFD; return false; } // Truncated sequence
                }
                else if (reader.Is3ByteSequence(index))
                {
                    if (index + 2 < reader.length)
                        currentCodepoint = (int)reader.GetUnicodeFrom3ByteSequence(index);
                    else { currentCodepoint = 0xFFFD; return false; } // Truncated sequence
                }
                else if (reader.Is4ByteSequence(index))
                {
                    if (index + 3 < reader.length)
                        currentCodepoint = (int)reader.GetUnicodeFrom4ByteSequence(index);
                    else { currentCodepoint = 0xFFFD; return false; } // Truncated sequence
                }
                else
                {
                    // Invalid starting byte for a UTF-8 sequence
                    currentCodepoint = 0xFFFD; // Unicode replacement character
                }
                return true;
            }

            public void Reset()
            {
                index = -1;
                currentCodepoint = 0;
            }
        }

        public UTF8UnicodeReader(byte* ptr, int length)
        {
            this.ptr = ptr;
            this.length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsASCII(int index)
        {
            return ((*(ptr + index)) & 0x80) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Is2ByteSequence(int index)
        {
            return ((*(ptr + index)) & 0xE0) == 0xC0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Is3ByteSequence(int index)
        {
            return ((*(ptr + index)) & 0xF0) == 0xE0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Is4ByteSequence(int index)
        {
            return ((*(ptr + index)) & 0xF8) == 0xF0;
        }


        public unsafe byte GetASCII(int index)
        {
            return ptr[index];
        }

        public unsafe uint GetUnicodeFrom2ByteSequence(int index)
        {
            byte b0 = ptr[index + 0];
            byte b1 = ptr[index + 1];
            if ((b1 & 0xC0) != 0x80)
            {
                return 0xFFFD;
            }
            return ((uint)(b0 & 0x1F) << 6) | (uint)(b1 & 0x3F);
        }

        public unsafe uint GetUnicodeFrom3ByteSequence(int index)
        {
            byte b0 = ptr[index + 0];
            byte b1 = ptr[index + 1];
            byte b2 = ptr[index + 2];
            if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80)
            {
                return 0xFFFD;
            }
            return ((uint)(b0 & 0x0F) << 12) | ((uint)(b1 & 0x3F) << 6) | (uint)(b2 & 0x3F);
        }

        public unsafe uint GetUnicodeFrom4ByteSequence(int index)
        {
            byte b0 = ptr[index + 0];
            byte b1 = ptr[index + 1];
            byte b2 = ptr[index + 2];
            byte b3 = ptr[index + 3];
            if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80)
            {
                return 0xFFFD;
            }
            return ((uint)(b0 & 0x07) << 18) | ((uint)(b1 & 0x3F) << 12) | ((uint)(b2 & 0x3F) << 6) | (uint)(b3 & 0x3F);
        }

        public bool IsNewLine(int index)
        {
            // Check for common newline characters
            if (ptr[index] == '\n' || ptr[index] == '\r')
            {
                return true;
            }

            // Check for other newline characters in UTF-8
            if (Is3ByteSequence(index) && GetUnicodeFrom3ByteSequence(index) == 0x2028)
            {
                return true;
            }

            if (Is4ByteSequence(index) && GetUnicodeFrom4ByteSequence(index) == 0x2029)
            {
                return true;
            }

            return false;
        }

        public bool IsWhiteSpace(int index)
        {
            // Check for common whitespace characters
            if (ptr[index] == ' ' || ptr[index] == '\t' || ptr[index] == '\n' || ptr[index] == '\r')
            {
                return true;
            }

            // Check for other whitespace characters in UTF-8
            if (Is3ByteSequence(index) && GetUnicodeFrom3ByteSequence(index) == 0x2028)
            {
                return true;
            }

            if (Is4ByteSequence(index) && GetUnicodeFrom4ByteSequence(index) == 0x2029)
            {
                return true;
            }

            return false;
        }

        public bool IsBreakOpportunity(int index)
        {
            if (IsNewLine(index) || IsWhiteSpace(index))
            {
                return true;
            }

            if (IsCJK(index))
            {
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCJK(int index)
        {
            // If the first bit is 0, it's a single-byte ASCII character (U+0000 to U+007F)
            // None of these are CJK characters.
            if (IsASCII(index))
            {
                return false;
            }

            // 2-byte sequences are in the range U+0080 to U+07FF
            // These are not CJK characters either.
            if (Is2ByteSequence(index))
            {
                return false;
            }

            if (Is3ByteSequence(index))
            {
                var codePoint = GetUnicodeFrom3ByteSequence(index);

                // Halfwidth and Fullwidth Forms (Includes Latin chars but often used with CJK)
                if (codePoint >= 0xFF00 && codePoint <= 0xFFEF) return true;
                // CJK Symbols and Punctuation (Common)
                if (codePoint >= 0x3000 && codePoint <= 0x303F) return true;
                // Hiragana
                if (codePoint >= 0x3040 && codePoint <= 0x309F) return true;
                // Katakana & Phonetic Extensions
                if (codePoint >= 0x30A0 && codePoint <= 0x30FF) return true;
                if (codePoint >= 0x31F0 && codePoint <= 0x31FF) return true;
                // Hangul Syllables (Very common Korean script block)
                if (codePoint >= 0xAC00 && codePoint <= 0xD7AF) return true;
                // CJK Unified Ideographs (Main block)
                if (codePoint >= 0x4E00 && codePoint <= 0x9FFF) return true;
                // CJK Unified Ideographs Extension A
                if (codePoint >= 0x3400 && codePoint <= 0x4DBF) return true;
                // Hangul Jamo & Compatibility Jamo
                if (codePoint >= 0x1100 && codePoint <= 0x11FF) return true;
                if (codePoint >= 0x3130 && codePoint <= 0x318F) return true;
                // Bopomofo
                if (codePoint >= 0x3100 && codePoint <= 0x312F) return true;
                if (codePoint >= 0x31A0 && codePoint <= 0x31BF) return true;
                // CJK Compatibility Ideographs
                if (codePoint >= 0xF900 && codePoint <= 0xFAFF) return true;
                // CJK Strokes
                if (codePoint >= 0x31C0 && codePoint <= 0x31EF) return true;
                // Enclosed CJK Letters and Months
                if (codePoint >= 0x3200 && codePoint <= 0x32FF) return true;
                // CJK Compatibility
                if (codePoint >= 0x3300 && codePoint <= 0x33FF) return true;
            }

            if (Is4ByteSequence(index))
            {
                var codePoint = GetUnicodeFrom4ByteSequence(index);

                // CJK Unified Ideographs Extension B (Rarely used but still CJK)
                if (codePoint >= 0x20000 && codePoint <= 0x2A6DF) return true;
            }

            return false;
        }

        public IEnumerator<int> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}