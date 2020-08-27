using System.Numerics;

namespace Sakazuki
{
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 Uv0;
        public Vector2? Uv1;
        public Vector2? Uv2;
        public Vector2? Uv3;
        public int[] BoneIndices;
        public float[] BoneWeights;

        public int UvLayerCount => Uv3.HasValue ? 4 : Uv2.HasValue ? 3 : Uv1.HasValue ? 2 : 1;
    }
}