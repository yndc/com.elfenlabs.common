using System.Runtime.CompilerServices;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Elfenlabs.String
{
    public static class UnicodeUtility
    {
        public static bool IsASCII(ref byte value)
        {
            return (value & 0x80) == 0;
        }

        public static bool Is2ByteSequence(ref byte value)
        {
            return (value & 0xE0) == 0xC0;
        }

        public static bool Is3ByteSequence(ref byte value)
        {
            return (value & 0xF0) == 0xE0;
        }

        public static bool Is4ByteSequence(ref byte value)
        {
            return (value & 0xF8) == 0xF0;
        }

        public static unsafe byte GetASCII(ref byte value)
        {
            var ptr = (byte*)UnsafeUtility.AddressOf(ref value);
            return ptr[0];
        }

        public static unsafe uint GetUnicodeFrom2ByteSequence(ref byte value)
        {
            var ptr = (byte*)UnsafeUtility.AddressOf(ref value);
            byte b0 = ptr[0];
            byte b1 = ptr[1];
            if ((b1 & 0xC0) != 0x80)
            {
                return 0xFFFD;
            }
            return ((uint)(b0 & 0x1F) << 6) | (uint)(b1 & 0x3F);
        }

        public static unsafe uint GetUnicodeFrom3ByteSequence(ref byte value)
        {
            var ptr = (byte*)UnsafeUtility.AddressOf(ref value);
            byte b0 = ptr[0];
            byte b1 = ptr[1];
            byte b2 = ptr[2];
            if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80)
            {
                return 0xFFFD;
            }
            return ((uint)(b0 & 0x0F) << 12) | ((uint)(b1 & 0x3F) << 6) | (uint)(b2 & 0x3F);
        }

        public static unsafe uint GetUnicodeFrom4ByteSequence(ref byte value)
        {
            var ptr = (byte*)UnsafeUtility.AddressOf(ref value);
            byte b0 = ptr[0];
            byte b1 = ptr[1];
            byte b2 = ptr[2];
            byte b3 = ptr[3];
            if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80)
            {
                return 0xFFFD;
            }
            return ((uint)(b0 & 0x07) << 18) | ((uint)(b1 & 0x3F) << 12) | ((uint)(b2 & 0x3F) << 6) | (uint)(b3 & 0x3F);
        }

        public static bool IsCJK(ref byte value)
        {
            // If the first bit is 0, it's a single-byte ASCII character (U+0000 to U+007F)
            // None of these are CJK characters.
            if (IsASCII(ref value))
            {
                return false;
            }

            // 2-byte sequences are in the range U+0080 to U+07FF
            // These are not CJK characters either.
            if (Is2ByteSequence(ref value))
            {
                return false;
            }

            if (Is3ByteSequence(ref value))
            {
                var codePoint = GetUnicodeFrom3ByteSequence(ref value);

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

            if (Is4ByteSequence(ref value))
            {
                var codePoint = GetUnicodeFrom4ByteSequence(ref value);

                // CJK Unified Ideographs Extension B (Rarely used but still CJK)
                if (codePoint >= 0x20000 && codePoint <= 0x2A6DF) return true;
            }

            return false;
        }

        private static unsafe uint UTF8ToUnicode(byte* ptr, int maxBytesToRead, out int bytesRead)
        {
            // Caller should have already checked maxBytesToRead > 0 and ptr != null
            byte b0 = ptr[0];
            // bytesRead = 1; // Start assuming 1, adjusted below

            // Check for invalid start byte patterns (continuation or too many 1s)
            // ASCII case already handled by caller.
            if ((b0 & 0xC0) == 0x80 || (b0 & 0xFE) == 0xFE)
            {
                bytesRead = 0; return 0xFFFD;
            }

            // 2-byte sequence
            if ((b0 & 0xE0) == 0xC0) // Starts with 110xxxxx
            {
                if (maxBytesToRead < 2) { bytesRead = 0; return 0xFFFD; }
                byte b1 = ptr[1];
                if ((b1 & 0xC0) != 0x80) { bytesRead = 0; return 0xFFFD; } // Invalid continuation byte
                bytesRead = 2;
                uint cp = ((uint)(b0 & 0x1F) << 6) | (uint)(b1 & 0x3F);
                // Check for overlong encoding (should be >= 0x80)
                return cp >= 0x80 ? cp : 0xFFFD;
            }

            // 3-byte sequence
            if ((b0 & 0xF0) == 0xE0) // Starts with 1110xxxx
            {
                if (maxBytesToRead < 3) { bytesRead = 0; return 0xFFFD; }
                byte b1 = ptr[1];
                byte b2 = ptr[2];
                if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80) { bytesRead = 0; return 0xFFFD; } // Invalid continuation
                bytesRead = 3;
                uint cp = ((uint)(b0 & 0x0F) << 12) | ((uint)(b1 & 0x3F) << 6) | (uint)(b2 & 0x3F);
                // Check for overlong encoding (should be >= 0x800) and surrogates (invalid in UTF-8)
                return (cp >= 0x800 && (cp < 0xD800 || cp > 0xDFFF)) ? cp : 0xFFFD;
            }

            // 4-byte sequence
            if ((b0 & 0xF8) == 0xF0) // Starts with 11110xxx
            {
                if (maxBytesToRead < 4) { bytesRead = 0; return 0xFFFD; }
                byte b1 = ptr[1];
                byte b2 = ptr[2];
                byte b3 = ptr[3];
                if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80) { bytesRead = 0; return 0xFFFD; } // Invalid continuation
                bytesRead = 4;
                uint cp = ((uint)(b0 & 0x07) << 18) | ((uint)(b1 & 0x3F) << 12) | ((uint)(b2 & 0x3F) << 6) | (uint)(b3 & 0x3F);
                // Check for overlong encoding (should be >= 0x10000) and code point validity (< 0x110000)
                return (cp >= 0x10000 && cp < 0x110000) ? cp : 0xFFFD;
            }

            // Invalid starting byte if we reach here (shouldn't happen if initial check is correct)
            bytesRead = 0;
            return 0xFFFD;
        }

        public static bool IsBreakOpportunity(UTF8CharRef charRef)
        {
            return char.IsWhiteSpace(charRef.AsChar()) || charRef.AsByte() == '\n' || charRef.AsByte() == '\r' || charRef.AsByte() == '\t' || charRef.AsChar() == 0x2028 || charRef.AsChar() == 0x2029;
        }

        public static bool IsBreakOpportunity(ref byte b)
        {
            if (char.IsWhiteSpace((char)b)) return true;
            if (b == '\n') return true;
            if (b == '\r') return true;
            if (b == '\t') return true;
            if (IsCJK(ref b)) return true;
            return false;
        }

        public static bool IsNewLine(UTF8CharRef charRef)
        {
            return charRef.AsByte() == '\n' || charRef.AsByte() == '\r' || charRef.AsChar() == 0x2028 || charRef.AsChar() == 0x2029;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNewLine(uint codePoint)
        {
            return codePoint == '\n' || codePoint == '\r' || // LF, CR
                   codePoint == 0x2028 || // Line Separator
                   codePoint == 0x2029;   // Paragraph Separator
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWhiteSpace(uint codePoint)
        {
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCJK(uint codePoint)
        {
            if (codePoint <= 0x007F) return false; // ASCII

            // Reordered CJK checks
            if (codePoint >= 0xFF00 && codePoint <= 0xFFEF) return true; // Halfwidth/Fullwidth Forms
            if (codePoint >= 0x3000 && codePoint <= 0x303F) return true; // CJK Symbols and Punctuation
            if (codePoint >= 0x3040 && codePoint <= 0x309F) return true; // Hiragana
            if (codePoint >= 0x30A0 && codePoint <= 0x30FF) return true; // Katakana
            if (codePoint >= 0x31F0 && codePoint <= 0x31FF) return true; // Katakana Phonetic Ext
            if (codePoint >= 0xAC00 && codePoint <= 0xD7AF) return true; // Hangul Syllables
            if (codePoint >= 0x4E00 && codePoint <= 0x9FFF) return true; // CJK Unified Ideographs
            if (codePoint >= 0x3400 && codePoint <= 0x4DBF) return true; // CJK Unified Ideographs Ext A
            if (codePoint >= 0x1100 && codePoint <= 0x11FF) return true; // Hangul Jamo
            if (codePoint >= 0x3130 && codePoint <= 0x318F) return true; // Hangul Compatibility Jamo
            if (codePoint >= 0x3100 && codePoint <= 0x312F) return true; // Bopomofo
            if (codePoint >= 0x31A0 && codePoint <= 0x31BF) return true; // Bopomofo Ext
            if (codePoint >= 0xF900 && codePoint <= 0xFAFF) return true; // CJK Compatibility Ideographs
            if (codePoint >= 0x31C0 && codePoint <= 0x31EF) return true; // CJK Strokes
            if (codePoint >= 0x3200 && codePoint <= 0x32FF) return true; // Enclosed CJK Letters and Months
            if (codePoint >= 0x3300 && codePoint <= 0x33FF) return true; // CJK Compatibility
            if (codePoint >= 0x20000 && codePoint <= 0x2A6DF) return true; // CJK Unified Ideographs Ext B (optional)

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBreakOpportunity(uint codePoint)
        {
            // A CJK character itself is a break opportunity *before* it.
            // Whitespace/newline are opportunities *after* them.
            // This logic might need refinement based on exact breaking rules.
            // For now, if it's any of these, consider it an opportunity.
            return IsNewLine(codePoint) || IsWhiteSpace(codePoint) || IsCJK(codePoint);
        }
    }
}