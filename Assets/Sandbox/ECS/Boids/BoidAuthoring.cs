using Sandbox.ECS.CastleWars;

using UnityEngine;

namespace Sandbox.ECS.Boids
{
    [AddComponentMenu("Boids/Boid")]
    public class BoidAuthoring : MonoBehaviour
    {
        public float CellRadius = 8.0f;

        public float SeparationWeight = 1.0f;

        public float AlignmentWeight = 1.0f;

        public float TargetWeight = 2.0f;

        public float ObstacleAversionDistance = 30.0f;

        public float MoveSpeed = 25.0f;

        public TeamEnum Team;
    }
}