using System;
using Unity.Entities;

/// <summary>
/// Implement this interface to be notified when a world is created or destroyed.
/// </summary>
public interface IWorldLifecycle
{
    public void OnCreate(World world);

    public void OnSystemsCreated(World world);

    public void OnDestroy(World world);
}

public class WorldLifecycleFilterAttribute : Attribute
{
    /// <summary>
    /// The World the system belongs in.
    /// </summary>
    public WorldFlags Flags;

    public WorldLifecycleFilterAttribute(WorldFlags filter)
    {
        Flags = filter;
    }
}