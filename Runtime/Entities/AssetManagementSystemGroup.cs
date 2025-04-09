using Unity.Entities;

namespace Elfenlabs.Entities
{
    /// <summary>
    /// A system group for asset management systems.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class AssetManagementSystemGroup : ComponentSystemGroup { }
}