using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;

namespace Elfenlabs.Collections
{
    public interface IBlobSerialized<T> where T : unmanaged
    {
        public void Serialize(BlobBuilder builder, T value);
        public T Deserialize(Allocator allocator);
    }

    public static class BlobUtility
    {
        public static byte[] ToBytes<TOriginal, TBlobSerialized>(in TOriginal value)
            where TOriginal : unmanaged
            where TBlobSerialized : unmanaged, IBlobSerialized<TOriginal>
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TBlobSerialized>();
            root.Serialize(builder, value);
            var memoryWriter = new MemoryBinaryWriter();
            BlobAssetReference<TBlobSerialized>.Write(memoryWriter, builder, 0);
            unsafe
            {
                var arr = new byte[memoryWriter.Length];
                fixed (byte* ptr = arr)
                {
                    UnsafeUtility.MemCpy(ptr, memoryWriter.Data, memoryWriter.Length);
                }
                return arr;
            }
        }

        public static TOriginal FromBytes<TOriginal, TBlobSerialized>(byte[] bytes, Allocator allocator)
            where TOriginal : unmanaged
            where TBlobSerialized : unmanaged, IBlobSerialized<TOriginal>
        {
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    var reader = new MemoryBinaryReader(ptr, bytes.Length);
                    BlobAssetReference<TBlobSerialized>.TryRead(reader, 0, out var blobAssetRef);
                    var result = blobAssetRef.Value.Deserialize(allocator);
                    blobAssetRef.Dispose();
                    return result;
                }
            }
        }

        public static BlobAssetReference<TBlobSerialized> CreateReference<TOriginal, TBlobSerialized>(in TOriginal atlas, Allocator allocator = Allocator.Persistent)
            where TOriginal : unmanaged
            where TBlobSerialized : unmanaged, IBlobSerialized<TOriginal>
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TBlobSerialized>();
            root.Serialize(builder, atlas);
            return builder.CreateBlobAssetReference<TBlobSerialized>(allocator);
        }
    }
}