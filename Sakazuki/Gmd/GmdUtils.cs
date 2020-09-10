using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Sakazuki.Gmd
{
    public static class GmdUtils
    {
        private static Dictionary<string, int> SHADER_TO_FLAGS = new Dictionary<string, int>
        {
            {"sd_d1dzt", 1},
            {"sd_c1dzt[hair][ao][makeup", 2},
            {"sd_d1dzt_m2t[skin", 1},
            {"sd_o1dzt[fur", 1},
            {"sd_d1dzt[skin", 1},
            {"sd_o1dztt_m2t[skin", 3},
            {"sd_p1dzt[hair][vcol][ao", 2},
            {"sd_b1dzt[glass][ref", 1},
            {"sd_c1dzt[hair", 1},
            {"sd_o1dzt_m2dzt[rough", 1},
            {"sd_o1dzt_m2t[skin", 1},
            {"sd_o1dzt[mouth", 1},
            {"sd_c1dzt", 1},
            {"sd_b1dzt[makeup", 2},
            {"sd_b1dzt", 1},
            {"sd_d1d", 0},
            {"sd_o1dzt[skin", 1},
            {"sd_o1dzt_m2t", 1},
            {"sd_o1dzt_m2dzt[dedit", 1},
            {"sd_c1dzt[hair][vcol][ao", 2},
            {"sd_c1dzt[hair][vcol][ao][sss", 2},
            {"sd_o1dzt", 1},
            {"sd_o1dzt[hair][dedit", 1},
            {"sd_d1dzt_m2dzt", 1},
            {"sd_p1dzt", 1},
            {"sd_c1dztf[hair][dedit", 1},
            {"sd_c1dzt[glass][ref", 1},
            {"sd_d1dzt[eye][makeup", 1},
            {"sd_d1dzt[skin][makeup", 2},
            {"sd_o1dzt_m2dzt", 1},
            {"sd_o1dzt[eye", 1},
            {"sd_b1dz[glass", 0},
            {"sd_o1dztt_m2dzt", 3}
        };

        /// <summary>
        /// Until I figured out what those flags actually do...
        /// </summary>
        /// <param name="shaderName"></param>
        /// <returns></returns>
        public static int GetFlagCount(string shaderName)
        {
            if (SHADER_TO_FLAGS.TryGetValue(shaderName, out var count))
            {
                return count;
            }

            return 1;
        }

        public static void PrepareDirectoryForTextures(string directory)
        {
            // Recreate
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(directory);
        }

        public static GmdFile.BoneTransform? GetParentBone(GmdFile.BoneTransform bt, GmdFile.BoneTransform[] boneTransforms)
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

        public static GmdFile.BoneTransform[] GetChildren(GmdFile.BoneTransform bt, GmdFile.BoneTransform[] boneTransforms)
        {
            var children = new List<GmdFile.BoneTransform>();

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