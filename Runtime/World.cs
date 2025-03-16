using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Entities;
using System.Reflection;

public static class WorldUtility
{
    /// <summary>
    /// Create a new world with the given name and flags. Will execute all IWorldLifecycle.OnStart() methods.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="flags"></param>
    /// <param name="systemFlags"></param>
    /// <returns></returns>
    public static World CreateWorld(string name, WorldFlags flags, WorldSystemFilterFlags systemFlags, Action<World> onCreate = null)
    {
        Debug.LogFormat("Creating world {0}", name);

        var world = new World(name, flags);

        if (onCreate != null) onCreate(world);

        // Execute all IWorldLifecycle.OnCreate() methods
        var lifecycles = GetAllWorldLifecycleMethods(flags);
        foreach (var l in lifecycles) l.OnCreate(world);

        // Add systems to the world, this will call OnCreate for all systems
        var systems = DefaultWorldInitialization.GetAllSystems(systemFlags);
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
        ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

        // Execute all IWorldLifecycle.OnSystemsCreated() methods
        foreach (var l in lifecycles) l.OnSystemsCreated(world);

        Debug.Log("Done");
        return world;
    }

    /// <summary>
    /// Add default systems to the given world. Will execute OnCreate on all systems
    /// </summary>
    /// <param name="world"></param>
    /// <param name="systemFlags"></param>
    public static void AddDefaultSystems(World world, WorldSystemFilterFlags systemFlags)
    {
        var systems = DefaultWorldInitialization.GetAllSystems(systemFlags);
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
        ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
    }

    public static IEnumerable<IWorldLifecycle> GetAllWorldLifecycleMethods(WorldFlags flags)
    {
        AppDomain app = AppDomain.CurrentDomain;
        Assembly[] ass = app.GetAssemblies();
        Type[] types;
        Type targetType = typeof(IWorldLifecycle);

        foreach (Assembly a in ass)
        {
            types = a.GetTypes();
            foreach (Type t in types)
            {
                if (t.IsInterface) continue;
                if (t.IsAbstract) continue;
                foreach (Type iface in t.GetInterfaces())
                {
                    if (!iface.Equals(targetType)) continue;
                    var attr = t.GetCustomAttribute<WorldLifecycleFilterAttribute>();
                    if (attr != null && (attr.Flags & flags) != attr.Flags)
                    {
                        continue;
                    }
                    yield return (IWorldLifecycle)Activator.CreateInstance(t);
                    break;
                }
            }
        }
    }
}