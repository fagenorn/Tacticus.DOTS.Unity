using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Sandbox.ECS.KNN
{
    public static unsafe class UnsafeUtilityEx
    {
        public static T* AllocArray<T>(int length, Allocator allocator) where T : unmanaged { return (T*)UnsafeUtility.Malloc(length * UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), allocator); }
    }
}