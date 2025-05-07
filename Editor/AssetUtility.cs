using UnityEditor;
using Unity.Mathematics;
using System;

namespace Elfenlabs.Editor
{
    public static class AssetUtility
    {
        public static void DeleteAllSubObjectsOfType<T>(UnityEngine.Object obj) where T : UnityEngine.Object
        {
            var path = AssetDatabase.GetAssetPath(obj);
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var subAsset in subAssets)
            {
                if (subAsset != obj && subAsset is T)
                {
                    AssetDatabase.RemoveObjectFromAsset(subAsset);
                    UnityEngine.Object.DestroyImmediate(subAsset, true);
                }
            }
        }

        public static uint4 GetAssetGUID(UnityEngine.Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (guid == null || guid.Length != 32)
                return new uint4(0, 0, 0, 0);

            return new uint4(
                Convert.ToUInt32(guid.Substring(0, 8), 16),
                Convert.ToUInt32(guid.Substring(8, 8), 16),
                Convert.ToUInt32(guid.Substring(16, 8), 16),
                Convert.ToUInt32(guid.Substring(24, 8), 16));
        }
    }
}