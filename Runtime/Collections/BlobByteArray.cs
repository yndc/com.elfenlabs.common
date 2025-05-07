using System;
using Unity.Collections;
using Unity.Entities;

namespace Elfenlabs.Collections
{
    /// <summary>
    /// Simple type wrapper for a C# byte array 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public struct ByteArray<T> where T : unmanaged
    {
        public byte[] Bytes;

        public bool IsCreated => Bytes != null && Bytes.Length > 0;

        public void Serialize(BlobAssetReference<T> blobRef, int version)
        {
            Bytes = BlobUtility.CopyToByteArray(blobRef, version);
        }

        public BlobAssetReference<T> Deserialize(int version, Allocator allocator)
        {
            return BlobUtility.CreateFromByteArray<T>(Bytes, version, allocator);
        }
    }
}