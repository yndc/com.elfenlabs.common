using System;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Elfenlabs.Collections
{
    public static class NativeBufferExtensions
    {
        public static NativeBuffer<U> ReinterpretCast<T, U>(this NativeBuffer<T> buffer)
            where T : unmanaged
            where U : unmanaged
        {
            unsafe
            {
                if (UnsafeUtility.SizeOf<T>() != UnsafeUtility.SizeOf<U>())
                {
                    throw new InvalidOperationException($"Cannot reinterpret cast from {typeof(T)} to {typeof(U)}. Size mismatch.");
                }
                return *(NativeBuffer<U>*)&buffer;
            }
        }

        public static NativeSlice<T> Slice<T>(this NativeBuffer<T> buffer, int start, int length)
            where T : unmanaged
        {
            if (length == -1)
            {
                length = buffer.Count() - start;
            }
            if (start < 0 || start >= buffer.Count()
                || length < 0 || start + length > buffer.Count())
            {
                throw new ArgumentOutOfRangeException($"Invalid slice parameters: start={start}, length={length}");
            }
            unsafe
            {
                var ptr = buffer.GetUnsafePtr() + start;
                var slice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<T>(
                    ptr, UnsafeUtility.SizeOf<T>(), length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref slice, AtomicSafetyHandle.Create());
#endif
                return slice;
            }
        }

        public static void CopyToBlobArray<T>(this NativeBuffer<T> buffer, BlobBuilder builder, ref BlobArray<T> blobArray)
            where T : unmanaged
        {
            unsafe
            {
                var arrayBuilder = builder.Allocate(ref blobArray, buffer.Count());
                UnsafeUtility.MemCpy(
                    arrayBuilder.GetUnsafePtr(),
                    buffer.GetUnsafePtr(),
                    buffer.Count() * UnsafeUtility.SizeOf<T>());
            }
        }

        public static NativeBuffer<T> AsNativeBuffer<T>(this DynamicBuffer<T> buffer)
            where T : unmanaged
        {
            unsafe
            {
                return new NativeBuffer<T>(
                    (IntPtr)buffer.GetUnsafeReadOnlyPtr(),
                    Allocator.Invalid,
                    buffer.Length * UnsafeUtility.SizeOf<T>());
            }
        }

        public static NativeBuffer<T> AsNativeBuffer<T>(this NativeArray<T> array)
            where T : unmanaged
        {
            unsafe
            {
                return new NativeBuffer<T>(
                    (IntPtr)array.GetUnsafeReadOnlyPtr(),
                    Allocator.Invalid,
                    array.Length * UnsafeUtility.SizeOf<T>());
            }
        }

        public static NativeBuffer<T> AsNativeBuffer<T>(this NativeSlice<T> slice)
    where T : unmanaged
        {
            unsafe
            {
                return new NativeBuffer<T>(
                    (IntPtr)slice.GetUnsafeReadOnlyPtr(),
                    Allocator.Invalid,
                    slice.Length * UnsafeUtility.SizeOf<T>());
            }
        }
    }
}