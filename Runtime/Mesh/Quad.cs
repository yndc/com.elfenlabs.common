using Unity.Mathematics;
using UnityEngine;

namespace Elfenlabs.Mesh
{
    public static partial class MeshUtility
    {
        private static readonly float2 DefaultSize = new float2(1, 1);

        public static UnityEngine.Mesh CreateQuad() => CreateQuad(DefaultSize);

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
    }
}