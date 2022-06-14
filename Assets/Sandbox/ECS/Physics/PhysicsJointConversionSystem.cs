using Unity.Entities;
using Unity.Physics.Authoring;

namespace Sandbox.ECS.Physics
{
    [UpdateAfter(typeof(PhysicsBodyConversionSystem))]
    [UpdateAfter(typeof(LegacyRigidbodyConversionSystem))]
    [UpdateAfter(typeof(BeginJointConversionSystem))]
    [UpdateBefore(typeof(EndJointConversionSystem))]
    public class PhysicsJointConversionSystem : GameObjectConversionSystem
    {
        void CreateJoint(BaseJoint joint)
        {
            if ( !joint.enabled )
                return;

            joint.EntityA = GetPrimaryEntity(joint.LocalBody);
            joint.EntityB = joint.ConnectedBody == null ? Entity.Null : GetPrimaryEntity(joint.ConnectedBody);

            joint.Create(DstEntityManager, this);
        }

        protected void CreateJoints<T>() where T : BaseJoint
        {
            Entities.ForEach((T joint) =>
                             {
                                 foreach ( var j in joint.GetComponents<T>() )
                                 {
                                     if ( joint.GetType() == j.GetType() )
                                     {
                                         CreateJoint(j);
                                     }
                                 }
                             });
        }

        protected override void OnUpdate() { CreateJoints<LimitDOFJoint>(); }
    }
}