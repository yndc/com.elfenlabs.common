using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Elfenlabs.Collections
{
    /// <summary>
    /// Small utility structure holding a flattened hash map that can be included in a blob asset.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="T"></typeparam>
    public struct BlobFlattenedHashMap<K, T>
    where T : unmanaged
    where K : unmanaged, IEquatable<K>
    {
        public BlobArray<K> Keys;
        public BlobArray<T> Values;
        public void Flatten(BlobBuilder builder, UnsafeHashMap<K, T> map)
        {
            BlobBuilderArray<K> keys = builder.Allocate(ref Keys, map.Count);
            BlobBuilderArray<T> values = builder.Allocate(ref Values, map.Count);

            int i = 0;
            foreach (var kvp in map)
            {
                keys[i] = kvp.Key;
                values[i] = kvp.Value;
                i++;
            }
        }

        public UnsafeParallelHashMap<K, T> Reconstruct(Allocator allocator)
        {
            var map = new UnsafeParallelHashMap<K, T>(Keys.Length, allocator);
            for (int i = 0; i < Keys.Length; i++)
            {
                map.Add(Keys[i], Values[i]);
            }
            return map;
        }
    }

    public struct BlobSerializedUnsafeParallelHashMap<K, T> : IBlobField<UnsafeParallelHashMap<K, T>>
        where T : unmanaged
        where K : unmanaged, IEquatable<K>
    {
        public BlobArray<K> Keys;
        public BlobArray<T> Values;

        public UnsafeParallelHashMap<K, T> Deserialize(Allocator allocator)
        {
            var map = new UnsafeParallelHashMap<K, T>(Keys.Length, allocator);
            for (int i = 0; i < Keys.Length; i++)
            {
                map.Add(Keys[i], Values[i]);
            }
            return map;
        }

        public void Serialize(BlobBuilder builder, UnsafeParallelHashMap<K, T> value)
        {
            BlobBuilderArray<K> keys = builder.Allocate(ref Keys, value.Count());
            BlobBuilderArray<T> values = builder.Allocate(ref Values, value.Count());

            int i = 0;
            foreach (var kvp in value)
            {
                keys[i] = kvp.Key;
                values[i] = kvp.Value;
                i++;
            }
        }
    }
}