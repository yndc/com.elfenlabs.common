using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;

[CreateAssetMenu(fileName = "SpritePack", menuName = "Content/Sprite Pack")]
public class SpritePack : ScriptableObject
{
    [SerializeField] public List<Texture2D> Textures = new List<Texture2D>();
    [SerializeField] public int AtlasSize = 1024;
    [SerializeField] public int Padding = 2;
    [SerializeField][HideInInspector] Texture2DArray atlasArray;
    [SerializeField][HideInInspector] List<SerializedSpriteReference> serializedReferences = new();

    [System.NonSerialized][HideInInspector] private Dictionary<Texture2D, SpriteReference> textureToReference = new();

    private void OnEnable()
    {
        // Initialize the dictionary at runtime or in the Editor when the object is loaded
        foreach (var serializedRef in serializedReferences)
        {
            SpriteReference reference = new SpriteReference
            {
                TextureIndex = serializedRef.TextureIndex,
                TextureUV = new float4(serializedRef.TextureUV.x, serializedRef.TextureUV.y,
                                      serializedRef.TextureUV.z, serializedRef.TextureUV.w)
            };
            textureToReference[serializedRef.texture] = reference;
        }
    }

    private void OnValidate()
    {
        // Automatically repack when changes are made in the Inspector
        EditorApplication.delayCall += Repack;
    }

    /// <summary>
    /// Retrieves the SpriteReference for a given texture.
    /// </summary>
    /// <param name="texture">The texture to look up.</param>
    /// <returns>The SpriteReference containing texture index and UV, or default if not found.</returns>
    public SpriteReference GetSpriteReference(Texture2D texture)
    {
        if (textureToReference.TryGetValue(texture, out SpriteReference reference))
        {
            return reference;
        }
        return default; // Returns empty SpriteReference if texture not found
    }

    /// <summary>
    /// Repacks all textures into atlases and updates the Texture2DArray.
    /// </summary>
    private void Repack()
    {
        // Clear existing references and atlas
        serializedReferences.Clear();
        if (atlasArray != null)
        {
            AssetDatabase.RemoveObjectFromAsset(atlasArray);
            Object.DestroyImmediate(atlasArray);
        }

        // Get unique non-null textures
        HashSet<Texture2D> uniqueTextures = new HashSet<Texture2D>();
        foreach (Texture2D texture in Textures)
        {
            if (texture != null)
            {
                uniqueTextures.Add(texture);
            }
        }
        List<Texture2D> texturesToPack = new List<Texture2D>(uniqueTextures);

        // Pack textures into atlases
        List<Texture2D> atlases = new List<Texture2D>();
        int textureIndex = 0;
        while (texturesToPack.Count > 0)
        {
            Texture2D atlas = new(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);
            Texture2D[] remainingTextures = texturesToPack.ToArray();
            Rect[] rects = atlas.PackTextures(remainingTextures, Padding, AtlasSize, false);
            if (rects == null || rects.Length == 0)
            {
                Debug.LogError("Failed to pack textures. Atlas size might be too small or no textures to pack.");
                DestroyImmediate(atlas);
                break;
            }

            // Check if the atlas was resized by PackTextures
            if (atlas.width != AtlasSize || atlas.height != AtlasSize)
            {
                // Create a new atlas with the desired size
                Texture2D tempAtlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);
                tempAtlas.SetPixels(new Color[AtlasSize * AtlasSize]);

                // Copy the packed content into the new atlas
                Graphics.CopyTexture(atlas, 0, 0, 0, 0, atlas.width, atlas.height, tempAtlas, 0, 0, 0, 0);

                // Clean up and replace the original atlas
                Object.DestroyImmediate(atlas);
                atlas = tempAtlas;
            }

            int packedCount = rects.Length;
            for (int i = 0; i < packedCount; i++)
            {
                Texture2D packedTex = remainingTextures[i];
                Rect uvRect = rects[i];

                // Calculate UV as (x1, y1, x2, y2)
                // x1, y1 = bottom-left corner
                // x2, y2 = top-right corner
                float x1 = uvRect.x;
                float y1 = uvRect.y;
                float x2 = uvRect.x + uvRect.width;
                float y2 = uvRect.y + uvRect.height;
                float4 uv = new float4(x1, y1, x2, y2);

                SpriteReference reference = new SpriteReference
                {
                    TextureIndex = textureIndex,
                    TextureUV = uv
                };

                // Serialize the reference
                SerializedSpriteReference serializedRef = new SerializedSpriteReference
                {
                    texture = packedTex,
                    TextureIndex = reference.TextureIndex,
                    TextureUV = new Vector4(uv.x, uv.y, uv.z, uv.w)
                };
                serializedReferences.Add(serializedRef);
            }

            // Remove packed textures from the list
            for (int i = 0; i < packedCount; i++)
            {
                texturesToPack.Remove(remainingTextures[i]);
            }
            atlases.Add(atlas);
            textureIndex++;
        }

        // Create Texture2DArray if there are atlases
        if (atlases.Count > 0)
        {
            atlasArray = new Texture2DArray(AtlasSize, AtlasSize, atlases.Count, TextureFormat.RGBA32, false);
            for (int i = 0; i < atlases.Count; i++)
            {
                Color32[] pixels = atlases[i].GetPixels32();
                Debug.Log(pixels.Length);
                Debug.Log(atlasArray.GetPixels32(i).Length);
                atlasArray.SetPixels32(pixels, i);
            }
            atlasArray.Apply();
            AssetDatabase.AddObjectToAsset(atlasArray, this); // Save as sub-asset
        }

        // Clean up atlas textures
        foreach (Texture2D atlas in atlases)
        {
            Object.DestroyImmediate(atlas);
        }

        // Update runtime dictionary
        textureToReference.Clear();
        foreach (var serializedRef in serializedReferences)
        {
            SpriteReference reference = new SpriteReference
            {
                TextureIndex = serializedRef.TextureIndex,
                TextureUV = new float4(serializedRef.TextureUV.x, serializedRef.TextureUV.y,
                                      serializedRef.TextureUV.z, serializedRef.TextureUV.w)
            };
            textureToReference[serializedRef.texture] = reference;
        }

        // Save changes
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
}

/// <summary>
/// Serializable version of SpriteReference for storage.
/// </summary>
[System.Serializable]
public class SerializedSpriteReference
{
    public Texture2D texture;
    public int TextureIndex;
    public Vector4 TextureUV; // Serialized as Vector4
}