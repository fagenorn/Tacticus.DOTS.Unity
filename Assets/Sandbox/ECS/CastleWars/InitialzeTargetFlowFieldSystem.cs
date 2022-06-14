using System.Collections.Generic;

using Sandbox.ECS.FlowField;

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Sandbox.ECS.CastleWars
{
    public partial class InitialzeTargetFlowFieldSystem : SystemBase
    {
        private readonly List<MainTargetComponent> _mainTargets = new List<MainTargetComponent>(2);

        protected override void OnCreate() { RequireSingletonForUpdate<FlowFieldControllerData>(); }

        protected override void OnUpdate()
        {
            var flowFieldControllerData = GetSingleton<FlowFieldControllerData>();

            EntityManager.GetAllUniqueSharedComponentData(_mainTargets);

            for ( var mainTargetIndex = 0; mainTargetIndex < _mainTargets.Count; mainTargetIndex++ )
            {
                var settings = _mainTargets[mainTargetIndex];

                Entities
                    .WithSharedComponentFilter(settings)
                    .WithName("GenerateTargetFlowField")
                    .WithStructuralChanges()
                    .WithNone<FlowFieldData>()
                    .ForEach((Entity entity, in Translation translation) =>
                             {
                                 var gridSize   = flowFieldControllerData.gridSize;
                                 var cellRadius = flowFieldControllerData.cellRadius;

                                 var flowFieldData    = new FlowFieldData { gridSize               = gridSize, cellRadius = cellRadius };
                                 var newFlowFieldData = new NewFlowFieldData { isExistingFlowField = false };

                                 var percentX = translation.Value.x / (gridSize.x * cellRadius * 2);
                                 var percentY = translation.Value.z / (gridSize.y * cellRadius * 2);

                                 percentX = math.clamp(percentX, 0f, 1f);
                                 percentY = math.clamp(percentY, 0f, 1f);

                                 flowFieldData.TargetGridIndex = new int2 { x = math.clamp((int)math.floor(gridSize.x * percentX), 0, gridSize.x - 1), y = math.clamp((int)math.floor(gridSize.y * percentY), 0, gridSize.y - 1) };

                                 EntityManager.AddComponent<FlowFieldData>(entity);
                                 EntityManager.AddComponent<NewFlowFieldData>(entity);
                                 EntityManager.SetComponentData(entity, flowFieldData);
                                 EntityManager.SetComponentData(entity, newFlowFieldData);
                             })
                    .Run();
            }

            _mainTargets.Clear();
        }
    }
}