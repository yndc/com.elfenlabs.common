using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elfenlabs.Mesh
{
    public static partial class MeshUtility
    {
        private static readonly float2 DefaultSize = new float2(1, 1);

        public static UnityEngine.Mesh CreateQuad() => CreateQuad(DefaultSize);
        public static UnityEngine.Mesh CreateQuad(float x, float y) => CreateQuad(new float2(x, y));

        public static UnityEngine.Mesh CreateQuad(float2 size)
        {
            var mesh = new UnityEngine.Mesh();
            var vertices = new Vector3[4];
            var triangles = new int[6];

            vertices[0] = new Vector3(-size.x / 2, -size.y / 2, 0);
            vertices[1] = new Vector3(size.x / 2, -size.y / 2, 0);
            vertices[2] = new Vector3(-size.x / 2, size.y / 2, 0);
            vertices[3] = new Vector3(size.x / 2, size.y / 2, 0);

            triangles[0] = 0;
            triangles[1] = 2;
            triangles[2] = 1;
            triangles[3] = 2;
            triangles[4] = 3;
            triangles[5] = 1;

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public static Entity CreatePrefab(string name, UnityEngine.Mesh mesh, EntityManager entityManager, Shader shader, int layer)
        {
            var material = new Material(shader);
            var desc = new RenderMeshDescription(
                shadowCastingMode: ShadowCastingMode.Off,
                receiveShadows: false,
                motionVectorGenerationMode: MotionVectorGenerationMode.ForceNoMotion,
                layer: layer,
                renderingLayerMask: 4294967295,
                lightProbeUsage: LightProbeUsage.Off,
                staticShadowCaster: false
            );

            // Create an array of mesh and material required for runtime rendering.
            var renderMeshArray = new RenderMeshArray(new Material[] { material }, new UnityEngine.Mesh[] { mesh });

            // Create empty base entity
            var prefab = entityManager.CreateEntity();
            entityManager.SetName(prefab, name);
            entityManager.AddComponent<Prefab>(prefab);
            entityManager.AddComponent<LocalToWorld>(prefab);
            entityManager.AddComponent<Parent>(prefab);
            entityManager.AddComponentData(prefab, new LocalTransform { Scale = 1f });
            entityManager.AddComponentData(prefab, new PostTransformMatrix { Value = float4x4.identity });

            // Populate the prototype entity with the required components
            RenderMeshUtility.AddComponents(
                prefab,
                entityManager,
                desc,
                renderMeshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

            return prefab;
        }
    }
}