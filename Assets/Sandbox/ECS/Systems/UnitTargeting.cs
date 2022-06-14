// using Unity.Burst;
// using Unity.Collections;
// using Unity.Entities;
// using Unity.Physics;
// using Unity.Physics.Systems;
//
// [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
// [UpdateAfter(typeof(ExportPhysicsWorld))]
// [UpdateBefore(typeof(EndFramePhysicsSystem))]
// public partial class UnitTargeting : SystemBase
// {
//     private StepPhysicsWorld _stepPhysicsWorld;
//
//     protected override void OnCreate() { _stepPhysicsWorld = World.GetOrCreateSystem<StepPhysicsWorld>(); }
//
//     protected override void OnStartRunning()
//     {
//         base.OnStartRunning();
//         this.RegisterPhysicsRuntimeSystemReadOnly();
//     }
//
//     protected override void OnUpdate()
//     {
//         Dependency = new TriggerTargetJob { AttackRangeGroup = GetComponentDataFromEntity<UnitAttackRangeComponent>(true), UnitCurrentTargetGroup = GetComponentDataFromEntity<UnitCurrentTargetComponent>() }
//             .Schedule(_stepPhysicsWorld.Simulation, Dependency);
//     }
//
//     [BurstCompile]
//     private struct TriggerTargetJob : ITriggerEventsJob
//     {
//         [ReadOnly] public ComponentDataFromEntity<UnitAttackRangeComponent> AttackRangeGroup;
//
//         public ComponentDataFromEntity<UnitCurrentTargetComponent> UnitCurrentTargetGroup;
//
//         public void Execute(TriggerEvent triggerEvent)
//         {
//             var entityA = triggerEvent.EntityA;
//             var entityB = triggerEvent.EntityB;
//
//             var isBodyATarget = UnitCurrentTargetGroup.HasComponent(entityA);
//             var isBodyBTarget = UnitCurrentTargetGroup.HasComponent(entityB);
//
//             var triggerEntity = isBodyATarget ? entityA : entityB;
//             var dynamicEntity = isBodyATarget ? entityB : entityA;
//         }
//     }
// }