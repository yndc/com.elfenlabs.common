using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Elfenlabs.Companion
{
    public class TransformFollowEntity : IComponentData
    {
        public Transform Value;
        public TransformFollowEntity()
        {
            Value = null;
        }
        // public float4x4 PostTransformMatrix;
    }

    public class EntityFollowTransform : IComponentData
    {
        public Transform Value;
        public EntityFollowTransform()
        {
            Value = null;
        }
        public EntityFollowTransform(Transform value)
        {
            Value = value;
        }
    }

    [WriteGroup(typeof(LocalToWorld))]
    public struct LocalToWorldOverride : IComponentData { }

    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class TransformLinkSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .WithoutBurst()
                .WithStructuralChanges()
                .WithAll<EntityFollowTransform>()
                .WithAbsent<LocalToWorldOverride>()
                .ForEach((Entity entity) =>
            {
                EntityManager.AddComponent<LocalToWorldOverride>(entity);
            }).Run();

            Entities
                .WithoutBurst()
                .WithAll<TransformFollowEntity, LocalToWorld>()
                .ForEach(
                (in LocalToWorld transform, in TransformFollowEntity config) =>
                {
                    config.Value.SetPositionAndRotation(transform.Position, transform.Rotation);
                    // TODO: look for local transform
                }).Run();

            Entities
                .WithoutBurst()
                .WithAll<EntityFollowTransform, LocalTransform, LocalToWorld>()
                .ForEach(
                (ref LocalToWorld ltw, in LocalTransform transform, in EntityFollowTransform config) =>
                {
                    var gameObjectMatrix = (float4x4)config.Value.localToWorldMatrix;
                    var finalTransform = math.mul(transform.ToMatrix(), gameObjectMatrix);
                    ltw.Value = finalTransform;
                }).Run();
        }
    }
}