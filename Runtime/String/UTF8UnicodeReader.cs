using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Elfenlabs.String
{
    /// <summary>
    /// Reads Unicode code points from a UTF-8 encoded byte buffer.
    /// This struct is designed to be Burst-compatible.
    /// </summary>
    [BurstCompile]
    public unsafe struct UTF8UnicodeReader : IEnumerable<int>
    {
        private readonly byte* m_Ptr;    // Pointer to the start of the UTF-8 data
        private readonly int m_Length; // Total length of the byte buffer in bytes

        /// <summary>
        /// Enumerator for iterating over Unicode code points in a UTF-8 buffer.
        /// </summary>
        public struct Enumerator : IEnumerator<int>
        {
            private readonly byte* m_StartPtr;
            private readonly int m_TotalByteLength;
            private byte* m_CurrentPtr;
            private int m_CurrentCodePoint;
            private int m_BytesConsumedByCurrent;

            public int Current => m_CurrentCodePoint;
            object IEnumerator.Current => Current;

            public Enumerator(in UTF8UnicodeReader reader)
            {
                this.m_StartPtr = reader.m_Ptr;
                this.m_TotalByteLength = reader.m_Length;
                this.m_CurrentPtr = reader.m_Ptr;
                this.m_CurrentCodePoint = 0;
                this.m_BytesConsumedByCurrent = 0;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                if (m_BytesConsumedByCurrent > 0) m_CurrentPtr += m_BytesConsumedByCurrent;
                else if (m_BytesConsumedByCurrent < 0) m_CurrentPtr += 1;

                if (m_CurrentPtr >= m_StartPtr + m_TotalByteLength)
                {
                    m_CurrentCodePoint = 0; m_BytesConsumedByCurrent = 0; return false;
                }

                int remainingBytes = (int)((m_StartPtr + m_TotalByteLength) - m_CurrentPtr);
                m_CurrentCodePoint = (int)DecodeUtf8CodePointInternal(m_CurrentPtr, remainingBytes, out m_BytesConsumedByCurrent);

                if (m_BytesConsumedByCurrent <= 0)
                {
                    m_BytesConsumedByCurrent = -1;
                    return m_CurrentPtr < m_StartPtr + m_TotalByteLength;
                }
                return true;
            }

            public void Reset()
            {
                m_CurrentPtr = m_StartPtr;
                m_CurrentCodePoint = 0;
                m_BytesConsumedByCurrent = 0;
            }
        }

        public UTF8UnicodeReader(byte* utf8Ptr, int byteLength)
        {
            this.m_Ptr = utf8Ptr;
            this.m_Length = byteLength;
        }

        public int ByteLength => m_Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetCodePointAtByteOffset(int byteOffset, out int bytesReadInChar)
        {
            if ((uint)byteOffset >= (uint)m_Length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentOutOfRangeException(nameof(byteOffset), $"Byte offset {byteOffset} is out of range for buffer of length {m_Length}.");
#else
            bytesReadInChar = 0; return 0xFFFD;
#endif
            }
            return DecodeUtf8CodePointInternal(m_Ptr + byteOffset, m_Length - byteOffset, out bytesReadInChar);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNewLine(int byteOffset)
        {
            uint codePoint = GetCodePointAtByteOffset(byteOffset, out int bytesRead);
            return bytesRead > 0 && UnicodeUtility.IsNewLine(codePoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWhiteSpace(int byteOffset)
        {
            uint codePoint = GetCodePointAtByteOffset(byteOffset, out int bytesRead);
            return bytesRead > 0 && UnicodeUtility.IsWhiteSpace(codePoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCJK(int byteOffset)
        {
            uint codePoint = GetCodePointAtByteOffset(byteOffset, out int bytesRead);
            return bytesRead > 0 && UnicodeUtility.IsCJK(codePoint);
        }

        /// <summary>
        /// Determines if a line break is allowed *after* the character at the current byteOffset.
        /// A break is allowed after whitespace/newline, OR if the *next* character is CJK.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBreakOpportunity(int byteOffset)
        {
            uint currentCodePoint = GetCodePointAtByteOffset(byteOffset, out int currentBytesRead);
            if (currentBytesRead == 0) return false; // Invalid current char, no defined break

            // Break opportunity *after* current if it's whitespace or newline
            if (UnicodeUtility.IsWhiteSpace(currentCodePoint) || UnicodeUtility.IsNewLine(currentCodePoint))
            {
                return true;
            }

            // Check if there's a next character
            int nextByteOffset = byteOffset + currentBytesRead;
            if ((uint)nextByteOffset < (uint)m_Length) // Check if next offset is within bounds
            {
                uint nextCodePoint = GetCodePointAtByteOffset(nextByteOffset, out int nextBytesRead);
                if (nextBytesRead > 0 && UnicodeUtility.IsCJK(nextCodePoint))
                {
                    return true; // Break opportunity *before* the next CJK character
                }
            }
            // If current is CJK, and next is not (or end of string), that's also a break.
            // This is implicitly handled if IsCJKCodePoint itself is a break trigger.

            // Add rule to break *after* a CJK if the next is not CJK (or vice versa)
            if (UnicodeUtility.IsCJK(currentCodePoint))
            {
                if (!((uint)nextByteOffset < (uint)m_Length) || // End of string
                    (GetCodePointAtByteOffset(nextByteOffset, out int _) != 0xFFFD
                        && !UnicodeUtility.IsCJK(GetCodePointAtByteOffset(nextByteOffset, out _))))
                {
                    return true;
                }
            }

            return false;
        }
        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [BurstCompile]
        private static uint DecodeUtf8CodePointInternal(byte* ptr, int maxBytesToRead, out int bytesRead)
        {
            bytesRead = 0;
            if (maxBytesToRead <= 0 || ptr == null) return 0xFFFD;
            byte b0 = ptr[0];
            bytesRead = 1;
            if ((b0 & 0x80) == 0) return b0;
            if ((b0 & 0xC0) == 0x80 || (b0 & 0xFE) == 0xFE) { bytesRead = 0; return 0xFFFD; }
            if ((b0 & 0xE0) == 0xC0)
            {
                if (maxBytesToRead < 2) { bytesRead = 0; return 0xFFFD; }
                byte b1 = ptr[1];
                if ((b1 & 0xC0) != 0x80) { bytesRead = 0; return 0xFFFD; }
                bytesRead = 2; uint cp = ((uint)(b0 & 0x1F) << 6) | (uint)(b1 & 0x3F);
                return cp >= 0x80 ? cp : 0xFFFD;
            }
            if ((b0 & 0xF0) == 0xE0)
            {
                if (maxBytesToRead < 3) { bytesRead = 0; return 0xFFFD; }
                byte b1 = ptr[1]; byte b2 = ptr[2];
                if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80) { bytesRead = 0; return 0xFFFD; }
                bytesRead = 3; uint cp = ((uint)(b0 & 0x0F) << 12) | ((uint)(b1 & 0x3F) << 6) | (uint)(b2 & 0x3F);
                return (cp >= 0x800 && (cp < 0xD800 || cp > 0xDFFF)) ? cp : 0xFFFD;
            }
            if ((b0 & 0xF8) == 0xF0)
            {
                if (maxBytesToRead < 4) { bytesRead = 0; return 0xFFFD; }
                byte b1 = ptr[1]; byte b2 = ptr[2]; byte b3 = ptr[3];
                if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80) { bytesRead = 0; return 0xFFFD; }
                bytesRead = 4; uint cp = ((uint)(b0 & 0x07) << 18) | ((uint)(b1 & 0x3F) << 12) | ((uint)(b2 & 0x3F) << 6) | (uint)(b3 & 0x3F);
                return (cp >= 0x10000 && cp < 0x110000) ? cp : 0xFFFD;
            }
            bytesRead = 0; return 0xFFFD;
        }
    }
}