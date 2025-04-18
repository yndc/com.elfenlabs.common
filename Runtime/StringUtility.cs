using System;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Elfenlabs.String
{
    public unsafe struct UTF8CharRef
    {
        public byte* Value;
        public UTF8CharRef(ref byte character)
        {
            Value = (byte*)UnsafeUtility.AddressOf(ref character);
        }

        public byte AsByte()
        {
            return Value[0];
        }

        public char AsChar()
        {
            return (char)Value[0];
        }

        public int AsInt()
        {
            return Value[0];
        }
    }

    public static class StringUtility
    {
        public static NativeArray<byte> CreateByteArray(string str, Allocator allocator = Allocator.Temp)
        {
            if (str == null)
            {
                return new NativeArray<byte>(0, allocator);
            }

            var utf8ByteCount = Encoding.UTF8.GetByteCount(str);
            var nativeArray = new NativeArray<byte>(utf8ByteCount, allocator, NativeArrayOptions.UninitializedMemory);

            if (utf8ByteCount == 0)
            {
                return nativeArray;
            }

            unsafe
            {
                var bufferPtr = (byte*)nativeArray.GetUnsafePtr();
                fixed (char* stringPtr = str)
                {
                    var bytesWritten = Encoding.UTF8.GetBytes(stringPtr, str.Length, bufferPtr, utf8ByteCount);
                }
                return nativeArray;
            }
        }

        public static void CopyToNativeArray(string src, NativeArray<byte> dst)
        {
            var utf8ByteCount = Encoding.UTF8.GetByteCount(src);
            if (utf8ByteCount > dst.Length)
            {
                throw new ArgumentException($"Destination array is not large enough to hold the string. Required: {utf8ByteCount}, Available: {dst.Length}");
            }

            unsafe
            {
                fixed (char* stringPtr = src)
                {
                    var bytesWritten = Encoding.UTF8.GetBytes(stringPtr, src.Length, (byte*)dst.GetUnsafePtr(), utf8ByteCount);
                }
            }
        }

        public static void CopyToDynamicBuffer<T>(string src, DynamicBuffer<T> dst, int offset = 0) where T : unmanaged, IBufferElementData
        {
            var utf8ByteCount = Encoding.UTF8.GetByteCount(src);
            dst.ResizeUninitialized(offset + utf8ByteCount);
            unsafe
            {
                fixed (char* stringPtr = src)
                {
                    var bytesWritten = Encoding.UTF8.GetBytes(stringPtr, src.Length, (byte*)dst.GetUnsafePtr() + offset, utf8ByteCount);
                }
            }
        }

        public static bool IsBreakOpportunity(UTF8CharRef charRef)
        {
            return char.IsWhiteSpace(charRef.AsChar()) || charRef.AsByte() == '\n' || charRef.AsByte() == '\r' || charRef.AsByte() == '\t' || charRef.AsChar() == 0x2028 || charRef.AsChar() == 0x2029;
        }

        public static bool IsBreakOpportunity(byte b)
        {
            return char.IsWhiteSpace((char)b) || b == '\n' || b == '\r' || b == '\t';
        }

        public static bool IsNewLine(UTF8CharRef charRef)
        {
            return charRef.AsByte() == '\n' || charRef.AsByte() == '\r' || charRef.AsChar() == 0x2028 || charRef.AsChar() == 0x2029;
        }

        public static bool IsNewLine(byte b)
        {
            return b == '\n' || b == '\r';
        }
    }
}