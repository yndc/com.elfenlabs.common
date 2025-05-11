using System;
using Elfenlabs.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Elfenlabs.Geometry
{
    /// <summary>
    /// Container that contains multiple atlases, automatically creates a new atlas when the current one is full.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct DynamicAtlasList : IDisposable, INativeDisposable
    {
        UnsafeList<DynamicAtlas> atlases;

        DynamicAtlas.Config config;

        Allocator allocator;

        public DynamicAtlasList(DynamicAtlas.Config config, Allocator allocator)
        {
            this.config = config;
            this.allocator = allocator;
            atlases = new UnsafeList<DynamicAtlas>(1, allocator)
            {
                new(config, allocator)
            };
        }

        public readonly int AtlasCount => atlases.Length;

        public ref DynamicAtlas Last => ref atlases.ElementAt(atlases.Length - 1);

        public readonly DynamicAtlas.Config Config => config;

        public ref DynamicAtlas this[int index] => ref atlases.ElementAt(index);

        public int2 AddItem(int2 size, out int atlasIndex)
        {
            atlasIndex = atlases.Length - 1;
            ref var currentAtlas = ref atlases.ElementAt(atlasIndex);
            var position = currentAtlas.AddItem(size);
            if (position.x == -1)
            {
                var newAtlas = new DynamicAtlas(config, allocator);
                atlases.Add(newAtlas);
                atlasIndex++;
                position = newAtlas.AddItem(size);
                if (position.x == -1)
                {
                    atlasIndex = -1;
                    return position;
                }
            }
            return position;
        }

        /// <summary>
        /// Adds items to the list of atlases until all inputs are added. 
        /// New atlas will be automatically created.
        /// Outputs the number of items that were placed in each atlas, starting from the last atlas before this call.
        /// </summary>
        /// <param name="sizes"></param>
        /// <param name="positions"></param>
        /// <param name="indices"></param>
        public void AddItems(
            NativeArray<int2> sizes,
            ref NativeArray<int2> positions,
            out NativeList<int> atlasPackedCounts,
            Allocator allocator)
        {
            atlasPackedCounts = new NativeList<int>(1, allocator);
            ref var currentAtlas = ref atlases.ElementAt(atlases.Length - 1);
            var startIndex = 0;
            do
            {
                var sizeSlice = sizes.Slice(startIndex);
                var positionSlice = positions.Slice(startIndex);
                var packed = currentAtlas.AddItems(sizeSlice, ref positionSlice);

                // If the atlas is empty and we couldn't pack anything, it means the item is too large for the atlas.
                // We need to throw an exception here to avoid infinite loop.
                if (packed == 0 && currentAtlas.IsEmpty)
                {
                    var atlasSize = Config.Size;
                    var itemSize = sizes[startIndex];
                    throw new InvalidOperationException(
                        $"The item is too large for the atlas: item size {itemSize.x}x{itemSize.y}, atlas size {atlasSize}x{atlasSize}");
                }

                if (packed < sizeSlice.Length)
                {
                    atlases.Add(new DynamicAtlas(config, allocator));
                    currentAtlas = ref atlases.ElementAt(atlases.Length - 1);
                }

                atlasPackedCounts.Add(packed);
                startIndex += packed;
            }
            while (startIndex < sizes.Length);
        }

        /// <summary>
        /// Adds items to the last atlas until all inputs are added or if the atlas is full. 
        /// If the atlas is full a new atlas will be added. Call this function again with a new slice. Returns the number of items that were placed.
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="sizes"></param>
        /// <param name="positions"></param>
        /// <param name="indices"></param>
        /// <returns></returns>
        public int AddItemsToLastAtlas(int startIndex, in NativeArray<int2> sizes, ref NativeArray<int2> positions, ref NativeArray<int> indices, out bool isNewAtlasCreated)
        {
            isNewAtlasCreated = false;
            ref var currentAtlas = ref Last;
            var sizeSlice = sizes.Slice(startIndex);
            var positionSlice = positions.Slice(startIndex);
            var indicesSlice = indices.Slice(startIndex);
            var placed = currentAtlas.AddItems(sizeSlice, ref positionSlice);
            for (int i = 0; i < placed; i++)
            {
                indicesSlice[i] = atlases.Length - 1;
            }
            if (placed < sizeSlice.Length)
            {
                atlases.Add(new DynamicAtlas(config, allocator));
                isNewAtlasCreated = true;
            }
            return placed;
        }

        public int AddItemsToLastAtlas(NativeSlice<int2> sizes, NativeSlice<int2> positions, out bool isNewAtlasCreated)
        {
            isNewAtlasCreated = false;
            ref var currentAtlas = ref Last;
            var placed = currentAtlas.AddItems(sizes, ref positions);
            if (placed < sizes.Length)
            {
                atlases.Add(new DynamicAtlas(config, allocator));
                isNewAtlasCreated = true;
            }
            return placed;
        }

        public int GetLastAtlasIndex()
        {
            return atlases.Length - 1;
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

        public struct ItemLocation
        {
            public int AtlasIndex;
            public int2 Position;
            public int2 Size;
            public float4 GetAtlasUV(int atlasSize)
            {
                return new float4(
                        (float)Position.x / atlasSize,
                        (float)Position.y / atlasSize,
                        (float)Size.x / atlasSize,
                        (float)Size.y / atlasSize
                );
            }
        }

        public struct Blob : IBlobField<DynamicAtlasList>
        {
            private BlobArray<DynamicAtlas.Blob> Atlases;
            private DynamicAtlas.Config Config;

            public void Serialize(BlobBuilder builder, DynamicAtlasList atlasList)
            {
                Config = atlasList.config;

                var atlasesBuilder = builder.Allocate(ref Atlases, atlasList.atlases.Length);
                for (int i = 0; i < atlasesBuilder.Length; i++)
                {
                    atlasesBuilder[i].Serialize(builder, atlasList.atlases[i]);
                }
            }

            public DynamicAtlasList Deserialize(Allocator allocator)
            {
                var atlasList = new DynamicAtlasList(Config, allocator);
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