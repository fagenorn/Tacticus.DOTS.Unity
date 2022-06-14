using Sandbox.ECS.Mouse;

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

using UnityEngine;

using RaycastHit = Unity.Physics.RaycastHit;

namespace Sandbox.ECS.FlowField
{
    [UpdateAfter(typeof(BuildPhysicsWorld))]
    [UpdateBefore(typeof(EndFramePhysicsSystem))]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [DisableAutoCreation]
    public partial class InitializeFlowFieldSystem : SystemBase
    {
        private Entity _flowFieldEntity;

        private EntityQuery _flowFieldControllerQuery;

        private Entity _flowFieldControllerEntity;

        private Camera _mainCamera;

        PhysicsWorld physicsWorld => World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>().PhysicsWorld;

        protected override void OnCreate() { _flowFieldControllerQuery = GetEntityQuery(typeof(FlowFieldControllerData)); }

        protected override void OnStartRunning() { _mainCamera = Camera.main; }

        protected override void OnUpdate()
        {
            if ( Input.GetMouseButtonDown(0) )
            {
                // var mousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Input.mousePosition.z);
                // var ray      = _mainCamera.ScreenPointToRay(mousePos);
                // // Physics.Raycast(ray, out var hit, float.MaxValue, -1);
                // var worldMousePos = hit.point;
                var physicsWorldSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>();
                var collisionWorld     = physicsWorldSystem.PhysicsWorld.CollisionWorld;
                var unityRay           = _mainCamera.ScreenPointToRay(Input.mousePosition);
                var input              = new RaycastInput { Start = unityRay.origin, End = unityRay.origin + unityRay.direction * 1000, Filter = CollisionFilter.Default, };
                var hit                = new RaycastHit();
                var haveHit            = collisionWorld.CastRay(input, out hit);

                if ( !haveHit )
                {
                    return;
                }

                var worldMousePos = hit.Position;

                _flowFieldControllerEntity = _flowFieldControllerQuery.GetSingletonEntity();

                FlowFieldControllerData flowFieldControllerData = EntityManager.GetComponentData<FlowFieldControllerData>(_flowFieldControllerEntity);

                FlowFieldData flowFieldData = new FlowFieldData { gridSize = flowFieldControllerData.gridSize, cellRadius = flowFieldControllerData.cellRadius };

                NewFlowFieldData newFlowFieldData = new NewFlowFieldData { isExistingFlowField = true };

                if ( _flowFieldEntity.Equals(Entity.Null) )
                {
                    _flowFieldEntity = EntityManager.CreateEntity();
                    EntityManager.AddComponent<FlowFieldData>(_flowFieldEntity);
                    newFlowFieldData.isExistingFlowField = false;
                }

                EntityManager.AddComponent<NewFlowFieldData>(_flowFieldEntity);
                EntityManager.SetComponentData(_flowFieldEntity, flowFieldData);
                EntityManager.SetComponentData(_flowFieldEntity, newFlowFieldData);
            }
        }
    }
}