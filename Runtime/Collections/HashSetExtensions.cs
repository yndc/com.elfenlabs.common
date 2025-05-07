using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Elfenlabs.Collections
{


    public static class HashSetExtensions
    {
        /// <summary>
        /// Removes the values from this set which are also present in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void ExceptWith<T>(this ref UnsafeHashSet<T> container, NativeSlice<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Remove(item);
            }
        }

        /// <summary>
        /// Removes the values from this set which are absent in another collection.
        /// </summary>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="container">The set to remove values from.</param>
        /// <param name="other">The collection to compare with.</param>
        public static void IntersectWith<T>(this ref UnsafeHashSet<T> container, NativeSlice<T> other)
            where T : unmanaged, IEquatable<T>
        {
            var result = new UnsafeList<T>(container.Count, Allocator.Temp);

            foreach (var item in other)
            {
                if (container.Contains(item))
                {
                    result.Add(item);
                }
            }

            container.Clear();
            container.UnionWith(result);

            result.Dispose();
        }

        public static void UnionWith<T>(this ref UnsafeHashSet<T> container, NativeSlice<T> other)
            where T : unmanaged, IEquatable<T>
        {
            foreach (var item in other)
            {
                container.Add(item);
            }
        }
    }
}