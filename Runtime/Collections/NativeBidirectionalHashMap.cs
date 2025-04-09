using System;
using Unity.Collections;
using Unity.Jobs;

namespace Elfenlabs.Collections
{
    public struct NativeBidirectionalHashMap<K, V> : IDisposable, INativeDisposable
        where K : unmanaged, IEquatable<K>
        where V : unmanaged, IEquatable<V>
    {
        NativeHashMap<K, V> map;
        NativeHashMap<V, K> reverseMap;

        public NativeBidirectionalHashMap(int capacity, Allocator allocator)
        {
            map = new NativeHashMap<K, V>(capacity, allocator);
            reverseMap = new NativeHashMap<V, K>(capacity, allocator);
        }

        public readonly int Count => map.Count;

        public bool TryGetValue(K key, out V value) => map.TryGetValue(key, out value);

        public bool TryGetKey(V value, out K key) => reverseMap.TryGetValue(value, out key);

        public void Add(K key, V value)
        {
            map.Add(key, value);
            reverseMap.Add(value, key);
        }

        public void Remove(K key)
        {
            if (map.TryGetValue(key, out var value))
            {
                map.Remove(key);
                reverseMap.Remove(value);
            }
        }

        public void Remove(V value)
        {
            if (reverseMap.TryGetValue(value, out var key))
            {
                reverseMap.Remove(value);
                map.Remove(key);
            }
        }

        public void Dispose()
        {
            map.Dispose();
            reverseMap.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            return JobHandle.CombineDependencies(map.Dispose(inputDeps), reverseMap.Dispose(inputDeps));
        }
    }
}