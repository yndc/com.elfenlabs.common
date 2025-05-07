using Unity.Mathematics;

namespace Elfenlabs.Geometry
{
    /// <summary>
    /// A rectangle with an index.
    /// </summary>
    public struct IndexedRect
    {
        public int Index;
        public float4 Rect;
    }
}