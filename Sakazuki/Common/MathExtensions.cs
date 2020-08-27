using System.Numerics;

namespace Sakazuki.Common
{
    public static class MathExtensions
    {
        public static Vector3 WithY(this Vector3 v, float value)
        {
            return new Vector3(v.X, value, v.Z);
        }
    }
}