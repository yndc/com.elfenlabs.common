using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;

namespace Elfenlabs.Collections
{
    public interface IBlob<T> where T : unmanaged
    {
        public void Construct(BlobBuilder builder);
    }

    public interface IBlobField<T> where T : unmanaged
    {
        public void Serialize(BlobBuilder builder, T value);
        public T Deserialize(Allocator allocator);
    }

    public static class BlobUtility
    {
        public static unsafe void CopyTo<T>(void* dst, BlobAssetReference<T> blobRef) where T : unmanaged
        {
            var memoryWriter = new MemoryBinaryWriter();
            memoryWriter.Write(blobRef);
            unsafe
            {
                UnsafeUtility.MemCpy(dst, memoryWriter.Data, memoryWriter.Length);
                memoryWriter.Dispose();
            }
        }

        public static byte[] CopyToByteArray<T>(BlobAssetReference<T> blobRef, int version) where T : unmanaged
        {
            var memoryWriter = new MemoryBinaryWriter();
            memoryWriter.Write(version);
            memoryWriter.Write(blobRef);
            unsafe
            {
                var arr = new byte[memoryWriter.Length];
                fixed (byte* ptr = arr)
                {
                    UnsafeUtility.MemCpy(ptr, memoryWriter.Data, memoryWriter.Length);
                }
                memoryWriter.Dispose();
                return arr;
            }
        }

        public static byte[] CopyToByteArray<TOriginal, TBlobSerialized>(in TOriginal value)
            where TOriginal : unmanaged
            where TBlobSerialized : unmanaged, IBlobField<TOriginal>
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

        public static ByteArray<T> ToByteArray<T>(BlobBuilder builder, int version) where T : unmanaged
        {
            var blobRef = builder.CreateBlobAssetReference<T>(Allocator.Temp);
            var byteArray = new ByteArray<T>();
            byteArray.Bytes = CopyToByteArray(blobRef, version);
            blobRef.Dispose();
            return byteArray;
        }

        public static BlobAssetReference<T> CreateFromByteArray<T>(byte[] bytes, int version, Allocator allocator)
            where T : unmanaged
        {
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    var reader = new MemoryBinaryReader(ptr, bytes.Length);
                    BlobAssetReference<T>.TryRead(reader, version, out var blobAssetRef);
                    return blobAssetRef;
                }
            }
        }

        public static BlobAssetReference<T> CreateFromByteArray<T>(ByteArray<T> byteArray, int version, Allocator allocator)
            where T : unmanaged
        {
            return CreateFromByteArray<T>(byteArray.Bytes, version, allocator);
        }

        public static BlobAssetReference<T> CreateFromBlobByteArray<T>(ref BlobArray<byte> blobArray, int version, Allocator allocator)
            where T : unmanaged
        {
            unsafe
            {
                var reader = new MemoryBinaryReader((byte*)blobArray.GetUnsafePtr(), blobArray.Length);
                BlobAssetReference<T>.TryRead(reader, version, out var blobAssetRef);
                return blobAssetRef;
            }
        }

        public static TOriginal FromBytes<TOriginal, TBlobSerialized>(byte[] bytes, int version, Allocator allocator)
            where TOriginal : unmanaged
            where TBlobSerialized : unmanaged, IBlobField<TOriginal>
        {
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    var reader = new MemoryBinaryReader(ptr, bytes.Length);
                    BlobAssetReference<TBlobSerialized>.TryRead(reader, version, out var blobAssetRef);
                    var result = blobAssetRef.Value.Deserialize(allocator);
                    blobAssetRef.Dispose();
                    return result;
                }
            }
        }

        public static BlobAssetReference<TBlobSerialized> CreateReference<TOriginal, TBlobSerialized>(in TOriginal atlas, Allocator allocator = Allocator.Persistent)
            where TOriginal : unmanaged
            where TBlobSerialized : unmanaged, IBlobField<TOriginal>
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TBlobSerialized>();
            root.Serialize(builder, atlas);
            return builder.CreateBlobAssetReference<TBlobSerialized>(allocator);
        }

        public static UnsafeList<T> ToUnsafeList<T>(this ref BlobArray<T> blobArray, Allocator allocator) where T : unmanaged
        {
            var list = new UnsafeList<T>(blobArray.Length, allocator);
            for (int i = 0; i < blobArray.Length; i++)
            {
                list.Add(blobArray[i]);
            }
            return list;
        }
    }
}