// using Unity.Collections;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Transforms;
//
// using UnityEngine;
//
// using Random = UnityEngine.Random;
//
// namespace Sandbox.ECS.Nav
// {
//     public struct PlayerInput : IComponentData
//     {
//         public float2 moveInput;
//
//         public bool fireInput;
//
//         public bool startFloodField;
//
//         public bool addLayer;
//
//         public bool calcOtherLayer;
//     }
//
//     [UpdateAfter(typeof(PlayerInputStuffSystem))]
//     public class PlayerTestInput : SystemBase
//     {
//         private Camera _mainCamera;
//
//         public FlowFieldData tempDestination;
//
//         public Entity tempdestentity;
//
//         public EntityQuery destinationBoofQuery;
//
//         public DotPrefabinator tempMoveObject;
//
//         protected override void OnCreate() { destinationBoofQuery = GetEntityQuery(typeof(CellDestinationBuffer)); }
//
//         protected override void OnStartRunning()
//         {
//             _mainCamera    = Camera.main;
//             tempMoveObject = HasSingleton<DotPrefabinator>() ? GetSingleton<DotPrefabinator>() : default;
//         }
//         
//         protected override void OnUpdate()
//         {
//             var tempInput             = GetSingleton<PlayerInput>();
//
//             if ( HasSingleton<FlowFieldData>() )
//             {
//                 tempDestination = GetSingleton<FlowFieldData>();
//             }
//
//             int numOfTimes = 0;
//
//             DynamicBuffer<CellDestinationBuffer> tempDestinatebuffo;
//
//             if ( tempInput.fireInput )
//             {
//                 var randomBuff = Random.Range(0, 4);
//                 if ( HasSingleton<FlowFieldData>() )
//                 {
//                     var tempBuffDestinationSentity = GetSingletonEntity<CellDestinationBuffer>();
//                     tempDestinatebuffo = GetBuffer<CellDestinationBuffer>(tempBuffDestinationSentity);
//
//                     SetSingleton(tempDestination);
//                     SetSingleton(new CellBDLayerToCalc { intVal = randomBuff });
//                 }
//                 else
//                 {
//                     return;
//                 }
//
//                 var temPositKyan = tempDestination.gridSize.x - 1;
//                 switch ( randomBuff )
//                 {
//                     case 0:
//                     {
//                         var treeNumb   = new int2(66, 129);     //ToDo: Too high??
//                         var tempBuffee = tempDestinatebuffo[0]; //ToDo: Sets destination
//                         tempBuffee.Destination = treeNumb;
//                         tempDestinatebuffo[0]  = treeNumb;
//
//                         break;
//                     }
//                     case 1:
//                     {
//                         var treeTwo    = new int2(2, 2);
//                         var tempBuffee = tempDestinatebuffo[1];
//                         tempBuffee.Destination = treeTwo;
//                         tempDestinatebuffo[1]  = tempBuffee;
//
//                         break;
//                     }
//                     case 2:
//                     {
//                         var treeTwo    = new int2(97, 2);
//                         var tempBuffee = tempDestinatebuffo[2];
//                         tempBuffee.Destination = treeTwo;
//                         tempDestinatebuffo[2]  = tempBuffee;
//
//                         break;
//                     }
//                     case 3:
//                     {
//                         var treeTwo    = new int2(2, 97);
//                         var tempBuffee = tempDestinatebuffo[3];
//                         tempBuffee.Destination = treeTwo;
//                         tempDestinatebuffo[3]  = tempBuffee;
//
//                         break;
//                     }
//                 }
//
//                 // if ( !HasSingleton<CalcintegrationFieldEvent>() )
//                 // {
//                 //     EntityManager.CreateEntity(typeof(CalcintegrationFieldEvent));
//                 // }
//
//                 var                 tempobject = tempMoveObject.TestObjectPrefab;
//                 int                 totalentys = 100;
//                 NativeArray<Entity> dudees     = new NativeArray<Entity>(totalentys, Allocator.Temp);
//                 EntityManager.Instantiate(tempobject, dudees);
//                 // EntityManager.AddComponent<TestMoveojbectTag>(dudees);
//                 EntityManager.AddComponent<CellBDLayer>(dudees);
//                 for ( int i = 0; i < dudees.Length; i++ )
//                 {
//                     int xrandnumpos  = UnityEngine.Random.Range(90, 120);
//                     int zrandnumpos  = UnityEngine.Random.Range(-20, -40);
//                     var thecreatepos = new float3(xrandnumpos, 1, zrandnumpos);
//
//                     EntityManager.SetComponentData<Translation>(dudees[i], new Translation { Value  = thecreatepos });
//                     EntityManager.SetComponentData<CellBDLayer>(dudees[i], new CellBDLayer { intVal = randomBuff });
//                 }
//
//                 dudees.Dispose();
//             }
//
//             if ( tempInput.startFloodField )
//             {
//                 if ( HasSingleton<NewFlowFieldEvent>() || HasSingleton<FlowFieldData>() )
//                 {
//                     return;
//                 }
//
//                 EntityManager.CreateEntity(typeof(NewFlowFieldEvent));
//             }
//
//             if ( tempInput.addLayer )
//             {
//                 EntityManager.CreateEntity(typeof(StartSpawningSystemEvent));
//                 if ( HasSingleton<FlowFieldData>() )
//                 {
//                     var TempbufferDestinationsentity = GetSingletonEntity<CellDestinationBuffer>();
//                     tempDestinatebuffo = GetBuffer<CellDestinationBuffer>(TempbufferDestinationsentity);
//
//                     SetSingleton<FlowFieldData>(tempDestination);
//                     SetSingleton<CellBDLayerToCalc>(new CellBDLayerToCalc { intVal = 0 });
//                 }
//                 else
//                 {
//                     return;
//                 }
//
//                 int2 treenumb    = new int2(103, 129);    //TODO This is too damn high for some reason
//                 var  tempbuffeee = tempDestinatebuffo[0]; //TODO THis just sets the distination I did not think it would work 
//                 tempbuffeee.Destination = treenumb;
//                 tempDestinatebuffo[0]   = tempbuffeee;
//
//                 // if ( !HasSingleton<CalcintegrationFieldEvent>() )
//                 // {
//                 //     EntityManager.CreateEntity(typeof(CalcintegrationFieldEvent));
//                 // }
//             }
//
//             if ( tempInput.calcOtherLayer )
//             {
//                 if ( HasSingleton<FlowFieldData>() )
//                 {
//                     var TempbufferDestinationsentity = GetSingletonEntity<CellDestinationBuffer>();
//
//                     tempDestinatebuffo = GetBuffer<CellDestinationBuffer>(TempbufferDestinationsentity);
//
//                     SetSingleton<FlowFieldData>(tempDestination);
//
//                     //var Layertocalc = EntityManager.CreateEntity(typeof(CellBDLayerToCalc));
//                     //var layertocalc = GetSingleton<CellBDLayerToCalc>();
//                     SetSingleton<CellBDLayerToCalc>(new CellBDLayerToCalc { intVal = 1 });
//                     //layertocalc.intVal = 1;
//                 }
//                 else
//                 {
//                     return;
//                 }
//
//                 int2 secondtreenumb = new int2(316, 316);
//
//                 var tempbufftwoo = tempDestinatebuffo[1];
//                 tempbufftwoo.Destination = secondtreenumb;
//                 tempDestinatebuffo[1]    = tempbufftwoo;
//
//                 // if ( !HasSingleton<CalcintegrationFieldEvent>() )
//                 // {
//                 //     EntityManager.CreateEntity(typeof(CalcintegrationFieldEvent));
//                 // }
//
//                 var tempobject = tempMoveObject.TestObjectPrefab;
//
//                 NativeArray<Entity> dudees = new NativeArray<Entity>(500, Allocator.Temp);
//                 EntityManager.Instantiate(tempobject, dudees);
//                 // EntityManager.AddComponent<TestMoveojbectTag>(dudees);
//                 EntityManager.AddComponent<CellBDLayer>(dudees);
//                 for ( int i = 0; i < 500; i++ )
//                 {
//                     int    xrandnumpos  = UnityEngine.Random.Range(0, 5);
//                     int    zrandnumpos  = UnityEngine.Random.Range(0, 5);
//                     float3 thecreatepos = new float3(xrandnumpos, 0, zrandnumpos);
//
//                     EntityManager.SetComponentData<Translation>(dudees[i], new Translation { Value  = thecreatepos });
//                     EntityManager.SetComponentData<CellBDLayer>(dudees[i], new CellBDLayer { intVal = 1 });
//                 }
//
//                 dudees.Dispose();
//             }
//         }
//
//         public static int2 GetCellIndexFromWorldPos(float3 worldPos, int2 gridSize, float cellDiameter)
//         {
//             float percentX = worldPos.x / (gridSize.x * cellDiameter);
//             float percentY = worldPos.z / (gridSize.y * cellDiameter);
//
//             percentX = math.clamp(percentX, 0f, 1f);
//             percentY = math.clamp(percentY, 0f, 1f);
//
//             int2 cellIndex = new int2 { x = math.clamp((int)math.floor((gridSize.x) * percentX), 0, gridSize.x - 1), y = math.clamp((int)math.floor((gridSize.y) * percentY), 0, gridSize.y - 1) };
//
//             return cellIndex;
//         }
//     }
//
//     [AlwaysUpdateSystem]
//     public class PlayerInputStuffSystem : SystemBase, PlayerInputActions.IPlayerControlsActions
//     {
//         public PlayerInputActions playerinputacts;
//
//         private float2 inputMove;
//
//         private float2 inputLook;
//
//         private bool inputFire;
//
//         private bool startfield;
//
//         private bool addlayer;
//
//         private bool Calcotherlayer;
//
//         //public NativeList<float> FireInputs;
//
//         protected override void OnCreate()
//         {
//             base.OnCreate();
//
//             playerinputacts = new PlayerInputActions();
//             playerinputacts.PlayerControls.SetCallbacks(this);
//             //FireInputs = new NativeList<float>(Allocator.Persistent);
//         }
//
//         protected override void OnDestroy()
//         {
//             base.OnDestroy();
//             //if (FireInputs.IsCreated)
//             {
//                 //FireInputs.Dispose();
//             }
//         }
//
//         protected override void OnStartRunning()
//         {
//             base.OnStartRunning();
//
//             playerinputacts.Enable();
//
//             if ( HasSingleton<PlayerInput>() ) return;
//
//             EntityManager.CreateEntity(typeof(PlayerInput));
//         }
//
//         protected override void OnUpdate()
//         {
//             var  move         = inputMove;
//             bool fire         = inputFire;
//             bool stootfield   = startfield;
//             bool oodlayer     = addlayer;
//             bool calcoddlayer = Calcotherlayer;
//
//             Entities.ForEach((ref PlayerInput input) =>
//                              {
//                                  input.moveInput = move;
//                                  //Debug.Log(input.moveInput);
//                                  //input.jumpInput = jump;
//                                  //input.lookInput = look;
//                                  input.fireInput       = fire;
//                                  input.startFloodField = stootfield;
//                                  input.addLayer        = oodlayer;
//                                  input.calcOtherLayer  = calcoddlayer;
//                              }).Run();
//
//             inputFire      = false;
//             startfield     = false;
//             addlayer       = false;
//             Calcotherlayer = false;
//         }
//
//         protected override void OnStopRunning()
//         {
//             base.OnStartRunning();
//
//             playerinputacts.Disable();
//         }
//
//         public void OnMove(InputAction.CallbackContext context)
//         {
//             inputMove = context.ReadValue<Vector2>();
//
//             //Debug.Log(context.valueType.Name.ToString());
//         }
//
//         public void OnFire(InputAction.CallbackContext context)
//         {
//             var tempval = context.ReadValue<float>();
//             inputFire = tempval == 1 ? inputFire = true : inputFire = false;
//         }
//
//         public void OnStartFlowfield(InputAction.CallbackContext context)
//         {
//             var anothertempval = context.ReadValue<float>();
//             startfield = anothertempval == 1 ? startfield = true : startfield = false;
//         }
//
//         public void OnAddNewLayer(InputAction.CallbackContext context)
//         {
//             var anotheothertempval = context.ReadValue<float>();
//             addlayer = anotheothertempval == 1 ? addlayer = true : addlayer = false;
//         }
//
//         public void OnCalcOtherLayer(InputAction.CallbackContext context)
//         {
//             var otherthee = context.ReadValue<float>();
//             Calcotherlayer = otherthee == 1 ? Calcotherlayer = true : Calcotherlayer = false;
//         }
//     }
// }