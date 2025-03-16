using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class ContentPackage
{
    public int ID;
    public int Version;
    public List<Mesh> Meshes;
    public List<Material> Materials;
    public List<Prefab> Prefabs;
    public List<Texture> Sprites;
}