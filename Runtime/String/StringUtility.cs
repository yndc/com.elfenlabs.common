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

    public struct Range<T>
    {
        public int Start;
        public int Length;
        public T Identifier;
        public Range(int start, int length, T identifier = default)
        {
            Start = start;
            Length = length;
            Identifier = identifier;
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

        public static unsafe void CopyToNativeList(NativeList<uint> dst, string src, int offset = 0)
        {
            fixed (char* srcCharsPtr = src)
            {
                int byteCount = Encoding.UTF32.GetByteCount(srcCharsPtr, src.Length);
                int codePointCount = byteCount / 4;

                int requiredLength = offset + codePointCount;
                if (dst.Length < requiredLength)
                {
                    dst.ResizeUninitialized(requiredLength);
                }

                uint* dstUintPtr = dst.GetUnsafePtr() + offset;
                byte* dstBytePtr = (byte*)dstUintPtr;

                Encoding.UTF32.GetBytes(srcCharsPtr, src.Length, dstBytePtr, byteCount);
            }
        }

        public static unsafe void CopyToDynamicBuffer(DynamicBuffer<uint> dst, string src, int offset = 0)
        {
            fixed (char* srcCharsPtr = src)
            {
                int byteCount = Encoding.UTF32.GetByteCount(srcCharsPtr, src.Length);
                int codePointCount = byteCount / 4;

                int requiredLength = offset + codePointCount;
                if (dst.Length < requiredLength)
                {
                    dst.ResizeUninitialized(requiredLength);
                }

                uint* dstUintPtr = ((uint*)dst.GetUnsafePtr()) + offset;
                byte* dstBytePtr = (byte*)dstUintPtr;

                Encoding.UTF32.GetBytes(srcCharsPtr, src.Length, dstBytePtr, byteCount);
            }
        }
    }
}