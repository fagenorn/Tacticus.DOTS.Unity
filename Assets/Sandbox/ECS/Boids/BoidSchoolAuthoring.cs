using System.Collections.Generic;

using Unity.Entities;

using UnityEngine;

namespace Sandbox.ECS.Boids
{
    [AddComponentMenu("Boids/BoidSchool")]
    [ConverterVersion("macton", 4)]
    public class BoidSchoolAuthoring : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        public int Count;

        public float InitialRadius;

        public GameObject Prefab;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) { dstManager.AddComponentData(entity, new BoidSchool { Prefab = conversionSystem.GetPrimaryEntity(Prefab), Count = Count, InitialRadius = InitialRadius }); }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs) { referencedPrefabs.Add(Prefab); }
    }
}