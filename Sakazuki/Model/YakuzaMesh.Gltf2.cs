using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Sakazuki.Common;
using Sakazuki.Gmd;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using AlphaMode = SharpGLTF.Materials.AlphaMode;

namespace Sakazuki.Model
{
    public partial class YakuzaMesh
    {
        private Regex MaterialRegex = new Regex(@"^(?<Material>.+)(?:[.][0-9]{2})?\{(?<Shader>[^{}]+)\}$");

        private string DecodeMaterialName(string materialName)
        {
            if (materialName == null) return null;

            var match = MaterialRegex.Match(materialName);
            if (match.Success)
            {
                return match.Groups["Material"].Value;
            }

            // Fallback
            return "Material";
        }

        private string DecodeShaderName(string materialName)
        {
            if (materialName == null) return null;

            var match = MaterialRegex.Match(materialName);
            if (match.Success)
            {
                return match.Groups["Shader"].Value;
            }

            // Fallback
            return GmdUtils.PBR_MAT;
        }

        private string EncodeMaterialName(string materialName, string shaderName, int count)
        {
            return $"{materialName}.{count.ToString().PadLeft(2, '0')}{{{shaderName}}}";
        }

        private MaterialBuilder InitializeMaterial(Material matDef, MaterialBuilder[] existingMaterials, ImageConverter converter)
        {
            var materialName = (matDef.DiffuseMap ?? "Material");
            int count = existingMaterials.Count(mat => DecodeMaterialName(mat?.Name) == materialName);
            var mat = new MaterialBuilder(EncodeMaterialName(materialName, matDef.Shader, count))
                .WithMetallicRoughnessShader();

            if (matDef.DiffuseMap != null)
            {
                var path = converter.GetImage(matDef.DiffuseMap, matDef.DetailMap, out var channels);
                if (path != null)
                {
                    mat.WithChannelImage(KnownChannel.BaseColor, path);
                }

                if (channels == 4)
                {
                    // mat.AlphaMode = AlphaMode.BLEND;
                    mat.WithAlpha(AlphaMode.MASK, 0.5f);
                }
            }

            if (matDef.NormalMap != null)
            {
                var path = converter.GetNormalImage(matDef.NormalMap);
                if (path != null)
                {
                    mat.WithNormal(path);
                }
            }

            // foreach (var texture in matDef.Textures)
            // {
            //     if (texture != null)
            //     {
            //         var path = converter.GetImage(texture, out _);
            //         if (path != null)
            //         {
            //             File.Copy(path,
            //                 @"D:\Program Files (x86)\Steam\steamapps\common\Yakuza Kiwami 2\data\chara_unpack\lexus2\tops\c_am_kiryu\Textures\" + texture +
            //                 ".png", true);
            //         }
            //     }
            // }


            if (matDef.MetallicMap != null)
            {
                var path = converter.GetMetallicRoughnessImage(matDef.MetallicMap);
                if (path != null)
                {
                    mat.WithChannelImage(KnownChannel.MetallicRoughness, path);
                    mat.WithChannelParam(KnownChannel.MetallicRoughness, new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
                    mat.UseChannel(KnownChannel.Occlusion).UseTexture().WithPrimaryImage(mat.GetChannel(KnownChannel.MetallicRoughness).Texture.PrimaryImage);
                }
            }

            return mat;
        }

        public void SaveToGltf2(string path, string texturePath)
        {
            var textures = new List<(string, Texture)>();
            var converter = new ImageConverter(texturePath);
            var scene = new SceneBuilder();
            // var materials = new DictionaryEntry();
            var materials = new MaterialBuilder[Materials.Length];

            var skin = CreateSkin();
            int meshId = 0;
            foreach (var mesh in Meshes)
            {
                // Console.WriteLine("Process mesh " + meshId);
                int counter = 0;
                foreach (var submesh in mesh.Submeshes)
                {
                    var mat = materials[submesh.Material.Id] ?? InitializeMaterial(submesh.Material, materials, converter);
                    materials[submesh.Material.Id] = mat;


                    if (submesh.Vertices.FirstOrDefault().BoneIndices != null)
                    {
                        var glbMesh = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>($"{submesh.Name}");
                        var primitive = glbMesh.UsePrimitive(mat);
                        var vertices = submesh.Vertices
                            .Select(v => new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>(
                                new VertexPositionNormal(v.Position, v.Normal),
                                new VertexTexture1(new Vector2(v.Uv0.X, 1 - v.Uv0.Y)),
                                new VertexJoints4(
                                    (v.BoneIndices[0], v.BoneWeights[0]),
                                    (v.BoneIndices[1], v.BoneWeights[1]),
                                    (v.BoneIndices[2], v.BoneWeights[2]),
                                    (v.BoneIndices[3], v.BoneWeights[3])
                                )
                            )).ToArray();
                        var t = submesh.Triangles;
                        for (int i = 0; i < t.GetLength(0); i++)
                        {
                            primitive.AddTriangle(vertices[t[i, 0]], vertices[t[i, 1]], vertices[t[i, 2]]);
                        }

                        scene.AddSkinnedMesh(glbMesh, null, Matrix4x4.Identity, skin);
                    }
                    else
                    {
                        var glbMesh = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>($"{submesh.Name}");
                        var primitive = glbMesh.UsePrimitive(mat);

                        var vertices = submesh.Vertices
                            .Select(v => new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
                                new VertexPositionNormal(v.Position, v.Normal),
                                new VertexTexture1(new Vector2(v.Uv0.X, 1 - v.Uv0.Y))
                            )).ToArray();
                        var t = submesh.Triangles;
                        for (int i = 0; i < t.GetLength(0); i++)
                        {
                            primitive.AddTriangle(vertices[t[i, 0]], vertices[t[i, 1]], vertices[t[i, 2]]);
                        }

                        scene.AddRigidMesh(glbMesh, null, submesh.Transform);
                    }
                }

                meshId++;
            }

            // Console.WriteLine("Save...");
            var gltf2 = scene.ToGltf2();

            // Fix skin
            var firstSkin = gltf2.LogicalSkins.FirstOrDefault();
            foreach (var node in gltf2.LogicalNodes)
            {
                if (node.Skin != null)
                {
                    node.Skin = firstSkin;
                }
            }

            // Fix texture naming (sadl, very inefficient...)
            foreach (var tex in gltf2.LogicalTextures)
            {
                var name = converter.GetName(tex.PrimaryImage.Content.Content.ToArray());
                if (name != null)
                {
                    tex.Name = name;
                    tex.PrimaryImage.Name = name;
                }
                else
                {
                    Console.WriteLine("Texture not found.");
                }
            }

            gltf2.Save(Path.GetFullPath(path), new WriteSettings()
            {
                ImageWriting = ResourceWriteMode.Default,
                Validation = ValidationMode.Strict
            });
        }

        // public static YakuzaMesh FromGlb(Stream stream)
        // {
        //     var mesh = new YakuzaMesh();
        //     mesh.LoadGlbFile(stream);
        //
        //     return mesh;
        // }

        public static YakuzaMesh FromGltf2(string path, string ddsPath = null)
        {
            // using var file = File.OpenRead(path);
            // return FromGlb(file);
            var mesh = new YakuzaMesh();
            mesh.LoadGlbFile(Path.GetFullPath(path), ddsPath);

            return mesh;
        }

        private NodeBuilder[] CreateSkin()
        {
            var root = new NodeBuilder("Armature");
            var bones = new NodeBuilder[Bones.Length];

            foreach (var bone in Bones)
            {
                var parent = bone.Parent?.Id != null ? bones[bone.Parent.Id] : root;
                var boneTransform = parent.CreateNode(bone.Name)
                    .WithLocalTranslation(bone.Position)
                    .WithLocalRotation(bone.Rotation)
                    .WithLocalScale(bone.Scale);

                bones[bone.Id] = boneTransform;
            }

            return bones.ToArray();
        }

        private string GetBaseColorTexture(SharpGLTF.Schema2.Material mat, string ddsPath)
        {
            var channel = mat.FindChannel(KnownChannel.BaseColor.ToString());
            if (channel?.Texture?.PrimaryImage?.Name != null)
            {
                return channel.Value.Texture.PrimaryImage.Name;
            }

            if (ddsPath != null && channel.HasValue)
            {
                var filename = $"{Guid.NewGuid().ToString().Substring(0, 8)}.dds";
                var converter = new ImageConverter(ddsPath);
                converter.GenerateColorTexture(filename, channel.Value.Parameter.X, channel.Value.Parameter.Y, channel.Value.Parameter.Z,
                    channel.Value.Parameter.W);

                return Path.GetFileNameWithoutExtension(filename);
            }

            return null;
        }

        private string GetRoughnessTexture(SharpGLTF.Schema2.Material mat, string ddsPath)
        {
            var channel = mat.FindChannel(KnownChannel.MetallicRoughness.ToString());
            if (channel?.Texture?.PrimaryImage?.Name != null)
            {
                return channel.Value.Texture.PrimaryImage.Name;
            }

            if (ddsPath != null)
            {
                var filename = $"{Guid.NewGuid().ToString().Substring(0, 8)}.dds";
                var converter = new ImageConverter(ddsPath);
                if (channel.HasValue)
                {
                    converter.GenerateMetallicRoughnessOcclusion(filename, channel.Value.Parameter.X, channel.Value.Parameter.Y, 1);
                }
                else
                {
                    converter.GenerateMetallicRoughnessOcclusion(filename, 0, 0.8f, 1);
                }

                return Path.GetFileNameWithoutExtension(filename);
            }

            return null;
        }

        private Material CreateMaterial(SharpGLTF.Schema2.Material mat, string ddsPath, int id)
        {
            if (mat != null)
            {
                return new Material()
                {
                    Id = (uint) id,
                    Shader = DecodeShaderName(mat.Name),
                    Textures = new[]
                    {
                        GetBaseColorTexture(mat, ddsPath),
                        GetRoughnessTexture(mat, ddsPath),
                        mat.FindChannel(KnownChannel.Normal.ToString())?.Texture?.PrimaryImage?.Name,
                    }
                };
            }
            else
            {
                return new Material()
                {
                    Id = (uint) id,
                    Shader = GmdUtils.PBR_MAT,
                    Textures = new string[]
                    {
                    }
                };
            }
        }

        private void LoadGlbFile(string path, string ddsPath = null)
        {
            var glb = ModelRoot.Load(path);

            Name = Path.GetFileNameWithoutExtension(path);

            // BONES
            var skin = glb.LogicalSkins[0];
            var joints = Enumerable.Range(0, skin.JointsCount)
                .Select(i => glb.LogicalSkins[0].GetJoint(i).Joint)
                .ToList();

            Bones = joints.Select((joint, i) => new Bone()
            {
                Id = i,
                Name = joint.Name,
                Position = joint.LocalTransform.Translation,
                Rotation = joint.LocalTransform.Rotation,
                Scale = joint.LocalTransform.Scale
            }).ToArray();

            for (int i = 0; i < joints.Count; i++)
            {
                var joint = joints[i];
                var bone = Bones[i];
                var parentIdx = joints.IndexOf(joint.VisualParent);

                bone.Parent = parentIdx >= 0 ? Bones[parentIdx] : null;

                foreach (var child in joint.VisualChildren)
                {
                    var childIdx = joints.IndexOf(child);
                    if (childIdx == -1)
                    {
                        throw new Exception("Child not found?!");
                    }

                    bone.Children.Add(Bones[childIdx]);
                }
            }

            var materials = new List<Material>();
            Meshes = glb.LogicalMeshes.SelectMany((sm, i) =>
            {
                return sm.Primitives.Select((primitive, j) =>
                {
                    var mesh = new Mesh();
                    var positions = primitive.GetVertices("POSITION").AsVector3Array();
                    var normals = primitive.GetVertices("NORMAL").AsVector3Array();
                    var uvs = primitive.GetVertices("TEXCOORD_0").AsVector2Array();
                    var meshJoints = primitive.GetVertices("JOINTS_0").AsVector4Array();
                    var weights = primitive.GetVertices("WEIGHTS_0").AsVector4Array();
                    var material = CreateMaterial(primitive.Material, ddsPath, materials.Count);

                    materials.Add(material);

                    mesh.Submeshes.Add(new Submesh()
                    {
                        Id = i,
                        Name = sm.Name + "_" + j,
                        Material = material,
                        Triangles = primitive.GetTriangleIndices().Select(t => new[]
                        {
                            t.A, t.B, t.C
                        }).To2DArray(),
                        Vertices = Enumerable.Range(0, positions.Count).Select(i => new Vertex()
                        {
                            Position = positions[i],
                            Normal = normals[i],
                            Uv0 = new Vector2(uvs[i].X, 1 - uvs[i].Y),
                            BoneIndices = new[]
                            {
                                (int) meshJoints[i].X,
                                (int) meshJoints[i].Y,
                                (int) meshJoints[i].Z,
                                (int) meshJoints[i].W
                            },
                            BoneWeights = new[] {weights[i].X, weights[i].Y, weights[i].Z, weights[i].W}
                        }).ToArray()
                    });

                    return mesh;
                });
            }).Where(m => m != null).ToArray();


            Materials = materials.ToArray();
            Textures = Materials.SelectMany(mat => mat.Textures).Where(tex => tex != null).Distinct().ToArray();

            if (ddsPath != null)
            {
                ConvertTextures(glb, ddsPath);
            }
        }

        private void ConvertTextures(ModelRoot glb, string ddsPath)
        {
            ddsPath = Path.GetFullPath(ddsPath);
            Directory.CreateDirectory(ddsPath);

            var converter = new ImageConverter(ddsPath);
            foreach (var material in glb.LogicalMaterials)
            {
                var diffuse = material.FindChannel(KnownChannel.BaseColor.ToString());
                var normal = material.FindChannel(KnownChannel.Normal.ToString());
                var roughness = material.FindChannel(KnownChannel.MetallicRoughness.ToString());

                if (diffuse?.Texture?.PrimaryImage?.Content != null)
                {
                    Console.WriteLine($"Converting {diffuse.Value.Texture.PrimaryImage.Name}");
                    converter.ConvertBaseColorToDDS(diffuse.Value.Texture.PrimaryImage);
                }

                //
                if (normal?.Texture?.PrimaryImage?.Content != null)
                {
                    Console.WriteLine($"Converting {normal.Value.Texture.PrimaryImage.Name}");
                    converter.ConvertNormalToDDS(normal.Value.Texture.PrimaryImage);
                }

                //
                if (roughness?.Texture?.PrimaryImage?.Content != null)
                {
                    Console.WriteLine($"Converting {roughness.Value.Texture.PrimaryImage.Name}");
                    converter.ConvertMetallicRoughnessToDDS(roughness.Value.Texture.PrimaryImage);
                }
            }
        }
    }
}