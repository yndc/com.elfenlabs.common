using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Elfenlabs.Collections;

namespace Elfenlabs.Entities
{
    public struct Repository<TKey, TValue> : IComponentData, IEnumerable<KeyValue<TKey, TValue>>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged, IEquatable<TValue>
    {
        [NativeDisableContainerSafetyRestriction]
        UnsafeParallelHashMap<TKey, TValue> data;

        UnsafeParallelHashSet<TKey> keySet;

        UnsafeHashSet<TKey> tester;

        public Repository(int capacity, Allocator allocator)
        {
            data = new UnsafeParallelHashMap<TKey, TValue>(capacity, allocator);
            keySet = new UnsafeParallelHashSet<TKey>(capacity, allocator);
            tester = new UnsafeHashSet<TKey>(capacity, allocator);
        }

        public void Add(TKey key, TValue value)
        {
            data.Add(key, value);
            keySet.Add(key);
        }

        public TValue Remove(TKey key)
        {
            var value = data[key];
            data.Remove(key);
            keySet.Remove(key);
            return value;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (data.TryGetValue(key, out value))
            {
                return true;
            }
            return false;
        }

        public TValue this[TKey assetRef]
        {
            get => data[assetRef];
        }

        public NativeArray<TKey> GetInitializationKeys(NativeHashSet<TKey> entityKeySet, Allocator allocator)
        {
            tester.Clear();
            tester.UnionWith(entityKeySet);
            tester.ExceptWith(keySet);
            return tester.ToNativeArray(allocator);
        }

        public NativeArray<TKey> GetCleanupKeys(NativeHashSet<TKey> entityKeySet, Allocator allocator)
        {
            tester.Clear();
            tester.UnionWith(keySet);
            tester.ExceptWith(entityKeySet);
            return tester.ToNativeArray(allocator);
        }

        public NativeArray<TKey> GetInitializationKeys(NativeList<TKey> entityKeySet, Allocator allocator)
        {
            tester.Clear();
            tester.UnionWith(entityKeySet);
            tester.ExceptWith(keySet);
            return tester.ToNativeArray(allocator);
        }

        public NativeArray<TKey> GetCleanupKeys(NativeList<TKey> entityKeySet, Allocator allocator)
        {
            tester.Clear();
            tester.UnionWith(keySet);
            tester.ExceptWith(entityKeySet);
            return tester.ToNativeArray(allocator);
        }

        public NativeArray<TKey> GetRequireInitializationKeys(NativeSlice<TKey> entityKeySet, Allocator allocator)
        {
            tester.Clear();
            tester.UnionWith(entityKeySet);
            tester.ExceptWith(keySet);
            return tester.ToNativeArray(allocator);
        }

        public NativeArray<TKey> GetRequireCleanupKeys(NativeSlice<TKey> entityKeySet, Allocator allocator)
        {
            tester.Clear();
            tester.UnionWith(keySet);
            tester.ExceptWith(entityKeySet);
            return tester.ToNativeArray(allocator);
        }

        IEnumerator<KeyValue<TKey, TValue>> IEnumerable<KeyValue<TKey, TValue>>.GetEnumerator()
        {
            return data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return data.GetEnumerator();
        }
    }
}