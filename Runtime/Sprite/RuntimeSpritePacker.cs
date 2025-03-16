using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct SpriteReference
{
    public int TextureIndex;
    public float4 TextureUV;
}

public class RuntimeSpritePacker : INativeDisposable
{
    public int2 AtlasSize;

    Texture2DArray pack;

    NativeHashMap<int, SpriteReference> cacheReferenceCache;

    public RuntimeSpritePacker(Allocator allocator, int2 atlasSize)
    {
        cacheReferenceCache = new NativeHashMap<int, SpriteReference>(64, allocator);
        AtlasSize = atlasSize;
        pack = new Texture2DArray(atlasSize.x, atlasSize.y, 1, TextureFormat.RGBA32, false);
    }

    public SpriteReference GetReference(Sprite sprite)
    {
        var hash = sprite.GetHashCode();
        if (cacheReferenceCache.TryGetValue(hash, out var reference))
        {
            return reference;
        }

        var texture = sprite.texture;
        var rect = sprite.rect;
        var textureUV = new float4(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);

        reference = new SpriteReference
        {
            TextureIndex = 0,
            TextureUV = textureUV
        };

        cacheReferenceCache[hash] = reference;

        return reference;
    }

    public JobHandle Dispose(JobHandle inputDeps)
    {
        return cacheReferenceCache.Dispose(inputDeps);
    }

    public void Dispose()
    {
        cacheReferenceCache.Dispose();
    }
}