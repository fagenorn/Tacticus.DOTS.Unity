namespace Sandbox.ECS.KNN
{
    public static class HeapUtils
    {
        public static int Parent(int index) { return index / 2; }

        public static int Left(int index) { return index * 2; }

        public static int Right(int index) { return index * 2 + 1; }
    }
}