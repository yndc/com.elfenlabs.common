using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Elfenlabs.String
{
    [BurstCompile]
    public unsafe struct UTF32UnicodeReader : IEnumerable<int> // , IUnicodeReader
    {
        private readonly uint* ptr; // Pointer to the start of the UTF-32 data (array of uints)
        private readonly int length;  // Number of code points (uint elements) in the buffer

        /// <summary>
        /// Enumerator for iterating over Unicode code points in a UTF-32 buffer.
        /// </summary>
        public struct Enumerator : IEnumerator<int>
        {
            private readonly uint* ptr;
            private readonly int length;
            private int currentIndex; // Current index in the uint array
            private int currentCodepoint;

            public int Current => currentCodepoint;

            object IEnumerator.Current => Current;

            public Enumerator(in UTF32UnicodeReader reader) // Pass reader by readonly ref
            {
                this.ptr = reader.ptr;
                this.length = reader.length;
                this.currentIndex = -1; // Position before the first element
                this.currentCodepoint = 0;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                currentIndex++;
                if (currentIndex < length)
                {
                    // Each uint is a code point. Cast to int for IEnumerator<int>.
                    currentCodepoint = (int)ptr[currentIndex];
                    return true;
                }
                else
                {
                    currentCodepoint = 0; // Or some other indicator for end
                    return false;
                }
            }

            public void Reset()
            {
                currentIndex = -1;
                currentCodepoint = 0;
            }
        }

        /// <summary>
        /// Initializes a new UTF32UnicodeReader.
        /// </summary>
        /// <param name="utf32Ptr">Pointer to the start of the UTF-32 data (array of uints).</param>
        /// <param name="codePointCount">The number of code points (uint elements) in the buffer.</param>
        public UTF32UnicodeReader(uint* utf32Ptr, int codePointCount)
        {
            this.ptr = utf32Ptr;
            this.length = codePointCount;
        }

        /// <summary>
        /// Gets the Unicode code point at the specified index.
        /// </summary>
        /// <param name="index">The index of the code point.</param>
        /// <returns>The Unicode code point as an int.</returns>
        /// <exception cref="System.IndexOutOfRangeException">Thrown if index is out of bounds.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCodePointAt(int index)
        {
            // Add bounds checking for safety, though Burst might optimize some away
            if ((uint)index >= (uint)length) // Unsigned trick for checking 0 <= index < length
            {
                // In Burst, throwing exceptions is generally avoided for performance.
                // Consider returning a specific error code or relying on caller to bounds check.
                // For now, let's match typical array behavior.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new System.IndexOutOfRangeException($"Index {index} is out of range for UTF32UnicodeReader of length {length}.");
#else
            // In a non-checked build, this might read garbage or crash if out of bounds.
            // It's often better to ensure valid indices before calling.
            return 0xFFFD; // Replacement character if bounds checks are off and index is bad
#endif
            }
            return (int)ptr[index];
        }

        /// <summary>
        /// Gets the total number of code points in the buffer.
        /// </summary>
        public int Length => length;


        // --- Character Property Checks (now operate on code points directly) ---

        /// <summary>
        /// Checks if the code point at the given index represents a newline character.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNewLine(int index)
        {
            if ((uint)index >= (uint)length) return false; // Bounds check
            uint codePoint = ptr[index];
            return codePoint == '\n' || codePoint == '\r' || // LF, CR
                   codePoint == 0x2028 || // Line Separator
                   codePoint == 0x2029;   // Paragraph Separator
        }

        /// <summary>
        /// Checks if the code point at the given index represents a whitespace character.
        /// (Basic check, can be expanded based on Unicode whitespace definition).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWhiteSpace(int index)
        {
            if ((uint)index >= (uint)length) return false; // Bounds check
            uint codePoint = ptr[index];
            // Common whitespace characters
            return codePoint == ' ' || // Space
                   codePoint == '\t' || // Horizontal Tab
                   codePoint == '\n' || // Line Feed
                   codePoint == '\r' || // Carriage Return
                   codePoint == 0x000B || // Vertical Tab
                   codePoint == 0x000C || // Form Feed
                   codePoint == 0x00A0 || // No-Break Space
                   (codePoint >= 0x2000 && codePoint <= 0x200A) || // Various general punctuation spaces
                   codePoint == 0x2028 || // Line Separator
                   codePoint == 0x2029 || // Paragraph Separator
                   codePoint == 0x202F || // Narrow No-Break Space
                   codePoint == 0x205F || // Medium Mathematical Space
                   codePoint == 0x3000;   // Ideographic Space
        }

        /// <summary>
        /// Checks if the code point at the given index falls within common CJK ranges.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCJK(int index)
        {
            if ((uint)index >= (uint)length) return false; // Bounds check
            uint codePoint = ptr[index];

            // ASCII characters are U+0000 to U+007F. None are CJK.
            if (codePoint <= 0x007F) return false;

            // --- Perform CJK Range Checks (Reordered by estimated commonality) ---
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

            // Optional: Add more extensive ranges if needed (CJK Ext B, C, D, etc.)
            // if (codePoint >= 0x20000 && codePoint <= 0x2A6DF) return true; // Ext B

            return false; // Not in any checked CJK range
        }

        /// <summary>
        /// Determines if a character at the given index is a potential line break opportunity.
        /// (Simplified: breaks after whitespace/newline or any CJK character).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBreakOpportunity(int index)
        {
            if ((uint)index >= (uint)length) return false; // Bounds check
                                                           // For UTF-32, IsNewLine/IsWhiteSpace/IsCJK directly check the character at 'index'.
            return IsNewLine(index) || IsWhiteSpace(index) || IsCJK(index);
        }


        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}