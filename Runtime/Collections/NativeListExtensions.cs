using Unity.Collections;
using Unity.Burst;
using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Elfenlabs.Collections
{
    /// <summary>
    /// Provides extension methods for Unity.Collections.NativeList<T>.
    /// </summary>
    [BurstCompile]
    public static class NativeListExtensions
    {
        /// <summary>
        /// Inserts an element into the NativeList at the specified index.
        /// NOTE: This is a workaround as NativeList doesn't have a built-in Insert.
        /// </summary>
        /// <typeparam name="T">The type of element, must be unmanaged.</typeparam>
        /// <param name="list">The list to insert into.</param>
        /// <param name="index">The zero-based index at which item should be inserted.</param>
        /// <param name="item">The object to insert.</param>
        [BurstCompile]
        public static void InsertAt<T>(this ref Unity.Collections.NativeList<T> list, int index, T item) where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            var oldCount = list.Length;

            // Grow first – this may realloc the buffer but gives us the new pointer.
            list.ResizeUninitialized(oldCount + 1);

            var bytesToMove = (oldCount - index) * size;   // elements that have to slide
            unsafe
            {
                if (bytesToMove > 0)
                {
                    byte* basePtr = (byte*)list.GetUnsafePtr();
                    byte* src = basePtr + index * size;
                    byte* dest = src + size;
                    UnsafeUtility.MemMove(dest, src, bytesToMove);
                }
                UnsafeUtility.WriteArrayElement(list.GetUnsafePtr(), index, item);
            }
        }

        public static NativeSlice<T> Slice<T>(this ref Unity.Collections.NativeList<T> list, int start, int length = -1)
            where T : unmanaged
        {
            if (length == -1)
            {
                length = list.Length - start;
            }
            if (start < 0 || start >= list.Length
                || length < 0 || start + length > list.Length)
            {
                throw new ArgumentOutOfRangeException($"Invalid slice parameters: start={start}, length={length}");
            }
            unsafe
            {
                var ptr = list.GetUnsafePtr() + start;
                var slice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<T>(
                    ptr, UnsafeUtility.SizeOf<T>(), length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeSliceUnsafeUtility.SetAtomicSafetyHandle(
                    ref slice,
                    NativeListUnsafeUtility.GetAtomicSafetyHandle(ref list));
#endif
                return slice;
            }
        }
    }
}