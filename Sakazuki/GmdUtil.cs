using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Sakazuki
{
    public static class GmdUtil
    {
        public static GmdFile2.BoneTransform? GetParentBone(GmdFile2.BoneTransform bt, GmdFile2.BoneTransform[] boneTransforms)
        {
            var searchIdx = bt.BoneNo;
            for (int i = 0; i < boneTransforms.Length; i++)
            {
                if (boneTransforms[i].NextChildIndex == searchIdx)
                {
                    return boneTransforms[i];
                }

                if (boneTransforms[i].NextSiblingIndex == searchIdx)
                {
                    searchIdx = boneTransforms[i].BoneNo;
                    i = 0;
                }
            }

            return null;
        }

        public static GmdFile2.BoneTransform[] GetChildren(GmdFile2.BoneTransform bt, GmdFile2.BoneTransform[] boneTransforms)
        {
            var children = new List<GmdFile2.BoneTransform>();

            if (bt.NextChildIndex >= 0)
            {
                var siblingIndex = bt.NextChildIndex;

                while (siblingIndex >= 0)
                {
                    children.Add(boneTransforms[siblingIndex]);
                    siblingIndex = boneTransforms[siblingIndex].NextSiblingIndex;
                }
            }

            return children.ToArray();
        }


        public static Vector3[] ReadVertices(BinaryReader br, int count)
        {
            var vertices = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = ReadVector3(br);
            }

            return vertices;
        }

        public static Vector3 ReadVector3(BinaryReader br)
        {
            return new Vector3(
                br.ReadSingle(),
                br.ReadSingle(),
                br.ReadSingle()
            );
        }

        public static void WriteVector3(Vector3 vector, BinaryWriter bw)
        {
            bw.Write(vector.X);
            bw.Write(vector.Y);
            bw.Write(vector.Z);
        }

        public static Vector3 ReadNormal(BinaryReader br)
        {
            return new Vector3(
                (br.ReadByte() - 128) / 128.0f,
                (br.ReadByte() - 128) / 128.0f,
                (br.ReadByte() - 128) / 128.0f
            );
        }

        public static void WriteNormal(Vector3 normal, BinaryWriter bw)
        {
            bw.Write((byte) (normal.X * 128.0f + 128));
            bw.Write((byte) (normal.Y * 128.0f + 128));
            bw.Write((byte) (normal.Z * 128.0f + 128));
        }
    }
}