using System;
using Elfenlabs.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Elfenlabs.Geometry
{
    public interface IAtlasListElement : IAtlasElement
    {
        public int Index { get; set; }
    }

    /// <summary>
    /// Container that contains multiple atlases, automatically creates a new atlas when the current one is full.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct DynamicAtlasList<T> : IDisposable, INativeDisposable where T : unmanaged, IAtlasElement, IAtlasListElement
    {
        UnsafeList<DynamicAtlas<T>> atlases;

        DynamicAtlas<T>.Config config;

        Allocator allocator;

        public DynamicAtlasList(DynamicAtlas<T>.Config config, Allocator allocator)
        {
            this.config = config;
            this.allocator = allocator;
            atlases = new UnsafeList<DynamicAtlas<T>>(1, allocator)
            {
                new(config, allocator)
            };
        }

        public void AddItems(NativeArray<T> items)
        {
            var currentAtlas = atlases[atlases.Length - 1];
            var startIndex = 0;
            do
            {
                var slice = items.Slice(startIndex);
                var placed = currentAtlas.AddItems(slice);
                startIndex += placed;
                if (placed < slice.Length)
                {
                    var newAtlas = new DynamicAtlas<T>(config, allocator);
                    currentAtlas = newAtlas;
                    atlases.Add(newAtlas);
                }
            }
            while (startIndex < items.Length);
        }

        public void Dispose()
        {
            for (int i = 0; i < atlases.Length; i++)
            {
                atlases[i].Dispose();
            }
            atlases.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            for (int i = 0; i < atlases.Length; i++)
            {
                inputDeps = atlases[i].Dispose(inputDeps);
            }
            return inputDeps;
        }

        public struct Blob : IBlobSerialized<DynamicAtlasList<T>>
        {
            private BlobArray<DynamicAtlas<T>.Blob> Atlases;
            private DynamicAtlas<T>.Config Config;

            public void Serialize(BlobBuilder builder, DynamicAtlasList<T> atlasList)
            {
                Config = atlasList.config;

                var atlasesBuilder = builder.Allocate(ref Atlases, atlasList.atlases.Length);
                for (int i = 0; i < atlasesBuilder.Length; i++)
                {
                    atlasesBuilder[i].Serialize(builder, atlasList.atlases[i]);
                }
            }

            public DynamicAtlasList<T> Deserialize(Allocator allocator)
            {
                var atlasList = new DynamicAtlasList<T>(Config, allocator);
                atlasList.atlases.Clear();
                for (int i = 0; i < Atlases.Length; i++)
                {
                    var atlas = Atlases[i].Deserialize(allocator);
                    atlasList.atlases.Add(atlas);
                }

                return atlasList;
            }
        }
    }
}