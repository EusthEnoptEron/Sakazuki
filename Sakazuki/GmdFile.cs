using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using MemoryTributaryS;

namespace Sakazuki
{
    public class GmdFile
    {
        private const uint MAGIC = 1296520007;
        private const int VERSION_1 = 33;
        private const int VERSION_2 = 262146;

        private const int Alignment = 2048;

        private Header _header;
        public HashedName[] Textures = new HashedName[0];
        public Material[] Materials = new Material[0];
        public Mesh[] Meshes = new Mesh[0];
        public Submesh[] Submeshes = new Submesh[0];
        public BoneIndex[] BoneIndices = new BoneIndex[0];
        public BoneTransform[] BoneTransforms = new BoneTransform[0];
        public HashedName[] BoneNames = new HashedName[0];
        public Index[] Indices = new Index[0];
        public byte[][] VertexBuffers = new byte[0][];
        public HashedName[] Shaders = new HashedName[0];
        public string Name = "";

        /// <summary>
        /// The length of this is BoneTransforms.length - (count where supplementary index >= 0)
        /// </summary>
        public Matrix64[] InverseBindPoses = new Matrix64[0];

        public Unknown1Struct[] _submeshToBones = new Unknown1Struct[0];
        public byte[] _unknown4 = new byte[0];


        public Unknown12Struct[] _submeshBonesMeta = new Unknown12Struct[0];

        public byte[] _unknown13 = new byte[0];
        public byte[] _unknown14 = new byte[0];
        public byte[] _unknown15 = new byte[0];
        public byte[] _buffer = new byte[0];

        public GmdFile()
        {
            _header.Magic = MAGIC;
            _header.Version1 = VERSION_1;
            _header.Version2 = VERSION_2;
        }

        public static GmdFile FromStream(Stream stream)
        {
            var file = new GmdFile();
            file.Read(stream);
            return file;
        }

        public static GmdFile FromFile(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                return FromStream(stream);
            }
        }

        private void Read(Stream sourceStream)
        {
            var stream = new MemoryTributary();
            var reader = new BinaryReader(stream);

            sourceStream.CopyTo(stream);

            stream.Seek(0, SeekOrigin.Begin);

            _header = Header.ReadStatic(reader);

            Name = _header.SceneName;
            Textures = ReadArray<HashedName>(reader, _header.TextureOffset, _header.TextureCount);
            Materials = ReadArray<Material>(reader, _header.MaterialOffset, _header.MaterialCount);
            Meshes = ReadArray<Mesh>(reader, _header.MeshOffset, _header.MeshCount);
            Submeshes = ReadArray<Submesh>(reader, _header.SubmeshOffset, _header.SubmeshCount);

            BoneIndices = ReadArray<BoneIndex>(reader, _header.BoneIndexOffset, Header.UseSingleByteIndices
                ? _header.BoneIndexBlockSize
                : _header.BoneIndexBlockSize / 2);

            BoneTransforms = ReadArray<BoneTransform>(reader, _header.BoneMatOffset, _header.BoneMatCount);
            BoneNames = ReadArray<HashedName>(reader, _header.BoneNameOffset, _header.BoneNameCount);
            Indices = ReadArray<Index>(reader, _header.IndicesOffset, _header.IndicesCount);
            Shaders = ReadArray<HashedName>(reader, _header.ShaderOffset, _header.ShaderCount);

            reader.BaseStream.Seek(_header.VertexBufferOffset, SeekOrigin.Begin);
            VertexBuffers = new byte[Meshes.Length][];
            int i = 0;
            foreach (var mesh in Meshes)
            {
                VertexBuffers[i++] = reader.ReadBytes(mesh.Size, _header.VertexBufferOffset + mesh.Offset);
            }

            InverseBindPoses = ReadArray<Matrix64>(reader, _header.InverseBindPosesOffset, _header.InverseBindPosesCount);

            // This seems to be a list of transforms for attachment (?) bones that have no parent. The first 4 ints are [ID] [Bone No] [Bone No] [?]
            _submeshToBones = ReadArray<Unknown1Struct>(reader, _header.Offset01, _header.Count01);
            // _unknown1 = BoneTransforms.Where(bt => bt.SupplementaryBoneTableIndex >= 0).Take(6).Select((b, i) =>
            // {
            //     var matrix = new Unknown1Struct();
            //     matrix.Id = b.SupplementaryBoneTableIndex;
            //     matrix.BoneNo = b.BoneNo;
            //     matrix.BoneNo2 = b.BoneNo2;
            //     matrix.Offset = i * 8;
            //     return matrix;
            // }).ToArray();

            // The value range correlates to the number of submeshes
            _submeshBonesMeta = ReadArray<Unknown12Struct>(reader, _header.Offset12, _header.Count12 / 8);
            // for (int j = 0; j < _unknown12.Length; j++)
            // {
            //     _unknown12[j].Unknown1 = 0;
            //     _unknown12[j].Unknown2 = 0;
            //     _unknown12[j].Unknown3 = 0;
            // }


            // _unknown12[4].Unknown1 = 0;
            // _unknown12[4].Unknown2 = 0;
            // _unknown12[4].Unknown3 = 0;
            // // _unknown12 = _unknown12.Reverse().ToArray();


            // for (int j = 0; j < _unknown12.Length; j += 4)
            // {
            //     var offset = j * 4;
            //     if (_unknown1.All(o => o.Offset != offset))
            //     {
            //         _unknown12[j].U1 = 50;
            //         _unknown12[j + 1].U1 = 50;
            //         _unknown12[j + 2].U1 = 50;
            //         _unknown12[j + 3].U1 = 50;
            //     }
            // }
            // 0 => Chin bone to floor
            // // 1 => Normal
            // // 2 => Normal
            // // 3 => Normal
            // _unknown12 = new Unknown12Struct[_unknown12.Length - 7];
            // 0 => crash, 4 => crash, 8 => crash, 12 => no crash, 32 => crash
            // _unknown12[1].U1 = 50;
            // _unknown12[2].U1 = 5;
            // _unknown12[3].U1 = 50;
            // _unknown12[5].U1 = 50;
            // _unknown12[12].U1 = 50;


            // for (int j = 0; j < _unknown12.Length / 2; j++)
            // {
            //     // if (_unknown12[j].U1 != max)
            //     {
            //         _unknown12[j].U1 = 0;
            //     }
            // }

            //
            // for (i = 0; i < _unknown12.Length; i++)
            // {
            //     _unknown12[i] = 0;
            // }
            // for (ushort j = 0; j < BoneNames.Length; j++)
            // {
            //     BoneNames[j].Id = j;
            // }


            // _unknown12[_unknown1[Submeshes[9].Unknown1Offset].Offset / 8].Unknown3 = 10;
            _unknown4 = ReadBuffer(reader, _header.Offset04, _header.Count04, 0x10);
            _unknown13 = ReadBuffer(reader, _header.Offset13, _header.Count13, 0x40);
            _unknown14 = ReadBuffer(reader, _header.Offset14, _header.Count14, 0x40);
            _unknown15 = ReadBuffer(reader, _header.Offset15, _header.Count15, 0x40);
        }

        private void EnsureCoherency()
        {
            CalculateInversePoses();

            for (int j = 0; j < Submeshes.Length; j++)
            {
                Submeshes[j].Unknown1Offset = j;
                Submeshes[j].BoneNo = 0;
            }

            _submeshToBones = Submeshes.Select((sm, j) => new Unknown1Struct()
            {
                Id = j,
                Offset = j * 8,
                BoneNo = 0,
                BoneNo2 = 0
            }).ToArray();

            _submeshBonesMeta = Submeshes.Select((sm, j) => new Unknown12Struct()
            {
                SubmeshId = 1,
                Unknown1 = 0,
                Unknown2 = (short) j,
                Unknown3 = (short) j
            }).ToArray();
        }

        public void Write(Stream stream)
        {
            EnsureCoherency();

            var writer = new BinaryWriter(stream, Encoding.Default, true);
            stream.SetLength(0);

            writer.Write(new byte[256]);
            // _header.Write(writer);
            var originalHeader = _header;
            _header.SceneName = Name;

            _header.TextureCount = Textures.Length;
            _header.TextureOffset = (int) WriteArray(writer, Textures);

            _header.MaterialCount = Materials.Length;
            _header.MaterialOffset = (int) WriteArray(writer, Materials);

            _header.SubmeshCount = Submeshes.Length;
            _header.SubmeshOffset = (int) WriteArray(writer, Submeshes);

            _header.BoneIndexBlockSize = Header.UseSingleByteIndices ? BoneIndices.Length : BoneIndices.Length * 2;
            _header.BoneIndexOffset = (int) WriteArray(writer, BoneIndices);

            _header.BoneMatCount = BoneTransforms.Length;
            _header.BoneMatOffset = (int) WriteArray(writer, BoneTransforms);

            _header.BoneNameCount = BoneNames.Length;
            _header.BoneNameOffset = (int) WriteArray(writer, BoneNames);

            _header.IndicesCount = Indices.Length;
            _header.IndicesOffset = (int) WriteArray(writer, Indices);

            _header.ShaderCount = Shaders.Length;
            _header.ShaderOffset = (int) WriteArray(writer, Shaders);

            _header.InverseBindPosesCount = InverseBindPoses.Length;
            _header.InverseBindPosesOffset = (int) WriteArray(writer, InverseBindPoses);


            for (int i = 0; i < VertexBuffers.Length; i++)
            {
                var offset = (int) WriteBuffer(writer, VertexBuffers[i]);
                if (i == 0)
                {
                    _header.VertexBufferOffset = offset;
                }

                Meshes[i].Offset = offset - _header.VertexBufferOffset;
            }

            _header.VertexBufferSize = (int) (writer.BaseStream.Position - _header.VertexBufferOffset);


            _header.MeshCount = Meshes.Length;
            _header.MeshOffset = (int) WriteArray(writer, Meshes);

            // Fill important unknowns
            // _unknown1 = new Unknown1Struct[0];
            _header.Count01 = _submeshToBones.Length;
            _header.Offset01 = (int) WriteArray(writer, _submeshToBones);

            _header.Count12 = _submeshBonesMeta.Length * 8;
            _header.Offset12 = (int) WriteArray(writer, _submeshBonesMeta);

            // Purge unimportant unknowns
            var end = writer.BaseStream.Position.Aligned(Alignment);
            _header.Count04 = 0;
            _header.Count13 = 0;
            _header.Count14 = 0;
            _header.Count15 = 0;
            _header.Offset04 = (int) end;
            _header.Offset13 = (int) end;
            _header.Offset14 = (int) end;
            _header.Offset15 = (int) end;
            _header.Unk01 = 0;
            _header.ModelSize = 0;

            writer.BaseStream.Seek(0, SeekOrigin.Begin);
            _header.Write(writer);

            writer.BaseStream.Seek(end, SeekOrigin.Begin);
            writer.WriteAlignment(Alignment);
        }

        private void CalculateInversePoses()
        {
            var list = new List<Matrix64>(BoneTransforms.Length);
            var transforms = new List<BoneTransform>();

            BoneTransform? GetParentBone(BoneTransform bt)
            {
                var searchIdx = bt.BoneNo;
                for (int i = 0; i < BoneTransforms.Length; i++)
                {
                    if (BoneTransforms[i].NextChildIndex == searchIdx)
                    {
                        return BoneTransforms[i];
                    }

                    if (BoneTransforms[i].NextSiblingIndex == searchIdx)
                    {
                        searchIdx = BoneTransforms[i].BoneNo;
                        i = 0;
                    }
                }

                return null;
            }

            foreach (var bt in BoneTransforms)
            {
                var rotation = bt.LocalRotation;
                var parent = GetParentBone(bt);

                transforms.Clear();
                transforms.Add(bt);

                while (parent.HasValue)
                {
                    transforms.Add(parent.Value);
                    rotation = parent.Value.LocalRotation * rotation;
                    parent = GetParentBone(parent.Value);
                }

                // matrix multiplication is right -> left, so we don't need to reverse anything
                var worldMat = transforms.Select(t =>
                    Matrix4x4.CreateFromQuaternion(t.LocalRotation) * Matrix4x4.CreateTranslation(t.LocalPosition)
                ).Aggregate((lhs, rhs) => lhs * rhs);

                if (Matrix4x4.Invert(worldMat, out var inverted))
                {
                    list.Add(Matrix64.FromMatrix(inverted));
                }
                else
                {
                    throw new Exception("Matrix could not be inverted");
                }
            }

            InverseBindPoses = list.ToArray();
        }


        private byte[] ReadBuffer(BinaryReader reader, long offset, int count, int elementSize)
        {
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            return reader.ReadBytes(count * elementSize);
        }

        private byte[] ReadArrayAsBuffer<T>(BinaryReader reader, long offset, int count) where T : struct, IReadable
        {
            var start = offset;
            ReadArray<T>(reader, offset, count);
            var end = reader.BaseStream.Position;

            return ReadBuffer(reader, offset, count, (int) ((end - start) / count));
        }

        private T[] ReadArray<T>(BinaryReader reader, long offset, int count) where T : struct, IReadable
        {
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            var result = new T[count];

            T baseInstance = default;
            for (int i = 0; i < count; i++)
            {
                baseInstance.Read(reader);
                result[i] = baseInstance; // struct will create a copy
            }

            return result;
        }

        private long WriteArray<T>(BinaryWriter writer, T[] data) where T : struct, IWritable
        {
            writer.WriteAlignment(Alignment);
            var offset = writer.BaseStream.Position;
            foreach (var el in data)
            {
                el.Write(writer);
            }

            return offset;
        }

        private long WriteBuffer(BinaryWriter writer, byte[] data)
        {
            writer.WriteAlignment(Alignment);
            var offset = writer.BaseStream.Position;
            writer.Write(data);
            return offset;
        }

        public struct Header : IReadable, IWritable
        {
            public uint Magic;
            public int Version1;
            public int Version2;
            public int ModelSize;
            public ushort Unk01;
            public string SceneName;
            public int BoneMatOffset;
            public int BoneMatCount;
            public int Offset01;
            public int Count01;
            public int SubmeshOffset;
            public int SubmeshCount;
            public int MaterialOffset;
            public int MaterialCount;
            public int Offset04;
            public int Count04;
            public int InverseBindPosesOffset;
            public int InverseBindPosesCount;
            public int MeshOffset;
            public int MeshCount;
            public int VertexBufferOffset;
            public int VertexBufferSize;
            public int TextureOffset;
            public int TextureCount;
            public int ShaderOffset;
            public int ShaderCount;
            public int BoneNameOffset;
            public int BoneNameCount;
            public int IndicesOffset;
            public int IndicesCount;
            public int Offset12;
            public int Count12;
            public int BoneIndexOffset;
            public int BoneIndexBlockSize;
            public int Offset13;
            public int Count13;
            public int Offset14;
            public int Count14;
            public int Offset15;
            public int Count15;
            public static bool UseSingleByteIndices;

            public static Header ReadStatic(BinaryReader reader)
            {
                var header = new Header();
                header.Read(reader);
                return header;
            }

            public void Read(BinaryReader reader)
            {
                Magic = reader.ReadUInt32();
                Version1 = reader.ReadInt32();
                Version2 = reader.ReadInt32();
                ModelSize = reader.ReadInt32();
                Unk01 = reader.ReadUInt16();
                SceneName = reader.ReadString(30);
                BoneMatOffset = reader.ReadInt32(); // Bone Matrix / Parent / 0x80;
                BoneMatCount = reader.ReadInt32();

                Offset01 = reader.ReadInt32(); // info related to section before last? / 0x40; Turns invisible when missing
                Count01 = reader.ReadInt32();
                SubmeshOffset = reader.ReadInt32(); // Face Info /0x40;
                SubmeshCount = reader.ReadInt32();
                MaterialOffset = reader.ReadInt32();
                MaterialCount = reader.ReadInt32();
                Offset04 = reader.ReadInt32(); // ? / 0x10;
                Count04 = reader.ReadInt32();
                InverseBindPosesOffset = reader.ReadInt32(); // Some Matrix / 0x40;
                InverseBindPosesCount = reader.ReadInt32(); // Bone count - Count01
                MeshOffset = reader.ReadInt32(); // Vertex Info / 0x20;
                MeshCount = reader.ReadInt32();
                VertexBufferOffset = reader.ReadInt32(); // Vertex Start / 1;
                VertexBufferSize = reader.ReadInt32();
                TextureOffset = reader.ReadInt32();
                TextureCount = reader.ReadInt32();
                ShaderOffset = reader.ReadInt32();
                ShaderCount = reader.ReadInt32();
                BoneNameOffset = reader.ReadInt32(); // Bone Names / 0x20;
                BoneNameCount = reader.ReadInt32();

                IndicesOffset = reader.ReadInt32(); // Some Face Info / 2;
                IndicesCount = reader.ReadInt32();
                Offset12 = reader.ReadInt32(); // ? / 1;
                Count12 = reader.ReadInt32();
                BoneIndexOffset = reader.ReadInt32(); // ? / 1;
                BoneIndexBlockSize = reader.ReadInt32();

                // Skip zeroes
                reader.BaseStream.Seek(12 * 4, SeekOrigin.Current);

                Offset13 = reader.ReadInt32(); // Correlates with material count
                Count13 = reader.ReadInt32();

                Offset14 = reader.ReadInt32(); // Doesn't seem to do anything
                Count14 = reader.ReadInt32();

                Offset15 = reader.ReadInt32(); // Correlates with material count
                Count15 = reader.ReadInt32();

                // Skip zeroes
                reader.BaseStream.Seek(4 * 4, SeekOrigin.Current);

                reader.BaseStream.Seek(255L, SeekOrigin.Begin);
                UseSingleByteIndices = (reader.ReadByte() >> 7) == 0;
            }

            public void Write(BinaryWriter writer)
            {
                writer.Write(Magic);
                writer.Write(Version1);
                writer.Write(Version2);
                writer.Write(ModelSize);
                writer.Write(Unk01);
                writer.WriteString(SceneName, 30);
                writer.Write(BoneMatOffset);
                writer.Write(BoneMatCount);

                writer.Write(Offset01);
                writer.Write(Count01);
                writer.Write(SubmeshOffset);
                writer.Write(SubmeshCount);
                writer.Write(MaterialOffset);
                writer.Write(MaterialCount);
                writer.Write(Offset04);
                writer.Write(Count04);
                writer.Write(InverseBindPosesOffset);
                writer.Write(InverseBindPosesCount);
                writer.Write(MeshOffset);
                writer.Write(MeshCount);
                writer.Write(VertexBufferOffset);
                writer.Write(VertexBufferSize);
                writer.Write(TextureOffset);
                writer.Write(TextureCount);
                writer.Write(ShaderOffset);
                writer.Write(ShaderCount);
                writer.Write(BoneNameOffset);
                writer.Write(BoneNameCount);

                writer.Write(IndicesOffset);
                writer.Write(IndicesCount);
                writer.Write(Offset12);
                writer.Write(Count12);
                writer.Write(BoneIndexOffset);
                writer.Write(BoneIndexBlockSize);

                // Skip zeroes
                writer.Write(new byte[12 * 4]);

                writer.Write(Offset13);
                writer.Write(Count13);

                writer.Write(Offset14);
                writer.Write(Count14);

                writer.Write(Offset15);
                writer.Write(Count15);

                // Skip zeroes
                writer.Write(new byte[4 * 4]);

                while (writer.BaseStream.Position < 255)
                {
                    writer.Write((byte) 0);
                }

                writer.Write((byte) (UseSingleByteIndices ? 0 : 0b10000000));
            }
        }


        public interface IReadable
        {
            public void Read(BinaryReader reader);
        }

        public interface IWritable
        {
            public void Write(BinaryWriter writer);
        }

        public struct Texture : IReadable, IWritable
        {
            public string Name;
            public ushort Id;

            void IReadable.Read(BinaryReader reader)
            {
                Id = reader.ReadUInt16();
                Name = reader.ReadString(30);
            }

            public void Write(BinaryWriter writer)
            {
                writer.Write(Id);
                writer.WriteString(Name, 30);
            }
        }

        public struct HashedName : IReadable, IWritable
        {
            // Unknown what this does, but it also appears at 0x10 in the actual shader file
            public ushort Hash;
            public string Name;

            void IReadable.Read(BinaryReader reader)
            {
                Hash = reader.ReadUInt16();
                Name = reader.ReadString(30);
            }

            public void Write(BinaryWriter writer)
            {
                writer.Write(Hash);
                writer.WriteString(Name, 30);
            }

            public override string ToString()
            {
                return Name;
            }

            public static HashedName FromString(string name)
            {
                var hash = name.Select(c => (int) c).Sum();

                var pName = new HashedName()
                {
                    Name = name,
                    Hash = (ushort) hash
                };

                return pName;
            }
        }

        public struct Material : IReadable, IWritable
        {
            public uint[] Footer { get; set; }

            public int[] TextureIndices { get; set; }

            public int[] Header { get; set; }

            public uint SubmeshIndex { get; set; }

            public uint ShaderIndex { get; set; }

            public uint Unknown1 { get; set; }

            public uint Id { get; set; }

            public void Initialize()
            {
                Header = new[]
                {
                    1,
                    7,
                    0x10000,
                    0
                };

                Footer = new uint[16];
                Unknown1 = 0;
            }

            void IReadable.Read(BinaryReader reader)
            {
                Id = reader.ReadUInt32();
                Unknown1 = reader.ReadUInt32(); // may be 0
                ShaderIndex = reader.ReadUInt32();
                SubmeshIndex = reader.ReadUInt32(); // idx14

                Header = new int[]
                {
                    reader.ReadInt32(), // 1 or 2, 0, makes mesh invisible
                    reader.ReadInt32(), // 7 seems to work?
                    reader.ReadInt32(), // has to be 0x10000 or strange things happen
                    reader.ReadInt32() // 0 seems to be ok?
                };

                TextureIndices = new int[]
                {
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),

                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32()
                };

                // May be empty
                Footer = new uint[]
                {
                    reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(),
                    reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(),
                    reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(),
                    reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32()
                };
            }

            public void Write(BinaryWriter writer)
            {
                Initialize();

                writer.Write(Id);
                writer.Write(Unknown1);
                writer.Write(ShaderIndex);
                writer.Write(SubmeshIndex);

                writer.Write(Header);
                writer.Write(TextureIndices);
                writer.Write(Footer);
            }
        }

        public struct BoneIndex : IReadable, IWritable
        {
            public int Value;

            void IReadable.Read(BinaryReader reader)
            {
                if (Header.UseSingleByteIndices)
                {
                    Value = reader.ReadByte();

                    // TODO: Check for max value?
                }
                else
                {
                    Value = reader.ReadUInt16();

                    // TODO: Check for max value?
                }
            }

            public void Write(BinaryWriter writer)
            {
                if (Header.UseSingleByteIndices)
                {
                    writer.Write((byte) Value);
                }
                else
                {
                    writer.Write((ushort) Value);
                }
            }
        }

        public struct Mesh : IReadable, IWritable
        {
            public int MeshId;
            public int Stride;
            public int Size;
            public int Offset;
            public long Format;
            public int Count;

            public enum MeshMode
            {
                Shadow = 0,
                Body = 1, // => 1 flag
                Hair = 2, // => 2 flags
                Face = 3 // => 3 flags
            }

            /*
             *  000010000011000000000001111111111000011 //UV1 / 32
                100010000101000000000001111111111000011 //UV2 / 36
                100010000101111111000001111111111000011 //UV2 / 44
                000010000011000000000000001111111000011 //UV1 / 28
                000010000011000111000001111111111000011 //UV1 / 36
             */
            public void Initialize()
            {
                Format = 0b100010000011000000000000001111111000011;
            }

            void IReadable.Read(BinaryReader reader)
            {
                MeshId = reader.ReadInt32();
                Count = reader.ReadInt32();
                Format = reader.ReadInt64();
                Offset = reader.ReadInt32();
                Size = reader.ReadInt32();
                Stride = reader.ReadInt32();
                reader.ReadInt32(); // NULL
            }

            public void Write(BinaryWriter writer)
            {
                writer.Write(MeshId);
                writer.Write(Count);
                writer.Write(Format);
                writer.Write(Offset);
                writer.Write(Size);
                writer.Write(Stride);
                writer.Write(0);
            }

            public int UvLayers
            {
                get => (int) ((Format >> 28) & 0b11);
                set
                {
                    Format &= ~(0b11 << 28); // Reset 
                    Format |= ((value & 0b11) << 28); // Set

                    // Format &= ~(0b1L << 38); // Reset
                    // if (value > 1)
                    // {
                    //     Format |= ((0b1L) << 38); // Set
                    // }
                }
            }

            public bool HasBones
            {
                get => (Format >> 6 & 0b1111) == 0b1111;
                set
                {
                    Format &= ~(0b1111 << 6); // Reset 
                    Format |= ((value ? 0b1111 : 0L) & 0b1111) << 6; // Set
                }
            }

            public MeshMode Mode
            {
                get
                {
                    int no = 0;
                    if ((Format >> 13 & 7L) == 7L)
                    {
                        no++;
                    }

                    if ((Format >> 21 & 7L) == 7L)
                    {
                        no++;
                    }

                    if ((Format >> 24 & 7L) == 7L)
                    {
                        no++;
                    }

                    return (MeshMode) no;
                }
                set
                {
                    int no = (int) value;
                    Format &= ~(0b111 << 13); // Reset 
                    Format &= ~(0b111 << 21);
                    Format &= ~(0b111 << 24);

                    if (no > 0)
                    {
                        Format |= (0b111 << 24); // Set
                    }

                    if (no > 1)
                    {
                        Format |= (0b111 << 21); // Set
                    }

                    if (no > 2)
                    {
                        Format |= (0b111 << 13); // Set
                    }

                    if (value != Mode)
                    {
                        throw new Exception("Not true!");
                    }
                }
            }
        }

        public struct Submesh : IReadable, IWritable
        {
            public int BufferOffset2;

            public int BufferOffset1;

            public int BoneIndexOffset;

            public int BoneIndexCount;

            public int TriangleStrip2Offset;

            public int TriangleStrip2Count;

            public int TriangleStripOffset;

            public int TriangleStripCount;

            public int IndicesOffset;

            public int IndicesCount;

            public int VertexCount;

            public int MeshIndex;

            public int MaterialIndex;

            public int Id;

            public int BoneNo;
            public int Unknown1Offset;


            void IReadable.Read(BinaryReader reader)
            {
                Id = reader.ReadInt32();
                MaterialIndex = reader.ReadInt32(); // does not actually have an effect
                MeshIndex = reader.ReadInt32();
                VertexCount = reader.ReadInt32();
                IndicesCount = reader.ReadInt32();
                IndicesOffset = reader.ReadInt32();
                TriangleStripCount = reader.ReadInt32();
                TriangleStripOffset = reader.ReadInt32(); // May be 0
                TriangleStrip2Count = reader.ReadInt32(); // May be 0
                TriangleStrip2Offset = reader.ReadInt32(); // May be 0
                BoneIndexCount = reader.ReadInt32();
                BoneIndexOffset = Header.UseSingleByteIndices ? reader.ReadInt32() : reader.ReadInt32() / 2;

                // Don't seem to do anything (i.e. can be 0)
                BoneNo = reader.ReadInt32();
                Unknown1Offset = reader.ReadInt32();

                BufferOffset1 = reader.ReadInt32();
                BufferOffset2 = reader.ReadInt32();
            }

            public void Write(BinaryWriter writer)
            {
                BufferOffset1 = 0;
                TriangleStrip2Count = 0;
                TriangleStrip2Offset = 0;
                TriangleStripCount = 0;
                TriangleStripOffset = 0;

                writer.Write(Id);
                writer.Write(MaterialIndex);
                writer.Write(MeshIndex);
                writer.Write(VertexCount);
                writer.Write(IndicesCount);
                writer.Write(IndicesOffset);
                writer.Write(TriangleStripCount);
                writer.Write(TriangleStripOffset);
                writer.Write(TriangleStrip2Count);
                writer.Write(TriangleStrip2Offset);
                writer.Write(BoneIndexCount);
                writer.Write(Header.UseSingleByteIndices ? BoneIndexOffset : BoneIndexOffset * 2);
                writer.Write(BoneNo);
                writer.Write(Unknown1Offset);
                writer.Write(BufferOffset1);
                writer.Write(BufferOffset2);
            }
        }

        public struct BoneTransform : IReadable, IWritable
        {
            public const int NODE_LEAF = 3;
            public const int NODE_LEAF_END_OF_LIST = 2;
            public const int NODE_PARENT = 1;
            public const int NODE_PARENT_END_OF_LIST = 0;

            public const int NODE_TYPE_BONE = 0;
            public const int NODE_TYPE_TRANSFORM = 2;

            public int BoneNo; // Same as unknown1.m01
            public int NextChildIndex;
            public int NextSiblingIndex;
            public int TransformIndex;
            public int BoneNo2; // Same as unknown1.m02, same as BoneNo
            public int HierarchyType;
            public int BoneNameIndex;
            public int NodeType; // Only used with supplementary bone table, then = 2
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 Scale;
            public Vector3 Position;
            public byte[] Footer; // May be empty
            public Vector4 Pos3;

            public void Initialize()
            {
                Footer = new byte[16];
                Pos3 = new Vector4();
            }

            void IReadable.Read(BinaryReader reader)
            {
                BoneNo = reader.ReadInt32();
                NextChildIndex = reader.ReadInt32();
                NextSiblingIndex = reader.ReadInt32();
                TransformIndex = reader.ReadInt32();
                BoneNo2 = reader.ReadInt32();
                HierarchyType = reader.ReadInt32();
                BoneNameIndex = reader.ReadInt32();
                NodeType = reader.ReadInt32();

                // if (_nextSiblingIndex >= 0 && (_num3 == 1 || _num3 == 3))
                // {
                //     _bones[_nextSiblingIndex].ParentBone = _bones[i].ParentBone;
                // }
                //
                // if (_num3 == 1 || _num3 == 0)
                // {
                //     _bones[_nextChildIndex].ParentBone = i;
                // }

                LocalPosition = new Vector3(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );

                reader.ReadSingle();

                LocalRotation = new Quaternion(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );

                Scale = new Vector3(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );

                reader.ReadSingle();

                Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                reader.ReadSingle();

                Pos3 = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                Footer = reader.ReadBytes(16);
            }

            public void Write(BinaryWriter writer)
            {
                if (NextChildIndex == -1)
                {
                    HierarchyType = NextSiblingIndex == -1 ? NODE_LEAF_END_OF_LIST : NODE_LEAF;
                }
                else
                {
                    HierarchyType = NextSiblingIndex == -1 ? NODE_PARENT_END_OF_LIST : NODE_PARENT;
                }

                Initialize();
                NodeType = TransformIndex >= 0 ? NODE_TYPE_TRANSFORM : NODE_TYPE_BONE;

                writer.Write(BoneNo);
                writer.Write(NextChildIndex);
                writer.Write(NextSiblingIndex);
                writer.Write(TransformIndex);
                writer.Write(BoneNo2);
                writer.Write(HierarchyType);
                writer.Write(BoneNameIndex);
                writer.Write(NodeType);

                writer.Write(LocalPosition.X);
                writer.Write(LocalPosition.Y);
                writer.Write(LocalPosition.Z);

                writer.Write(1.0f);

                writer.Write(LocalRotation.X);
                writer.Write(LocalRotation.Y);
                writer.Write(LocalRotation.Z);
                writer.Write(LocalRotation.W);

                writer.Write(Scale.X);
                writer.Write(Scale.Y);
                writer.Write(Scale.Z);
                writer.Write(1.0f);

                writer.Write(Position.X);
                writer.Write(Position.Y);
                writer.Write(Position.Z);
                writer.Write(1.0f);

                writer.Write(Pos3.X);
                writer.Write(Pos3.Y);
                writer.Write(Pos3.Z);
                writer.Write(Pos3.W);

                writer.Write(Footer);
            }
        }

        public struct BoneName : IReadable, IWritable
        {
            public ushort Id;
            public string Value;

            void IReadable.Read(BinaryReader reader)
            {
                Id = reader.ReadUInt16();
                Value = reader.ReadString(30);
            }

            public void Write(BinaryWriter writer)
            {
                writer.Write(Id);
                writer.WriteString(Value, 30);
            }
        }

        public struct Vertex : IReadable
        {
            void IReadable.Read(BinaryReader reader)
            {
            }
        }

        public struct Matrix64 : IReadable, IWritable
        {
            public Matrix4x4 Values;

            public static readonly Matrix64 Identity = new Matrix64()
            {
                Values = Matrix4x4.Identity
            };

            public static Matrix64 FromMatrix(Matrix4x4 matrix)
            {
                return new Matrix64() {Values = matrix};
            }

            public void Read(BinaryReader reader)
            {
                Values.M11 = reader.ReadSingle();
                Values.M12 = reader.ReadSingle();
                Values.M13 = reader.ReadSingle();
                Values.M14 = reader.ReadSingle();

                Values.M21 = reader.ReadSingle();
                Values.M22 = reader.ReadSingle();
                Values.M23 = reader.ReadSingle();
                Values.M24 = reader.ReadSingle();

                Values.M31 = reader.ReadSingle();
                Values.M32 = reader.ReadSingle();
                Values.M33 = reader.ReadSingle();
                Values.M34 = reader.ReadSingle();

                Values.M41 = reader.ReadSingle();
                Values.M42 = reader.ReadSingle();
                Values.M43 = reader.ReadSingle();
                Values.M44 = reader.ReadSingle();
            }

            public void Write(BinaryWriter writer)
            {
                writer.Write(Values.M11);
                writer.Write(Values.M12);
                writer.Write(Values.M13);
                writer.Write(Values.M14);

                writer.Write(Values.M21);
                writer.Write(Values.M22);
                writer.Write(Values.M23);
                writer.Write(Values.M24);

                writer.Write(Values.M31);
                writer.Write(Values.M32);
                writer.Write(Values.M33);
                writer.Write(Values.M34);

                writer.Write(Values.M41);
                writer.Write(Values.M42);
                writer.Write(Values.M43);
                writer.Write(Values.M44);
            }
        }

        public struct Unknown1Struct : IReadable, IWritable
        {
            public int Id;
            public int BoneNo;
            public int BoneNo2;
            public int Offset;

            public Vector4 UnknownVector1; // May be empty
            public Vector4 UnknownVector2; // May be empty
            public Vector4 UnknownVector3; // May be empty


            public void Read(BinaryReader reader)
            {
                Id = reader.ReadInt32();
                BoneNo = reader.ReadInt32();
                BoneNo2 = reader.ReadInt32();
                Offset = reader.ReadInt32();

                UnknownVector1 = new Vector4(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );
                UnknownVector2 = new Vector4(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );
                UnknownVector3 = new Vector4(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );
            }


            public void Write(BinaryWriter writer)
            {
                writer.Write(Id);
                writer.Write(BoneNo);
                writer.Write(BoneNo2);
                writer.Write(Offset);

                writer.Write(UnknownVector1.X);
                writer.Write(UnknownVector1.Y);
                writer.Write(UnknownVector1.Z);
                writer.Write(UnknownVector1.W);

                writer.Write(UnknownVector2.X);
                writer.Write(UnknownVector2.Y);
                writer.Write(UnknownVector2.Z);
                writer.Write(UnknownVector2.W);

                writer.Write(UnknownVector3.X);
                writer.Write(UnknownVector3.Y);
                writer.Write(UnknownVector3.Z);
                writer.Write(UnknownVector3.W);
            }
        }

        public struct Unknown12Struct : IReadable, IWritable
        {
            public short SubmeshId;
            public short Unknown1; // 0 in case of Supplementary Bones, may warp mesh when set to 0 (Not necessarily)
            public short Unknown2; // Seems to change the semantics of Unknown3 depending on its value
            public short Unknown3; // Same as Unknown2 (Not necessarily)

            public void Read(BinaryReader reader)
            {
                SubmeshId = reader.ReadInt16();
                Unknown1 = reader.ReadInt16();
                Unknown2 = reader.ReadInt16();
                Unknown3 = reader.ReadInt16();
            }

            public void Write(BinaryWriter writer)
            {
                writer.Write(SubmeshId);
                writer.Write(Unknown1);
                writer.Write(Unknown2);
                writer.Write(Unknown3);
            }
        }

        public struct Index : IReadable, IWritable
        {
            public int Value;

            void IReadable.Read(BinaryReader reader)
            {
                Value = reader.ReadUInt16();
            }

            public void Write(BinaryWriter writer)
            {
                writer.Write((ushort) Value);
            }
        }
    }
}