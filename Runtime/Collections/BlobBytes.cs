using Unity.Collections;
using Unity.Entities;

namespace Elfenlabs.Collections
{
    /// <summary>
    /// Simple wrapper for a byte array that can be serialized and deserialized to a blob asset
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct Blob<T> : IBlobField<BlobAssetReference<T>>
        where T : unmanaged
    {
        public BlobArray<byte> Data;

        public BlobAssetReference<T> Deserialize(Allocator allocator)
        {
            unsafe
            {
                return BlobAssetReference<T>.Create(
                    Data.GetUnsafePtr(),
                    Data.Length);
            }
        }

        public void Serialize(BlobBuilder builder, BlobAssetReference<T> value)
        {
            unsafe
            {
                BlobUtility.CopyTo(Data.GetUnsafePtr(), value);
            }
        }
    }
}