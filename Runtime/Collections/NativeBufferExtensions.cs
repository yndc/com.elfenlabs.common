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
    }
}