using Elfenlabs.Debug;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Elfenlabs.Texture
{
    public static class TextureUtility
    {
        /// <summary>
        /// Creates a new Texture2DArray with a different depth, copying contents from the source.
        /// This effectively "resizes" the array by creating a larger one.
        /// </summary>
        /// <param name="sourceTexture">The original Texture2DArray.</param>
        /// <param name="newDepth">The desired number of slices for the new texture. Must be greater than sourceTexture.depth.</param>
        /// <param name="destroyOriginal">If true, the original sourceTexture will be destroyed after copying.</param>
        /// <returns>The new Texture2DArray with the specified depth, or null if creation failed or inputs were invalid.</returns>
        public static Texture2DArray CloneWithDepth(Texture2DArray sourceTexture, int newDepth, bool destroyOriginal = false)
        {
            // --- Input Validation ---
            if (sourceTexture == null)
            {
                Log.Error("ResizeDepth Error: Source Texture2DArray is null.");
                return null;
            }

            if (newDepth <= sourceTexture.depth)
            {
                Log.Error($"ResizeDepth Error: New depth ({newDepth}) must be greater than the original depth ({sourceTexture.depth}).");
                return null;
            }

            // --- Get Properties from Source ---
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            int oldDepth = sourceTexture.depth;
            GraphicsFormat format = sourceTexture.graphicsFormat;
            int mipCount = sourceTexture.mipmapCount;
            // Determine creation flags based on whether the original had mipmaps
            TextureCreationFlags flags = (mipCount > 1) ? TextureCreationFlags.MipChain : TextureCreationFlags.None;

            // --- Create New Texture ---
            Texture2DArray newTexture = null;
            try
            {
                newTexture = new Texture2DArray(width, height, newDepth, format, flags);
                newTexture.name = $"{sourceTexture.name}_Resized_{newDepth}";
                // Optionally copy other settings like filterMode, wrapMode etc. if needed
                // newTexture.filterMode = sourceTexture.filterMode;
                // newTexture.wrapMode = sourceTexture.wrapMode;
            }
            catch (System.Exception ex)
            {
                Log.Error($"ResizeDepth Error: Failed to create new Texture2DArray ({width}x{height}x{newDepth}, {format}): {ex.Message}");
                if (newTexture != null) Object.Destroy(newTexture); // Clean up if partially created
                return null;
            }

            // --- Copy Contents ---
            // Copy slice by slice, mip by mip
            for (int slice = 0; slice < oldDepth; ++slice)
            {
                for (int mip = 0; mip < mipCount; ++mip)
                {
                    // Ensure Graphics.CopyTexture runs without error
                    try
                    {
                        Graphics.CopyTexture(sourceTexture, slice, mip, newTexture, slice, mip);
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"ResizeDepth Error: Failed to copy slice {slice}, mip {mip}: {ex.Message}");
                        Object.Destroy(newTexture); // Clean up the new texture on error
                        if (destroyOriginal) Object.Destroy(sourceTexture); // Destroy original if requested, even on error? Maybe not desirable.
                        return null; // Return null indicating failure
                    }
                }
            }

            // --- Destroy Original (Optional) ---
            if (destroyOriginal)
            {
                Object.Destroy(sourceTexture);
            }

            return newTexture;
        }

        public static Texture2D Clone(Texture2D sourceTexture, TextureCreationFlags flags = TextureCreationFlags.DontInitializePixels)
        {
            var texture = new Texture2D(
                sourceTexture.width,
                sourceTexture.height,
                sourceTexture.graphicsFormat,
                flags | TextureCreationFlags.DontUploadUponCreate)
            {
                name = sourceTexture.name + "_clone",
                filterMode = sourceTexture.filterMode,
                wrapMode = sourceTexture.wrapMode,
            };

            Graphics.CopyTexture(sourceTexture, texture);
            return texture;
        }

        public static void Fill(Texture2D texture, Color32 color)
        {
            var array = texture.GetRawTextureData<Color32>();
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = color;
            }
        }
    }
}