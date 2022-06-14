using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;
using UnityEngine.VFX;

namespace Sandbox.ECS.CastleWars
{
    public class BatchVisualEffectHandler
    {
        // Pixels:
        // ==================================
        // 1.xyz = spawn position
        // 1.w = distance
        // 2.xyz = spawn rotation
        // 2.w = intensity
        // ==================================
        public static int PixelsPerVFXInstance = 3;

        public static int ManualSpawnEvent = Shader.PropertyToID("OnPlay");

        public static int SpawnTextureAttribute = Shader.PropertyToID("STex");

        public static int SpawnCountAttribute = Shader.PropertyToID("SCount");

        public static int CameraPositionAttribute = Shader.PropertyToID("CamPos");

        public VisualEffect Graph;

        public Texture2D SpawnTexture;

        public int SimultaneousVFXSpawnCapacity;

        public int SpawnTextureDimensions;

        public int SpawnTexturePixelCount;

        private int _newVFXCount = 0;

        private int _writtenPixelsCount = 0;

        private NativeArray<float4> _tmpPixels;

        public BatchVisualEffectHandler(VFXManagerSystem manager, BatchVFXParameters parameters)
        {
            GameObject BulletHitVFXGraphEntity = GameObject.Instantiate(parameters.GraphPrefab);
            Graph = BulletHitVFXGraphEntity.GetComponent<VisualEffect>();

            SimultaneousVFXSpawnCapacity = parameters.MaxSimultaneousSpawnedVFX;
            SpawnTextureDimensions       = math.ceilpow2((int)math.ceil(math.sqrt(SimultaneousVFXSpawnCapacity * PixelsPerVFXInstance)));
            SpawnTexturePixelCount       = SpawnTextureDimensions * SpawnTextureDimensions;

            SpawnTexture = new Texture2D(SpawnTextureDimensions, SpawnTextureDimensions, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
            Graph.SetTexture(SpawnTextureAttribute, SpawnTexture);

            _tmpPixels = SpawnTexture.GetRawTextureData<float4>();
        }

        public void BeforeWriteToTexture()
        {
            _newVFXCount        = 0;
            _writtenPixelsCount = 0;
        }

        public void TryWriteSpawnParamsToTexture(BatchVFXRequest vfxRequest)
        {
            if ( _newVFXCount < SimultaneousVFXSpawnCapacity && _writtenPixelsCount < (SpawnTexturePixelCount - PixelsPerVFXInstance) )
            {
                _tmpPixels[_writtenPixelsCount]     = new float4(vfxRequest.Position.x, vfxRequest.Position.y, vfxRequest.Position.z, vfxRequest.Distance);
                _tmpPixels[_writtenPixelsCount + 1] = new float4(vfxRequest.Rotation.x, vfxRequest.Rotation.y, vfxRequest.Rotation.z, vfxRequest.Intensity);
                _tmpPixels[_writtenPixelsCount + 2] = new float4(vfxRequest.Target.x, vfxRequest.Target.y, vfxRequest.Target.z, 0);

                _writtenPixelsCount += PixelsPerVFXInstance;
                _newVFXCount++;
            }
        }

        public void AfterWriteToTexture(float3 camPos, float deltaTime)
        {
            Graph.SetVector3(CameraPositionAttribute, camPos);

            if ( _newVFXCount > 0 )
            {
                Graph.SetInt(SpawnCountAttribute, _newVFXCount);

                SpawnTexture.Apply();
                Graph.SetTexture(SpawnTextureAttribute, SpawnTexture);
                
                Graph.SendEvent(ManualSpawnEvent);
                Graph.Stop();
                Graph.Play();

                var x = SpawnTexture.GetPixel(0, 0,0).r;
            }
        }
    }
}