using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Elfenlabs.Mathematics
{
    public static class DataUtility
    {
        public static NativeArray<int4> Combine(NativeArray<int2> a, NativeArray<int2> b, Allocator allocator)
        {
            if (a.Length != b.Length)
            {
                throw new ArgumentException("Arrays must be of the same length.");
            }
            var result = new NativeArray<int4>(a.Length, allocator);
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = new int4(a[i], b[i]);
            }
            return result;
        }

        public static NativeArray<int2> ExtractXY(NativeArray<int4> input, Allocator allocator)
        {
            var result = new NativeArray<int2>(input.Length, allocator);
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = input[i].xy;
            }
            return result;
        }

        public static NativeArray<int2> ExtractZW(NativeArray<int4> input, Allocator allocator)
        {
            var result = new NativeArray<int2>(input.Length, allocator);
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = input[i].zw;
            }
            return result;
        }
    }
}