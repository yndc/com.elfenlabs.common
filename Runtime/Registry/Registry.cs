using System;
using Unity.Collections;
using Unity.Jobs;

namespace Elfenlabs.Registry
{
    public struct Registry<T> : IDisposable, INativeDisposable where T : unmanaged, IEquatable<T>
    {
        NativeList<T> items;
        NativeHashMap<T, RuntimeIndex<T>> indexLookup;

        public Registry(Allocator allocator, int initialCapacity = 16)
        {
            items = new NativeList<T>(initialCapacity, allocator);
            indexLookup = new NativeHashMap<T, RuntimeIndex<T>>(initialCapacity, allocator);
        }

        public RuntimeIndex<T> Register(T item)
        {
            if (indexLookup.TryGetValue(item, out var index))
            {
                return index;
            }
            index = items.Length;
            items.Add(item);
            indexLookup.Add(item, index);
            return index;
        }

        public T Get(RuntimeIndex<T> index)
        {
            return items[index.Value];
        }

        public bool Has(RuntimeIndex<T> index)
        {
            return false;
        }

        public bool Has(T item)
        {
            return indexLookup.ContainsKey(item);
        }

        public void Dispose()
        {
            items.Dispose();
            indexLookup.Dispose();
        }

        public JobHandle Dispose(JobHandle jobHandle)
        {
            return JobHandle.CombineDependencies(
                items.Dispose(jobHandle),
                indexLookup.Dispose(jobHandle)
            );
        }
    }
}