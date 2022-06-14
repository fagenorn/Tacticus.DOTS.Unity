namespace Sandbox.ECS.KNN
{
    public struct KdNode
    {
        public KdNodeBounds Bounds;

        public int Start;

        public int End;

        public int PartitionAxis;

        public float PartitionCoordinate;

        public int NegativeChildIndex;

        public int PositiveChildIndex;

        public int Count => End - Start;

        public bool Leaf => PartitionAxis == -1;
    }
}