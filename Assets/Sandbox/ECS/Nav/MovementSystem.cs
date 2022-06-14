// using Unity.Collections;
// using Unity.Entities;
// using Unity.Jobs.LowLevel.Unsafe;
// using Unity.Mathematics;
//
// namespace Sandbox.ECS.Nav
// {
//     public class MovementSystem : SystemBase
//     {
//         public float3 Target;
//
//         public float Speed;
//
//         public EntityQuery CelldataQuery;
//
//         public EntityQuery FlowFieldData;
//
//         public Random RandomNumCreator;
//
//         protected override void OnCreate()
//         {
//             base.OnCreate();
//
//             Target = new float3(-5, 0, 5);
//             Speed  = 15f;
//
//             CelldataQuery = GetEntityQuery(ComponentType.ReadOnly<CellsBestDirection>());
//             RequireSingletonForUpdate<FlowFieldData>();
//             RandomNumCreator = new Random();
//
//             Enabled = false;
//         }
//
//         protected override void OnStartRunning() { }
//
//         protected override void OnDestroy() { }
//
//         protected override void OnUpdate()
//         {
//             var randomNumCreator = new NativeArray<Random>(JobsUtility.MaxJobThreadCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
//             var r                = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
//             for ( var i = 0; i < JobsUtility.MaxJobThreadCount; i++ )
//             {
//                 randomNumCreator[i] = new Random(r == 0 ? r + 1 : r);
//             }
//
//             var cellentitys   = CelldataQuery.ToEntityArray(Allocator.TempJob);
//             var tempFlowField = GetSingleton<FlowFieldData>();
//             var speed         = Speed;
//             var tempTarget    = Target;
//             var timeDelta     = Time.DeltaTime;
//
//             var cellIndexWorldPos = new GetCellIndexFromWorldPost();
//         }
//     }
// }