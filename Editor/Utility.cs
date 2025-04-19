using UnityEngine;
using UnityEditor;

public static class AssetUtility
{
    public static void DeleteAllSubObjects<T>(this T obj) where T : Object
    {
        var path = AssetDatabase.GetAssetPath(obj);
        var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var subAsset in subAssets)
        {
            if (subAsset != obj)
            {
                AssetDatabase.RemoveObjectFromAsset(subAsset);
                Object.DestroyImmediate(subAsset, true);
            }
        }
    }
}