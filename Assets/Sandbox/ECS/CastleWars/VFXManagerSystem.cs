using System.Collections.Generic;

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using UnityEngine;
using UnityEngine.VFX;

namespace Sandbox.ECS.CastleWars
{
    public partial class VFXManagerSystem : SystemBase
    {
        public List<BatchVisualEffectHandler> BatchVFXManagers;

        public NativeQueue<BatchVFXRequest> BatchVFXRequests;

        private NativeList<JobHandle> _dependenciesToComplete;

        private GameDataManaged _gameDataManaged;

        public static int ManualSpawnEvent = Shader.PropertyToID("OnPlay");

        private Camera _mainCamera;

        protected override void OnCreate()
        {
            _gameDataManaged = GameDataManaged.Load();

            BatchVFXRequests = new NativeQueue<BatchVFXRequest>(Allocator.Persistent);

            BatchVFXManagers        = new List<BatchVisualEffectHandler>();
            _dependenciesToComplete = new NativeList<JobHandle>(Allocator.Persistent);

            // Batch VFX managers
            {
                for ( int i = 0; i < _gameDataManaged.BatchVFX.Count; i++ )
                {
                    BatchVFXManagers.Add(new BatchVisualEffectHandler(this, _gameDataManaged.BatchVFX[i]));
                }
            }

            // Alloc tmp pixels array of the max required length
            int maxPixelCount = 1;
            foreach ( var item in BatchVFXManagers )
            {
                maxPixelCount = math.max(maxPixelCount, item.SpawnTexturePixelCount);
            }
        }

        protected override void OnStartRunning() { _mainCamera = Camera.main; }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            BatchVFXRequests.Dispose();
            _dependenciesToComplete.Dispose();
        }

        public void AddDependencyToComplete(JobHandle dep) { _dependenciesToComplete.Add(dep); }

        protected override void OnUpdate()
        {
            JobHandle.CombineDependencies(_dependenciesToComplete.AsArray()).Complete();
            _dependenciesToComplete.Clear();

            float3 camPos = _mainCamera.transform.position;
            // if ( HasSingleton<MainEntityCamera>() )
            // {
            //     Entity mainCameraEntity = GetSingletonEntity<MainEntityCamera>();
            //     camPos = GetComponent<LocalToWorld>(mainCameraEntity).Position;
            // }

            // Batch VFX update
            {
                for ( int i = 0; i < BatchVFXManagers.Count; i++ )
                {
                    BatchVFXManagers[i].BeforeWriteToTexture();
                }

                while ( BatchVFXRequests.TryDequeue(out BatchVFXRequest vfxRequest) )
                {
                    BatchVFXManagers[vfxRequest.VFXId].TryWriteSpawnParamsToTexture(vfxRequest);
                }

                for ( int i = 0; i < BatchVFXManagers.Count; i++ )
                {
                    BatchVFXManagers[i].AfterWriteToTexture(camPos, Time.DeltaTime);
                }

                BatchVFXRequests.Clear();
            }

            // VFX Play
            // {
            //     Entities
            //         .WithoutBurst()
            //         .ForEach((Entity entity, VisualEffect vfx, ref VFXPlayRequest vFXPlay) =>
            //                  {
            //                      if ( vFXPlay.Play )
            //                      {
            //                          vfx.SendEvent(ManualSpawnEvent);
            //                          vFXPlay.Play = false;
            //                      }
            //                  }).Run();
            // }
        }
    }

    public struct VFXPlayRequest : IComponentData
    {
        public bool Play;
    }

    public class GameDataManaged
    {
        public List<BatchVFXParameters> BatchVFX { get; set; }

        public static GameDataManaged Load()
        {
            var gameDataManaged = new GameDataManaged();
            gameDataManaged.BatchVFX = new List<BatchVFXParameters>(GameObject.FindObjectsOfType<BatchVFXParameters>());

            return gameDataManaged;
        }
    }

    public struct BatchVFXRequest
    {
        public float3 Position { get; set; }

        public float3 Target { get; set; }

        public Quaternion Rotation { get; set; }

        public float Distance { get; set; }

        public float Intensity { get; set; }

        public int VFXId { get; set; }
    }
}