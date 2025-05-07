using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Collections;

namespace Elfenlabs.Collections
{
    /// <summary>
    /// Provides extension methods for Unity.Collections.NativeList<T>.
    /// </summary>
    [BurstCompile]
    public static class UnsafeListExtensions
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
        public static void InsertAt<T>(this ref UnsafeList<T> list, int index, T item) where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            var oldCount = list.Length;

            // Grow first – this may realloc the buffer but gives us the new pointer.
            list.Resize(oldCount + 1);

            var bytesToMove = (oldCount - index) * size;   // elements that have to slide
            unsafe
            {
                if (bytesToMove > 0)
                {
                    byte* basePtr = (byte*)list.Ptr;
                    byte* src = basePtr + index * size;
                    byte* dest = src + size;
                    UnsafeUtility.MemMove(dest, src, bytesToMove);
                }
                UnsafeUtility.WriteArrayElement(list.Ptr, index, item);
            }
        }

        public static void AddRange<T>(this ref UnsafeList<T> list, NativeSlice<T> values) where T : unmanaged
        {
            var index = list.Length;

            list.Resize(list.Length + values.Length, NativeArrayOptions.UninitializedMemory);

            unsafe
            {
                var sizeOf = UnsafeUtility.SizeOf<T>();
                void* dst = (byte*)list.Ptr + index * sizeOf;
                UnsafeUtility.MemCpy(dst, values.GetUnsafePtr(), values.Length * sizeOf);
            }
        }
    }
}