using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Elfenlabs.Collections
{

    public static class BlobExtensions
    {
        public static NativeBuffer<T> AsNativeBuffer<T>(this ref BlobArray<T> blobRef) where T : unmanaged
        {
            unsafe
            {
                return new NativeBuffer<T>(
                    (IntPtr)blobRef.GetUnsafePtr(),
                    Allocator.Invalid,
                    blobRef.Length * UnsafeUtility.SizeOf<T>());
            }
        }
    }
}