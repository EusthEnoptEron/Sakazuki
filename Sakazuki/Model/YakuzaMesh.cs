using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Sakazuki.Common;
using Sakazuki.Gmd;
using Sakazuki.Par;

namespace Sakazuki.Model
{
    public partial class YakuzaMesh
    {
        public string Name { get; set; }
        public Mesh[] Meshes { get; set; }
        public Material[] Materials { get; set; }
        public string[] Textures { get; set; }
        public Bone[] Bones { get; set; }

        private YakuzaMesh()
        {
        }


        private string GetSubmeshName(string name, List<string> nameList)
        {
            var count = nameList.Count(el => el == name);
            nameList.Add(name);

            if (count > 0)
            {
                name += "." + count.ToString().PadLeft(2, '0');
            }

            return name;
        }

        private void LoadGmdFile(GmdFile gmdFile)
        {
            Name = gmdFile.Name;

            Textures = gmdFile.Textures.Select(tex => tex.Name).ToArray();

            Materials = gmdFile.Materials.Select(mat =>
            {
                return new Material
                {
                    Id = mat.Id,
                    Shader = gmdFile.Shaders[mat.ShaderIndex].ToString(),
                    Textures = mat.TextureIndices.Select(texIdx =>
                    {
                        if (texIdx >= 0 && texIdx < Textures.Length)
                        {
                            return Textures[texIdx];
                        }

                        return null;
                    }).ToArray()
                };
            }).ToArray();

            var bones = new Bone[gmdFile.BoneTransforms.Length];
            foreach (var bt in gmdFile.BoneTransforms)
            {
                if (GmdUtils.GetParentBone(bt, gmdFile.BoneTransforms) == null)
                {
                    LoadBone(bt, null, gmdFile, bones);
                }
            }

            Bones = bones.ToArray();


            var nameList = new List<string>();
            var meshes = new List<Mesh>();
            foreach (var gmdMesh in gmdFile.Meshes)
            {
                var mesh = new Mesh();
                meshes.Add(mesh);

                // Console.WriteLine($"------------");

                foreach (var gmdSub in gmdFile.Submeshes.Where(s => s.MeshIndex == gmdMesh.MeshId))
                {
                    var submesh = new Submesh();
                    mesh.Submeshes.Add(submesh);

                    submesh.Id = gmdSub.Id;
                    submesh.Name = GetSubmeshName(Bones[gmdSub.BoneNo].Name, nameList);
                    submesh.Transform = Bones[gmdSub.BoneNo].WorldMatrix;
                    submesh.Material = Materials[gmdSub.MaterialIndex];
                    submesh.Vertices = ReadVertices(gmdSub, gmdMesh, gmdFile);
                    submesh.Triangles = new int[gmdSub.IndicesCount / 3, 3];

                    // Console.WriteLine(submesh.Name.PadRight(25, ' ')
                    //                   + submesh.Material.Shader.PadRight(25, ' ')
                    //                   + $"{gmdMesh.HasFlag1} {gmdMesh.HasFlag2} {gmdMesh.HasFlag3}");

                    var offset = gmdSub.IndicesOffset;
                    for (int i = 0; i < gmdSub.IndicesCount / 3; i++)
                    {
                        submesh.Triangles[i, 0] = gmdFile.Indices[offset + i * 3].Value - gmdSub.BufferOffset2;
                        submesh.Triangles[i, 1] = gmdFile.Indices[offset + i * 3 + 1].Value - gmdSub.BufferOffset2;
                        submesh.Triangles[i, 2] = gmdFile.Indices[offset + i * 3 + 2].Value - gmdSub.BufferOffset2;
                    }
                }
            }

            Meshes = meshes.ToArray();
        }


        private Bone LoadBone(GmdFile.BoneTransform boneTransform, Bone parent, GmdFile file, Bone[] boneTable)
        {
            var bone = new Bone();
            bone.Id = boneTransform.BoneNo;
            bone.Name = file.BoneNames[boneTransform.BoneNameIndex].ToString();
            bone.Position = boneTransform.LocalPosition;
            bone.Rotation = boneTransform.LocalRotation;
            bone.Scale = boneTransform.Scale;
            bone.Parent = parent;
            boneTable[boneTransform.BoneNo] = bone;

            bone.Children.AddRange(GmdUtils.GetChildren(boneTransform, file.BoneTransforms).Select(bt => LoadBone(bt, bone, file, boneTable)));

            return bone;
        }

        public GmdFile ToGmdFile()
        {
            var file = new GmdFile();

            file.Name = Name;
            // TEXTURES

            var textures = Textures.Concat(new string[] {"_dummy_rd", "dummy_nmap", "dummy_black", "dummy_gray", "dummy_white", "default_z", "noise"})
                .Distinct().ToArray();

            file.Textures = textures
                .Select(GmdFile.HashedName.FromString)
                .ToArray();

            var shaders = Materials.Select(mat => mat.Shader).Distinct().ToArray();
            var hlSubmeshes = Meshes.SelectMany(m => m.Submeshes).OrderBy(sm => sm.Id).ToArray();

            // MATERIALS
            file.Materials = Materials.Select(mat =>
            {
                var material = new GmdFile.Material()
                {
                    Id = mat.Id,
                    TextureIndices =
                        mat.Textures.Select(tex => (int) (ushort) Array.IndexOf(textures, tex))
                            .Concat(Enumerable.Repeat((int) ushort.MaxValue, 8 - mat.Textures.Length)).ToArray(),
                    ShaderIndex = (uint) Array.IndexOf(shaders, mat.Shader),
                    SubmeshIndex = (uint) Array.FindIndex(hlSubmeshes, m => m.Material == mat)
                };

                /* 
                * 3 => rd (sd_o1dzt_m2dzt) / tr (skin) / dummy_white
                    4 => rm / default_z [can be default_z, strange things happen when null]
                    5 => rt / noise [can be noise, too smooth when null]
                    6 => rs [can be null]
                    7 => 
*/

                material.TextureIndices[0] = material.TextureIndices[0] >= 255 ? Array.IndexOf(textures, "dummy_black") : material.TextureIndices[0];
                material.TextureIndices[1] = material.TextureIndices[1] >= 255 ? Array.IndexOf(textures, "default_z") : material.TextureIndices[1];
                material.TextureIndices[2] = material.TextureIndices[2] >= 255 ? Array.IndexOf(textures, "dummy_nmap") : material.TextureIndices[2];
                material.TextureIndices[3] = material.TextureIndices[3] >= 255 ? Array.IndexOf(textures, "dummy_white") : material.TextureIndices[3];
                material.TextureIndices[4] = material.TextureIndices[4] >= 255 ? Array.IndexOf(textures, "default_z") : material.TextureIndices[4];
                material.TextureIndices[5] = material.TextureIndices[5] >= 255 ? Array.IndexOf(textures, "noise") : material.TextureIndices[5];
                material.TextureIndices[6] = material.TextureIndices[6] >= 255 ? ushort.MaxValue : material.TextureIndices[6]; //ushort.MaxValue;
                material.TextureIndices[7] = material.TextureIndices[7] >= 255 ? ushort.MaxValue : material.TextureIndices[7]; //ushort.MaxValue;
                // ushort.MaxValue

                material.Initialize(mat.Shader == GmdUtils.TRANSPARENCY_MAT);

                return material;
            }).ToArray();

            // SHADERS
            file.Shaders = shaders.Select(shader => GmdFile.HashedName.FromString(shader)).ToArray();

            // MESHES
            var meshes = new List<GmdFile.Mesh>();
            var vertexBuffers = new List<byte[]>();
            var submeshes = new List<GmdFile.Submesh>();
            var indices = new List<GmdFile.Index>();
            var boneIndices = new List<GmdFile.BoneIndex>();

            for (int i = 0; i < Meshes.Length; i++)
            {
                var mesh = Meshes[i];
                var gmdMesh = new GmdFile.Mesh();
                var firstVertex = mesh.Submeshes.First().Vertices.First();

                gmdMesh.Initialize();
                gmdMesh.MeshId = i;
                gmdMesh.Count = mesh.Submeshes.Sum(m => m.Vertices.Length);
                gmdMesh.Offset = vertexBuffers.Sum(b => b.Length);
                gmdMesh.UvLayers = firstVertex.UvLayerCount;
                gmdMesh.HasBones = firstVertex.BoneIndices != null;
                gmdMesh.FlagCount = GmdUtils.GetFlagCount(mesh.Submeshes.First().Material.Shader);

                using var buffer = new MemoryStream();
                using var writer = new BinaryWriter(buffer);

                Console.WriteLine("m" + i);
                int vertexCount = 0;
                for (int j = 0; j < mesh.Submeshes.Count; j++)
                {
                    var submesh = mesh.Submeshes[j];
                    var gmdSubmesh = new GmdFile.Submesh();
                    var boneIndexList = submesh.Vertices.SelectMany(v => v.BoneIndices ?? Enumerable.Empty<int>()).Distinct().ToList();

                    gmdSubmesh.Id = Array.IndexOf(hlSubmeshes, submesh);
                    gmdSubmesh.MeshIndex = i;
                    gmdSubmesh.IndicesCount = submesh.Triangles.GetLength(0) * 3;
                    gmdSubmesh.IndicesOffset = indices.Count;
                    gmdSubmesh.VertexCount = submesh.Vertices.Length;
                    gmdSubmesh.MaterialIndex = (int) submesh.Material.Id;
                    gmdSubmesh.BufferOffset2 = vertexCount;
                    gmdSubmesh.BoneIndexOffset = boneIndices.Count;
                    gmdSubmesh.BoneIndexCount = boneIndexList.Count;

                    gmdMesh.Stride = WriteVertices(writer, submesh, gmdMesh.FlagCount, boneIndexList);

                    boneIndexList.Insert(0, gmdSubmesh.BoneIndexCount); // Expects count in index list
                    vertexCount += submesh.Vertices.Length;

                    // Fill in indices
                    for (int y = 0; y < submesh.Triangles.GetLength(0); y++)
                    {
                        indices.Add(new GmdFile.Index() {Value = submesh.Triangles[y, 0] + gmdSubmesh.BufferOffset2});
                        indices.Add(new GmdFile.Index() {Value = submesh.Triangles[y, 1] + gmdSubmesh.BufferOffset2});
                        indices.Add(new GmdFile.Index() {Value = submesh.Triangles[y, 2] + gmdSubmesh.BufferOffset2});
                    }

                    boneIndices.AddRange(boneIndexList.Select(idx => new GmdFile.BoneIndex() {Value = idx}));

                    submeshes.Add(gmdSubmesh);
                }

                gmdMesh.Size = (int) buffer.Length;
                vertexBuffers.Add(buffer.ToArray());

                meshes.Add(gmdMesh);
            }

            file.Indices = indices.ToArray();
            file.Meshes = meshes.ToArray();
            file.Submeshes = submeshes.OrderBy(sm => sm.Id).ToArray();
            file.VertexBuffers = vertexBuffers.ToArray();
            file.BoneIndices = boneIndices.ToArray();
            file.BoneNames = Bones.Select(b => GmdFile.HashedName.FromString(b.Name)).ToArray();
            file.BoneTransforms = Bones.Select(bone =>
            {
                var bt = new GmdFile.BoneTransform();
                bt.Position = bone.WorldMatrix.Translation;
                bt.LocalPosition = bone.Position;
                bt.LocalRotation = bone.Rotation;
                bt.Scale = bone.Scale;
                bt.BoneNo = bt.BoneNo2 = bone.Id;
                bt.BoneNameIndex = bone.Id;

                bt.TransformIndex = -1;
                bt.NextSiblingIndex = -1;
                bt.NextChildIndex = -1;
                bt.Footer = new byte[16];

                if (bone.Name.Contains("[l0]"))
                {
                    bt.TransformIndex = 0;
                    bt.NodeType = GmdFile.BoneTransform.NODE_TYPE_TRANSFORM;
                }

                // If bone has children, set the nextChildIndex idx
                if (bone.Children.Count > 0)
                {
                    bt.NextChildIndex = bone.Children[0].Id;
                }

                // If bone has another sibling that comes after itself, set NextSiblingIndex 
                if (bone.Parent != null)
                {
                    var childIdx = bone.Parent.Children.IndexOf(bone);
                    if (childIdx >= 0 && childIdx + 1 < bone.Parent.Children.Count)
                    {
                        bt.NextSiblingIndex = bone.Parent.Children[childIdx + 1].Id;
                    }
                }

                return bt;
            }).ToArray();


            return file;
        }

        private Vertex[] ReadVertices(GmdFile.Submesh submesh, GmdFile.Mesh mesh, GmdFile file)
        {
            var vertices = new Vertex[submesh.VertexCount];
            var meshIndex = Array.IndexOf(file.Meshes, mesh);
            using var memoryStream = new MemoryStream(file.VertexBuffers[meshIndex]);
            using var bs = new BinaryReader(memoryStream);

            bs.BaseStream.Seek((submesh.BufferOffset1 + submesh.BufferOffset2) * mesh.Stride, SeekOrigin.Begin);

            for (int j = 0; j < submesh.VertexCount; j++)
            {
                var v = new Vertex();
                v.Position = GmdUtils.ReadVector3(bs);
                if (mesh.HasBones && file.BoneIndices.Length > 0)
                {
                    v.BoneWeights = new[]
                    {
                        bs.ReadByteAsFloat(),
                        bs.ReadByteAsFloat(),
                        bs.ReadByteAsFloat(),
                        bs.ReadByteAsFloat()
                    };

                    v.BoneIndices = new[]
                    {
                        file.BoneIndices[bs.ReadByte() + submesh.BoneIndexOffset + 1].Value,
                        file.BoneIndices[bs.ReadByte() + submesh.BoneIndexOffset + 1].Value,
                        file.BoneIndices[bs.ReadByte() + submesh.BoneIndexOffset + 1].Value,
                        file.BoneIndices[bs.ReadByte() + submesh.BoneIndexOffset + 1].Value
                    };
                }

                v.Normal = GmdUtils.ReadNormal(bs);
                bs.ReadByte();

                if (mesh.HasFlag1)
                {
                    // var a = bs.ReadByteAsFloat();
                    // var b = bs.ReadByteAsFloat();
                    // var c = bs.ReadByteAsFloat();
                    // var d = bs.ReadByteAsFloat();
                    var val = bs.ReadInt32();
                    // Console.WriteLine($"{a:0.00} / {b:0.00} / {c:0.00} / {d:0.00}");
                    // bs.ReadInt32();
                }

                if (mesh.HasFlag2)
                {
                    bs.ReadInt32();
                }

                if (mesh.HasFlag3)
                {
                    bs.ReadInt32();
                }

                for (int l = 0; l < mesh.UvLayers; l++)
                {
                    var uvs = new Vector2(Half.ToHalf(bs.ReadUInt16()), 1 - Half.ToHalf(bs.ReadUInt16()));
                    switch (l)
                    {
                        case 0:
                            v.Uv0 = uvs;
                            break;
                        case 1:
                            v.Uv1 = uvs;
                            break;
                        case 2:
                            v.Uv2 = uvs;
                            break;
                        case 3:
                            v.Uv3 = uvs;
                            break;
                        default:
                            Console.Error.WriteLine("Too many UV channels!");
                            break;
                    }
                }

                vertices[j] = v;
            }

            return vertices;
        }

        private int WriteVertices(BinaryWriter writer, Submesh submesh, int flagCount, List<int> boneIndices)
        {
            var startPosition = writer.BaseStream.Position;

            foreach (var v in submesh.Vertices)
            {
                GmdUtils.WriteVector3(v.Position, writer);

                if (v.BoneIndices != null)
                {
                    writer.Write((byte) (v.BoneWeights[0] * 255));
                    writer.Write((byte) (v.BoneWeights[1] * 255));
                    writer.Write((byte) (v.BoneWeights[2] * 255));
                    writer.Write((byte) (v.BoneWeights[3] * 255));

                    writer.Write((byte) (boneIndices.IndexOf(v.BoneIndices[0])));
                    writer.Write((byte) (boneIndices.IndexOf(v.BoneIndices[1])));
                    writer.Write((byte) (boneIndices.IndexOf(v.BoneIndices[2])));
                    writer.Write((byte) (boneIndices.IndexOf(v.BoneIndices[3])));
                }

                GmdUtils.WriteNormal(v.Normal, writer);
                writer.Write((byte) 0);

                for (int i = 0; i < flagCount; i++)
                {
                    writer.Write((byte) 255);
                    writer.Write((byte) 255);
                    writer.Write((byte) 255);
                    writer.Write((byte) 255);
                }

                // writer.Write(100);
                // writer.Write((byte)0);
                // if ((mesh.Format >> 12 & 7L) == 7L)
                // {
                //     writer.Write(0);
                // }
                //
                // if ((mesh.Format >> 21 & 7L) == 7L)
                // {
                //     writer.Write(0);
                // }
                //
                // if ((mesh.Format >> 24 & 7L) == 7L)
                // {
                //     writer.Write(0);
                // }
                writer.Write(Half.GetBits(new Half(v.Uv0.X)));
                writer.Write(Half.GetBits(new Half(1 - v.Uv0.Y)));

                if (v.Uv1.HasValue)
                {
                    writer.Write(Half.GetBits(new Half(v.Uv1.Value.X)));
                    writer.Write(Half.GetBits(new Half(1 - v.Uv1.Value.Y)));
                }

                if (v.Uv2.HasValue)
                {
                    writer.Write(Half.GetBits(new Half(v.Uv2.Value.X)));
                    writer.Write(Half.GetBits(new Half(1 - v.Uv2.Value.Y)));
                }

                if (v.Uv3.HasValue)
                {
                    writer.Write(Half.GetBits(new Half(v.Uv3.Value.X)));
                    writer.Write(Half.GetBits(new Half(1 - v.Uv3.Value.Y)));
                }
            }

            var endPosition = writer.BaseStream.Position;

            // Stride
            return (int) ((endPosition - startPosition) / submesh.Vertices.Length);
        }

        public void WriteGmd(Stream stream)
        {
            var gmd = ToGmdFile();
            gmd.Write(stream);
        }

        public byte[] ToGmdBytes()
        {
            using var stream = new MemoryStream();
            WriteGmd(stream);
            return stream.ToArray();
        }

        public static YakuzaMesh FromGmdFile(GmdFile GmdFile)
        {
            var mesh = new YakuzaMesh();
            mesh.LoadGmdFile(GmdFile);
            return mesh;
        }

        public static YakuzaMesh FromArchive(string name, ArchiveFile archive)
        {
            return FromGmdFile(GmdFile.FromArchive(name, archive));
        }

        public static YakuzaMesh FromGmdStream(Stream stream)
        {
            var gmd = GmdFile.FromStream(stream);
            return FromGmdFile(gmd);
        }

        public static YakuzaMesh FromGmdFile(string path)
        {
            return FromGmdStream(File.OpenRead(path));
        }

        private static bool EqualBones(Bone lhs, Bone rhs)
        {
            return lhs.Name == rhs.Name;
        }

        public void CopySkin(Bone[] skin, bool applyTransforms = true, bool fillUp = true)
        {
            var bonesClone = new List<Bone>();
            foreach (var bone in skin)
            {
                var otherBone = Array.Find(Bones, b => EqualBones(bone, b));
                if (otherBone != null)
                {
                    if (applyTransforms)
                    {
                        // Replace
                        otherBone.Position = bone.Position;
                        otherBone.Rotation = bone.Rotation;
                        otherBone.Scale = bone.Scale;
                    }

                    otherBone.Name = bone.Name;
                    bonesClone.Add(otherBone);
                }
                else
                {
                    Console.WriteLine($"Adding bone: {bone.Name}");
                    if (fillUp)
                    {
                        var newBone = new Bone()
                        {
                            Id = bonesClone.Count,
                            Name = bone.Name,
                            Rotation = Quaternion.Identity,
                            Scale = Vector3.One,
                            Position = Vector3.One
                        };
                        if (bone.Parent != null)
                        {
                            newBone.Parent = bonesClone.Find(b => b.Name == bone.Parent.Name);
                            newBone.Parent.Children.Add(newBone);
                        }

                        bonesClone.Add(newBone);
                    }
                    else
                    {
                        Console.Error.WriteLine("Bone not found: " + bone.Name);
                    }
                }
            }

            // Update weights
            var indexMapping = new Dictionary<int, int>();
            foreach (var bone in Bones)
            {
                var oldIndex = bone.Id;
                var newIndex = bonesClone.IndexOf(bone);
                bone.Id = newIndex;
                indexMapping[oldIndex] = newIndex;
            }

            foreach (var mesh in Meshes)
            {
                foreach (var submesh in mesh.Submeshes)
                {
                    for (int i = 0; i < submesh.Vertices.Length; i++)
                    {
                        submesh.Vertices[i].BoneIndices[0] = indexMapping[submesh.Vertices[i].BoneIndices[0]];
                        submesh.Vertices[i].BoneIndices[1] = indexMapping[submesh.Vertices[i].BoneIndices[1]];
                        submesh.Vertices[i].BoneIndices[2] = indexMapping[submesh.Vertices[i].BoneIndices[2]];
                        submesh.Vertices[i].BoneIndices[3] = indexMapping[submesh.Vertices[i].BoneIndices[3]];
                    }
                }
            }

            Bones = bonesClone.ToArray();
        }
    }
}