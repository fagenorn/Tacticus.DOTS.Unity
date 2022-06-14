using System;

using Sandbox.ECS.CastleWars;

using Unity.Entities;
using Unity.Transforms;

namespace Sandbox.ECS.Boids
{
    [Serializable]
    [WriteGroup(typeof(LocalToWorld))]
    public struct Boid : ISharedComponentData
    {
        public float CellRadius;

        public float SeparationWeight;

        public float AlignmentWeight;

        public float TargetWeight;

        public float ObstacleAversionDistance;

        public float MoveSpeed;

        public TeamEnum Team;
    }
}