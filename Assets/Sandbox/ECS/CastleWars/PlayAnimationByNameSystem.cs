using TAO.VertexAnimation;

using Unity.Collections;
using Unity.Entities;

namespace Sandbox.ECS.CastleWars
{
// Example system to set the animation by name.
// This could be in a state of a state machine system.
    [UpdateBefore(typeof(VA_AnimatorSystem))]
    public partial class PlayAnimationByNameSystem : SystemBase
    {
        public FixedString64Bytes animationName = "new ModelBaker_RunForward1";

        protected override void OnUpdate()
        {
            float deltaTime = UnityEngine.Time.deltaTime;
            var   an        = animationName;

            Entities
                .ForEach((Entity entity, ref VA_AnimatorComponent ac) =>
                             {
                                 // Get the animation lib data.
                                 ref VA_AnimationLibraryData animationsRef = ref ac.animationLibrary.Value;

                                 // Set the animation index on the AnimatorComponent to play this animation.
                                 ac.animationIndex = 0; // VA_AnimationLibraryUtils.GetAnimation(ref animationsRef, an);

                                 // 'Play' the actual animation.
                                 ac.animationTime += deltaTime * animationsRef.animations[ac.animationIndex].frameTime;

                                 if ( ac.animationTime > animationsRef.animations[ac.animationIndex].duration )
                                 {
                                     // Set time. Using the difference to smoothen out animations when looping.
                                     ac.animationTime -= animationsRef.animations[ac.animationIndex].duration;
                                 }
                             }).ScheduleParallel();
        }
    }
}