using System;
using Unity.Collections;
using Unity.Jobs;

namespace Elfenlabs.Collections
{
    public struct NativeHashCounter<T> : IDisposable, INativeDisposable where T : unmanaged, IEquatable<T>
    {
        NativeHashMap<T, int> map;

        public NativeHashCounter(int capacity, Allocator allocator)
        {
            map = new NativeHashMap<T, int>(capacity, allocator);
        }

        /// <summary>
        /// Increments the count of the specified key by the specified count.
        /// If the key does not exist, it is added with the specified count.
        /// Returns the new count of the key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public int Increment(T key, int count = 1)
        {
            if (map.TryGetValue(key, out var value))
            {
                count += value;
                map[key] = count;
            }
            else
            {
                map[key] = count;
            }
            return count;
        }

        /// <summary>
        /// Decrements the count of the specified key by the specified count.
        /// If the count reaches zero or below, the key is removed from the map.
        /// Returns the new count of the key, or zero if it was removed.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public int Decrement(T key, int count = 1)
        {
            if (map.TryGetValue(key, out var value))
            {
                value -= count;
                if (value > 0)
                {
                    map[key] = value;
                    return value;
                }
                map.Remove(key);
            }
            return 0;
        }

        public int this[T key]
        {
            get => map.TryGetValue(key, out var value) ? value : 0;
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            return map.Dispose(inputDeps);
        }

        public void Dispose()
        {
            map.Dispose();
        }
    }
}