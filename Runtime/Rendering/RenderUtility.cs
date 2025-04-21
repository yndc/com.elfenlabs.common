using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elfenlabs.Rendering
{
    public  static partial class RenderUtility
    {
        public static BatchMeshID RegisterMesh(World world, UnityEngine.Mesh mesh)
        {
            return world.GetExistingSystemManaged<EntitiesGraphicsSystem>().RegisterMesh(mesh);
        }

        public static BatchMaterialID RegisterMaterial(World world, Material material)
        {
            return world.GetExistingSystemManaged<EntitiesGraphicsSystem>().RegisterMaterial(material);
        }
    }
}