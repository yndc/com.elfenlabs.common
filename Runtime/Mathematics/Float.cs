using Unity.Mathematics;

public static partial class MathematicsExtensions
{
    public static float2 Swap(this float2 value) => new float2(value.y, value.x);
}