using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Elfenlabs.Collections
{
    public static class SliceExtensions
    {
        public unsafe static NativeSlice<T> CreateSlice<T>(
            void* srcPtr, int max, int start = 0, int length = -1, AtomicSafetyHandle safetyHandle = default)
            where T : unmanaged
        {
            if (length == -1)
            {
                length = max - start;
            }
            if (start < 0 || start >= max || length < 0 || start + length > max)
            {
                throw new ArgumentOutOfRangeException($"Invalid slice parameters: start={start}, length={length}");
            }
            unsafe
            {
                var ptr = (T*)srcPtr + start;
                var slice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<T>(
                    ptr, UnsafeUtility.SizeOf<T>(), length);
                NativeSliceUnsafeUtility.SetAtomicSafetyHandle(
                    ref slice,
                    safetyHandle.Equals(new AtomicSafetyHandle()) ? AtomicSafetyHandle.Create() : safetyHandle);
                return slice;
            }
        }

        public static NativeSlice<T> Slice<T>(this ref DynamicBuffer<T> buf, int start = 0, int length = -1)
            where T : unmanaged
        {
            unsafe
            {
                return CreateSlice<T>(
                    buf.GetUnsafePtr(),
                    buf.Length,
                    start,
                    length,
                    NativeArrayUnsafeUtility.GetAtomicSafetyHandle(
                        buf.AsNativeArray() // We need to get the safety handle somehow
                    ));
            }
        }
    }
}