using System;
using Elfenlabs.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityUnsafeUtility = Unity.Collections.LowLevel.Unsafe.UnsafeUtility;

namespace Elfenlabs.Unsafe
{
    public static class aUnsafeUtility
    {
        public static unsafe void CopyArrayToPtr<T>(T[] source, void* destination, int length) where T : unmanaged
        {
            if (source == null || destination == null || length <= 0)
                return;

            fixed (T* sourcePtr = source)
            {
                UnityUnsafeUtility.MemCpy(destination, sourcePtr, length * UnityUnsafeUtility.SizeOf<T>());
            }
        }
    }
}