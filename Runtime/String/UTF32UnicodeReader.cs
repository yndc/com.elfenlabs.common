using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Elfenlabs.String
{
    /// <summary>
    /// Reads Unicode code points from a UTF-32 encoded buffer.
    /// Assumes each uint in the buffer is a valid Unicode code point.
    /// This struct is designed to be Burst-compatible.
    /// </summary>
    [BurstCompile]
    public unsafe struct UTF32UnicodeReader : IEnumerable<int>
    {
        private readonly uint* m_Ptr;  // Pointer to the start of the UTF-32 data (array of uints)
        private readonly int m_Length; // Number of code points (uint elements) in the buffer

        /// <summary>
        /// Enumerator for iterating over Unicode code points in a UTF-32 buffer.
        /// </summary>
        public struct Enumerator : IEnumerator<int>
        {
            private readonly uint* m_Ptr;
            private readonly int m_Length;
            private int m_CurrentIndex; // Current index in the uint array
            private int m_CurrentCodepoint;

            public int Current => m_CurrentCodepoint;

            object IEnumerator.Current => Current;

            public Enumerator(in UTF32UnicodeReader reader) // Pass reader by readonly ref
            {
                this.m_Ptr = reader.m_Ptr;
                this.m_Length = reader.m_Length;
                this.m_CurrentIndex = -1; // Position before the first element
                this.m_CurrentCodepoint = 0;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                m_CurrentIndex++;
                if (m_CurrentIndex < m_Length)
                {
                    // Each uint is a code point. Cast to int for IEnumerator<int>.
                    m_CurrentCodepoint = (int)m_Ptr[m_CurrentIndex];
                    return true;
                }
                else
                {
                    m_CurrentCodepoint = 0; // Or some other indicator for end
                    return false;
                }
            }

            public void Reset()
            {
                m_CurrentIndex = -1;
                m_CurrentCodepoint = 0;
            }
        }

        /// <summary>
        /// Initializes a new UTF32UnicodeReader from a raw pointer and count.
        /// </summary>
        /// <param name="utf32Ptr">Pointer to the start of the UTF-32 data (array of uints).</param>
        /// <param name="codePointCount">The number of code points (uint elements) in the buffer.</param>
        public UTF32UnicodeReader(uint* utf32Ptr, int codePointCount)
        {
            this.m_Ptr = utf32Ptr;
            this.m_Length = codePointCount;
        }

        /// <summary>
        /// Initializes a new UTF32UnicodeReader from a NativeArray of uints.
        /// </summary>
        /// <param name="utf32Array">The NativeArray containing UTF-32 code points.</param>
        public UTF32UnicodeReader(NativeArray<uint> utf32Array)
        {
            if (!utf32Array.IsCreated)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentNullException(nameof(utf32Array), "Input NativeArray has not been created.");
#else
            this.m_Ptr = null;
            this.m_Length = 0;
            return;
#endif
            }
            this.m_Ptr = (uint*)utf32Array.GetUnsafeReadOnlyPtr(); // Get read-only pointer
            this.m_Length = utf32Array.Length;
        }


        /// <summary>
        /// Gets the Unicode code point at the specified index.
        /// </summary>
        /// <param name="index">The index of the code point.</param>
        /// <returns>The Unicode code point as an int.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCodePointAt(int index)
        {
            if ((uint)index >= (uint)m_Length) // Unsigned trick for checking 0 <= index < length
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new System.IndexOutOfRangeException($"Index {index} is out of range for UTF32UnicodeReader of length {m_Length}.");
#else
            return 0xFFFD; // Replacement character if bounds checks are off and index is bad
#endif
            }
            return (int)m_Ptr[index];
        }

        /// <summary>
        /// Gets the total number of code points in the buffer.
        /// </summary>
        public int Length => m_Length;


        // --- Character Property Checks using UnicodeUtility ---

        /// <summary>
        /// Checks if the code point at the given index represents a newline character.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNewLine(int index)
        {
            if ((uint)index >= (uint)m_Length) return false;
            return UnicodeUtility.IsNewLine(m_Ptr[index]);
        }

        /// <summary>
        /// Checks if the code point at the given index represents a whitespace character.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWhiteSpace(int index)
        {
            if ((uint)index >= (uint)m_Length) return false;
            return UnicodeUtility.IsWhiteSpace(m_Ptr[index]);
        }

        /// <summary>
        /// Checks if the code point at the given index falls within common CJK ranges.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCJK(int index)
        {
            if ((uint)index >= (uint)m_Length) return false;
            return UnicodeUtility.IsCJK(m_Ptr[index]);
        }

        /// <summary>
        /// Determines if a line break is allowed *after* the character at the current index.
        /// A break is allowed after whitespace/newline, OR if the *next* character is CJK.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBreakOpportunity(int index)
        {
            if ((uint)index >= (uint)m_Length) return false; // Current index out of bounds

            uint currentCodePoint = m_Ptr[index];

            // Break opportunity *after* current if it's whitespace or newline
            if (UnicodeUtility.IsWhiteSpace(currentCodePoint) || UnicodeUtility.IsNewLine(currentCodePoint))
            {
                return true;
            }

            // Check if there's a next character
            int nextIndex = index + 1;
            if ((uint)nextIndex < (uint)m_Length) // Check if next index is within bounds
            {
                uint nextCodePoint = m_Ptr[nextIndex];
                if (UnicodeUtility.IsCJK(nextCodePoint))
                {
                    return true; // Break opportunity *before* the next CJK character
                }
            }
            // Optional: Add rule to break *after* a CJK if current is CJK and next is not (or end of string)
            // if (UnicodeUtility.IsCJK(currentCodePoint))
            // {
            //     if (!((uint)nextIndex < (uint)m_Length) || // End of string
            //         !UnicodeUtility.IsCJK(m_Ptr[nextIndex]))
            //     {
            //         return true;
            //     }
            // }

            return false;
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