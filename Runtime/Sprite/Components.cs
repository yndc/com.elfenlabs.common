using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

[MaterialProperty("_SpriteTextureRect")]
public struct SpriteTextureRect : IComponentData
{
    public float4 Value;
}

[MaterialProperty("_SpriteTint")]
public struct SpriteTint : IComponentData
{
    public float4 Value;
}

[MaterialProperty("_SpriteOverlay")]
public struct SpriteOverlay : IComponentData
{
    public float4 Value;
}

[MaterialProperty("_BaseColor")]
public struct SpriteBackgroundColor : IComponentData
{
    public float4 Value;
}

public struct SpriteTextureConfig : ISharedComponentData, IEquatable<SpriteTextureConfig>
{
    public Sprite Sprite;

    public bool Equals(SpriteTextureConfig other)
    {
        return Sprite == other.Sprite;
    }

    public override int GetHashCode()
    {
        return Sprite.GetHashCode();
    }
}

public struct SpriteConfig : ISharedComponentData
{
    public int TextureIndex;
    public float4 TextureRect;
    public float4 Tint;
    public float4 Overlay;
}
