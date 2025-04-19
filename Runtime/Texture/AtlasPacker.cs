using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Elfenlabs.Collections;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Serialization;

namespace Elfenlabs.Texture
{
    public interface IRectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; }
        public int Height { get; }
    }

    public struct AtlasPacker<T> : IDisposable, INativeDisposable where T : unmanaged, IRectangle
    {
        public struct Config
        {
            public int Size;        // Width and Height of the square atlas slice
            public int Margin;      // Minimum distance between packed rectangles and the atlas border.
            public int Flags;       // User-defined flags? (Not used directly in packer)
        }

        private struct SkylineNode
        {
            public int X;     // Left coordinate (inclusive)
            public int Y;     // Height (y-coordinate) of the segment's top edge (floor)
            public int Width; // Width
        }

        public struct Blob : IBlobSerialized<AtlasPacker<T>>
        {
            private Config Config;
            private BlobArray<SkylineNode> Skyline;

            public void Serialize(BlobBuilder builder, AtlasPacker<T> atlas)
            {
                Config = atlas.config;
                var skylineBuilder = builder.Allocate(ref Skyline, atlas.skyline.Length);
                for (int i = 0; i < skylineBuilder.Length; i++)
                {
                    skylineBuilder[i] = atlas.skyline[i];
                }
            }

            public AtlasPacker<T> Deserialize(Allocator allocator)
            {
                var packer = new AtlasPacker<T>(Config, allocator);
                packer.skyline.Clear();
                for (int i = 0; i < Skyline.Length; i++)
                {
                    packer.skyline.Add(Skyline[i]);
                }
                return packer;
            }

            // public static AtlasPacker<T> Deserialize(byte[] bytes, Allocator allocator)
            // {
            //     unsafe
            //     {
            //         fixed (byte* ptr = bytes)
            //         {
            //             var reader = new MemoryBinaryReader(ptr, bytes.Length);
            //             BlobAssetReference<Blob>.TryRead(reader, 0, out var blobAssetRef);
            //             var result = blobAssetRef.Value.Deserialize(allocator);
            //             blobAssetRef.Dispose();
            //             return result;
            //         }
            //     }
            // }

            // public static BlobAssetReference<Blob> CreateReference(in AtlasPacker<T> atlas, Allocator allocator = Allocator.Persistent)
            // {
            //     using var builder = new BlobBuilder(Allocator.Temp);
            //     ref var root = ref builder.ConstructRoot<Blob>();
            //     root.Serialize(builder, atlas);
            //     return builder.CreateBlobAssetReference<Blob>(allocator);
            // }

            // public static byte[] ToBytes(in AtlasPacker<T> atlas)
            // {
            //     using var builder = new BlobBuilder(Allocator.Temp);
            //     ref var root = ref builder.ConstructRoot<Blob>();
            //     root.Serialize(builder, atlas);
            //     var memoryWriter = new MemoryBinaryWriter();
            //     BlobAssetReference<Blob>.Write(memoryWriter, builder, 0);
            //     unsafe
            //     {
            //         var arr = new byte[memoryWriter.Length];
            //         fixed (byte* ptr = arr)
            //         {
            //             UnsafeUtility.MemCpy(ptr, memoryWriter.Data, memoryWriter.Length);
            //         }
            //         return arr;
            //     }
            // }
        }

        private Config config;

        private UnsafeList<SkylineNode> skyline;

        /// <summary>
        /// Creates and initializes a new packer for a single atlas slice.
        /// </summary>
        /// <param name="config">Configuration settings for the atlas dimensions and margin.</param>
        /// <param name="allocator">The allocator to use for the internal skyline list.</param>
        public AtlasPacker(Config config, Allocator allocator)
        {
            this.config = config;
            this.skyline = new UnsafeList<SkylineNode>(1, allocator);

            // Initialize the skyline respecting the margin.
            // If size <= 2*margin, skyline remains empty and packing will fail.
            if (config.Size > 2 * config.Margin)
            {
                skyline.Add(new SkylineNode
                {
                    X = config.Margin,
                    Y = config.Margin,
                    Width = config.Size - 2 * config.Margin
                });
            }
        }

        public readonly bool IsCreated => skyline.IsCreated;

        /// <summary>
        /// Disposes the internal NativeList holding the skyline data.
        /// Must be called when the packer is no longer needed.
        /// </summary>
        public void Dispose()
        {
            if (skyline.IsCreated)
            {
                skyline.Dispose();
            }
        }

        public JobHandle Dispose(JobHandle jobHandle)
        {
            if (skyline.IsCreated)
            {
                return skyline.Dispose(jobHandle);
            }
            return jobHandle;
        }

        /// <summary>
        /// Attempts to add multiple glyphs into the current atlas slice.
        /// Modifies the input glyphs array directly with atlas_x_px/atlas_y_px.
        /// Glyphs that couldn't fit will have atlas_x_px = -1.
        /// </summary>
        /// <param name="items">A NativeArray containing glyph metrics. Input dimensions are read,
        /// output coordinates (atlas_x_px, atlas_y_px) are written back.</param>
        /// <returns>The number of glyphs successfully placed into *this slice* during this call.</returns>
        public int PackGlyphs(NativeArray<T> items)
        {
            int placed_count = 0;

            // Cannot pack if skyline is empty (e.g., invalid config preventing initialization)
            if (!skyline.IsCreated)
            {
                return 0;
            }

            for (int i = 0; i < items.Length; ++i)
            {
                var item = items[i];

                // Calculate node dimensions including the margin for placement spacing
                int nodeWidth = item.Width + 2 * config.Margin;
                int nodeHeight = item.Height + 2 * config.Margin;

                if (nodeWidth <= config.Margin || nodeHeight <= config.Margin)
                    continue;

                if (FindPosition(nodeWidth, nodeHeight, out var bestX, out var bestY))
                {
                    PlaceNodeAndUpdateSkyline(bestX, bestY, nodeWidth, nodeHeight);

                    item.X = bestX;
                    item.Y = config.Size - (bestY + nodeHeight);

                    items[i] = item; // Write back modified struct

                    placed_count++;
                }
                else
                {
                    break;
                }
            }
            return placed_count;
        }

        /// <summary>
        /// Resets the packer state to an empty atlas (single skyline node at the top).
        /// </summary>
        public void Reset()
        {
            skyline.Clear();
            if (config.Size > 2 * config.Margin)
            {
                skyline.Add(new SkylineNode { X = config.Margin, Y = config.Margin, Width = config.Size - 2 * config.Margin });
            }
        }

        // --- Internal Helper Methods (Operate in Y-down coordinate system) ---

        /// <summary> Finds the best position (bottom-left heuristic) for a rectangle. </summary>
        /// <returns>True if a suitable position was found, false otherwise.</returns>
        private bool FindPosition(int nodeWidth, int nodeHeight, out int outX, out int outY)
        {
            // Cannot pack if skyline is empty (e.g., invalid config preventing initialization)
            // Check added in PackGlyphs, but double check doesn't hurt.
            if (!skyline.IsCreated || skyline.Length == 0)
            {
                outX = -1; outY = -1;
                return false;
            }

            int bestY = config.Size; // Initialize best Y to max height
            int bestX = -1;
            // int best_node_index = -1; // Index not strictly needed for result

            for (int i = 0; i < skyline.Length; ++i)
            {
                if (CanPlaceHorizontally(i, nodeWidth, out int currentY))
                {
                    // Check vertical fit (top_y + height <= max_height - margin)
                    if (currentY + nodeHeight <= config.Size - config.Margin)
                    {
                        // Check if better than current best (lower Y, then leftmost X)
                        if (currentY < bestY || (currentY == bestY && skyline[i].X < bestX))
                        {
                            bestY = currentY;
                            bestX = skyline[i].X;
                        }
                    }
                }
            }

            if (bestX != -1)
            {
                outX = bestX;
                outY = bestY;
                return true;
            }

            outX = -1;
            outY = -1;
            return false;
        }

        /// <summary> Checks if a rectangle fits horizontally starting at 'start_node_index'. </summary>
        /// <returns>True if the rectangle fits horizontally, false otherwise.</returns>
        private bool CanPlaceHorizontally(int start_node_index, int nodeWidth, out int outY)
        {
            // Assign default value for out param
            outY = 0;
            if (start_node_index < 0 || start_node_index >= skyline.Length) return false; // Bounds check

            int currentX = skyline[start_node_index].X;
            // Check horizontal boundary (respecting right margin)
            if (currentX + nodeWidth > config.Size - config.Margin)
            {
                return false;
            }

            int maxSpanY = 0;
            for (int i = start_node_index; i < skyline.Length; ++i)
            {
                maxSpanY = math.max(maxSpanY, skyline[i].Y);
                var width_covered = (skyline[i].X + skyline[i].Width) - currentX;

                if (width_covered >= nodeWidth)
                {
                    outY = maxSpanY; // This is the floor height the node must sit on
                    return true; // Fits
                }
                // Check for horizontal gap or end of skyline
                if (i + 1 >= skyline.Length || skyline[i + 1].X != (skyline[i].X + skyline[i].Width))
                {
                    return false; // Doesn't fit contiguously
                }
            }
            return false; // Shouldn't be reached if initial width check is correct
        }

        /// <summary> Updates the skyline structure after placing a rectangle. </summary>
        private void PlaceNodeAndUpdateSkyline(int x, int y, int nodeWidth, int nodeHeight)
        {
            var currentIndex = 0;
            var actualHeight = nodeHeight - config.Margin;
            var placedNodeTop = new SkylineNode
            {
                X = x,
                Y = y + actualHeight + config.Margin,   // bottom + rect + ONE margin
                Width = nodeWidth
            };

            // Phase 1: Remove/Split existing nodes covered by the new node's footprint
            while (currentIndex < skyline.Length)
            {
                ref var skylineNode = ref skyline.ElementAt(currentIndex); // Work with copy for safety? No, need ref for modification.
                int intersectStart = math.max(placedNodeTop.X, skylineNode.X);
                int intersectEnd = math.min(placedNodeTop.X + placedNodeTop.Width, skylineNode.X + skylineNode.Width);

                if (intersectStart >= intersectEnd)
                { // No overlap
                    if (skylineNode.X >= placedNodeTop.X + placedNodeTop.Width)
                        break; // Past the placed node
                    currentIndex++;
                    continue;
                }
                if (placedNodeTop.Y <= skylineNode.Y)
                { // Placed node is below this segment
                    currentIndex++;
                    continue;
                }

                // Overlap detected and new node is higher
                if (placedNodeTop.X <= skylineNode.X && (placedNodeTop.X + placedNodeTop.Width) >= (skylineNode.X + skylineNode.Width))
                {
                    // Case 1: Full cover -> Erase and re-evaluate index
                    skyline.RemoveAt(currentIndex); // Shifts subsequent elements
                    continue; // Process the element now at current_index
                }
                else if (placedNodeTop.X > skylineNode.X && (placedNodeTop.X + placedNodeTop.Width) < (skylineNode.X + skylineNode.Width))
                {
                    // Case 2: Split -> Create right part, adjust left part, insert right, then break loop
                    SkylineNode rightPart = new SkylineNode
                    {
                        X = placedNodeTop.X + placedNodeTop.Width,
                        Y = skylineNode.Y,
                        Width = (skylineNode.X + skylineNode.Width) - (placedNodeTop.X + placedNodeTop.Width)
                    };
                    // Modify the existing node (skyline[current_index]) to become the left part
                    skylineNode.Width = placedNodeTop.X - skylineNode.X;
                    skyline.InsertAt(currentIndex + 1, rightPart);
                    break; // Interaction finished for placed_node_top
                }
                else if (placedNodeTop.X <= skylineNode.X && (placedNodeTop.X + placedNodeTop.Width) < (skylineNode.X + skylineNode.Width))
                {
                    // Case 3: Overlap left -> Adjust node to become the right remainder, continue check
                    int originalEndX = skylineNode.X + skylineNode.Width;
                    skylineNode.X = placedNodeTop.X + placedNodeTop.Width;
                    skylineNode.Width = originalEndX - skylineNode.X;
                    currentIndex++;
                    continue;
                }
                else if (placedNodeTop.X > skylineNode.X && (placedNodeTop.X + placedNodeTop.Width) >= (skylineNode.X + skylineNode.Width))
                {
                    // Case 4: Overlap right -> Adjust node to become the left remainder, continue check
                    skylineNode.Width = placedNodeTop.X - skylineNode.X;
                    currentIndex++;
                    continue;
                }
                else
                {
                    currentIndex++; // Should not happen
                }
            }

            // Phase 2: Insert the new node for the top edge
            int insertPos = 0;
            while (insertPos < skyline.Length && skyline[insertPos].X < placedNodeTop.X)
            {
                insertPos++;
            }
            skyline.InsertAt(insertPos, placedNodeTop);

            // Phase 3: Merge adjacent nodes
            MergeSkyline();
        }

        /// <summary> Merges adjacent skyline nodes that have the same y-coordinate. </summary>
        private void MergeSkyline()
        {
            for (int i = 0; i + 1 < skyline.Length; /* no increment */)
            {
                // Need to get element refs again in case list was modified
                ref var current = ref skyline.ElementAt(i);
                ref var next = ref skyline.ElementAt(i + 1);

                if (current.Y == next.Y && (current.X + current.Width) == next.X)
                {
                    current.Width += next.Width; // Extend current node
                    skyline.RemoveAt(i + 1);     // Remove the next node
                                                 // Do not increment 'i', re-evaluate potential merge at current index
                }
                else
                {
                    ++i; // Move to next node only if no merge occurred
                }
            }
        }
    }
}