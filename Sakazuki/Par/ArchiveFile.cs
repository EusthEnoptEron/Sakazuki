using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Aethon.IO;
using MemoryTributaryS;
using Sakazuki.Common;

namespace Sakazuki.Par
{
    public class ArchiveFile : IDisposable
    {
        private const uint Signature = 1129464144; // 'PARC'
        private const int Alignment = 2048;

        private Stream _stream;
        private EndiannessAwareBinaryReader _reader;

        private List<FileEntry> _entries = new List<FileEntry>();
        private static Encoding _Encoding;
        private static readonly StringComparer _NameComparer;

        static ArchiveFile()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            _Encoding = Encoding.GetEncoding(932); // SJIS
            _NameComparer = StringComparer.Ordinal;
        }


        private ArchiveFile()
        {
            _stream = new MemoryTributary();
            _reader = new EndiannessAwareBinaryReader(_stream);
        }

        private ArchiveFile(Stream stream)
        {
            // Copy to memory... we're dealing with ~1GB, so we should be fine.
            _stream = new MemoryTributary();
            _reader = new EndiannessAwareBinaryReader(_stream);
            stream.CopyTo(_stream);

            ReadHeader();
        }

        public void ReplaceFile(string name, byte[] data)
        {
            var idx = _entries.FindIndex(e => string.Equals(e.Name, name, StringComparison.InvariantCultureIgnoreCase));
            if (idx < 0)
            {
                throw new KeyNotFoundException();
            }

            var entry = _entries[idx];
            _entries.RemoveAt(idx);

            // Write to end of our stream...
            AddFile(entry.Path, data, entry);
        }


        public void RemoveByName(string name)
        {
            Console.WriteLine("Remove " + name);
            _entries.RemoveAll(e => string.Equals(e.Name, name, StringComparison.InvariantCultureIgnoreCase));
        }

        public void RemoveByPath(string path)
        {
            path = NormalizePath(path);
            Console.WriteLine("Remove " + path);

            _entries.RemoveAll(e => e.Path == path);
        }

        public void AddFile(string path, byte[] data)
        {
            path = NormalizePath(path);

            var idx = _entries.FindIndex(e => e.Path == path);

            FileEntry existingEntry = default;
            if (idx >= 0)
            {
                existingEntry = _entries[idx];
                _entries.RemoveAt(idx);
            }

            AddFile(path, data, existingEntry);
        }

        public void AddDirectory(string sourcePath, string destinationPath)
        {
            if (!Directory.Exists(sourcePath))
            {
                Console.Error.WriteLine("Directory does not exist!");
                return;
            }

            foreach (var file in Directory.EnumerateFiles(sourcePath))
            {
                Console.WriteLine($"Adding file from directory: {Path.GetFileName(file)}");
                AddFile(Path.Combine(destinationPath, Path.GetFileName(file)), File.ReadAllBytes(file));
            }
        }

        private void AddFile(string path, byte[] data, FileEntry baseEntry)
        {
            using var writer = new BinaryWriter(_stream, _Encoding, true);

            // Go to end
            writer.Seek(0, SeekOrigin.End);
            var offset = writer.Seek(0, SeekOrigin.End);

            writer.Write(data);
            _entries.Add(new FileEntry()
            {
                Path = path,
                DataOffset = offset,
                IsCompressed = false,
                DataCompressedSize = (uint) data.Length,
                DataUncompressedSize = (uint) data.Length,
                Unknown1C = baseEntry.Name != null ? baseEntry.Unknown1C : 0
            });
        }

        public FileEntry Find(string fileName)
        {
            return EnumerateFiles(fileName).First();
        }

        public IEnumerable<FileEntry> EnumerateFiles(string searchFilter = "*")
        {
            return _entries.Where(file => FileSystemName.MatchesSimpleExpression(searchFilter, file.Name));
        }

        public MemoryStream Read(FileEntry entry)
        {
            var memoryStream = new MemoryStream(new byte[entry.DataUncompressedSize]);
            this._stream.Seek(entry.DataOffset, SeekOrigin.Begin);

            if (entry.IsCompressed)
            {
                ParCompression.Decompress(_stream, memoryStream, entry.DataCompressedSize);
            }
            else
            {
                this._stream.CopyRange(memoryStream, entry.DataUncompressedSize);
            }

            memoryStream.Position = 0;
            return memoryStream;
        }

        private void ReadHeader()
        {
            _stream.Seek(0, SeekOrigin.Begin);

            var magic = _reader.ReadUInt32();
            if (magic != Signature)
            {
                throw new FormatException("Bad header magic");
            }

            var version = _reader.ReadByte();
            if (version != 2)
            {
                throw new FormatException("Unsupported version");
            }

            var endianness = _reader.ReadByte();
            _reader.Endianness = endianness == 0 ? Endianness.Little : Endianness.Big;

            var headerSizeHi = _reader.ReadByte();
            var unknown07 = _reader.ReadByte(); // 0
            var unknown08 = _reader.ReadUInt32(); // 0x00020001u

            var headerSizeLo = _reader.ReadUInt32();
            var directoryCount = _reader.ReadUInt32();
            var directoryTableOffset = _reader.ReadUInt32();
            var fileCount = _reader.ReadUInt32();
            var fileTableOffset = _reader.ReadUInt32();

            var actualHeaderSize = 32L;
            actualHeaderSize += 64 * (directoryCount + fileCount);
            actualHeaderSize += 32 * directoryCount;
            actualHeaderSize += 32 * fileCount;

            var headerSize = (headerSizeHi << 32) | headerSizeLo;
            var directoryNames = new string[directoryCount];
            for (uint i = 0; i < directoryCount; i++)
            {
                directoryNames[i] = _reader.ReadString(64, _Encoding);
            }

            var fileNames = new string[fileCount];
            for (uint i = 0; i < fileCount; i++)
            {
                fileNames[i] = _reader.ReadString(64, _Encoding);
            }

            var rawDirectoryEntries = new RawDirectoryEntry[directoryCount];
            if (directoryCount > 0)
            {
                _stream.Position = directoryTableOffset;
                for (uint i = 0; i < directoryCount; i++)
                {
                    var directoryEntry = rawDirectoryEntries[i] = RawDirectoryEntry.Read(_reader);

                    if (directoryEntry.Unknown14 != 0)
                    {
                        throw new FormatException("bad directory unknown14");
                    }

                    if (directoryEntry.Unknown18 != 0)
                    {
                        throw new FormatException("bad directory unknown18");
                    }

                    if (directoryEntry.Unknown1C != 0)
                    {
                        throw new FormatException("bad directory unknown1C");
                    }
                }
            }

            var rawFileEntries = new RawFileEntry[fileCount];
            if (fileCount > 0)
            {
                _stream.Position = fileTableOffset;
                for (uint i = 0; i < fileCount; i++)
                {
                    var fileEntry = rawFileEntries[i] = RawFileEntry.Read(_reader);

                    if ((fileEntry.CompressionFlags & 0x7FFFFFFFu) != 0)
                    {
                        throw new FormatException("bad file compression flags");
                    }

                    if (fileEntry.Unknown14 != 0)
                    {
                        throw new FormatException("bad file unknown14");
                    }

                    if (fileEntry.Unknown18 != 0)
                    {
                        throw new FormatException("bad file unknown18");
                    }
                }
            }

            Queue<(int Index, string BasePath)> queue = new Queue<(int Index, string BasePath)>();
            queue.Enqueue((0, null));

            while (queue.Count > 0)
            {
                var (index, basePath) = queue.Dequeue();
                var rawDirectory = rawDirectoryEntries[index];
                var directoryName = directoryNames[index];
                var isRootDirectory = index == 0 && directoryName == ".";
                var directoryPath = basePath == null
                    ? isRootDirectory
                        ? null
                        : directoryName
                    : isRootDirectory
                        ? basePath
                        : Path.Combine(basePath, directoryName);

                for (int i = 0; i < rawDirectory.DirectoryCount; i++)
                {
                    queue.Enqueue((i + rawDirectory.DirectoryOffset, directoryPath));
                }


                for (int i = 0; i < rawDirectory.FileCount; i++)
                {
                    var fileIndex = i + rawDirectory.FileOffset;
                    var rawFileEntry = rawFileEntries[fileIndex];
                    FileEntry fileEntry;
                    fileEntry.Path = directoryPath == null
                        ? fileNames[fileIndex]
                        : Path.Combine(directoryPath, fileNames[fileIndex]);
                    fileEntry.IsCompressed = ((FileFlags) rawFileEntry.CompressionFlags & FileFlags.IsCompressed) != 0;
                    fileEntry.DataUncompressedSize = rawFileEntry.DataUncompressedSize;
                    fileEntry.DataCompressedSize = rawFileEntry.DataCompressedSize;
                    fileEntry.DataOffset = (long) (rawFileEntry.DataOffsetLo) << 0 | (long) (rawFileEntry.DataOffsetHi) << 32;
                    fileEntry.Unknown10 = rawFileEntry.HeaderSize;
                    fileEntry.Unknown14 = rawFileEntry.Unknown14;
                    fileEntry.Unknown18 = rawFileEntry.Unknown18;
                    fileEntry.Unknown1C = rawFileEntry.Unknown1C;

                    _entries.Add(fileEntry);
                }
            }

            // _entries = _entries.OrderBy(e => e.DataOffset).ToList();
            // int counter = 0;
            // for (int i = 1; i < _entries.Count; i++)
            // {
            //     var prev = _entries[i - 1];
            //     var file = _entries[i];
            //
            //     // Console.WriteLine($"[{file.DataOffset % Alignment}] {file.DataOffset - (prev.DataOffset + (prev.IsCompressed ? prev.DataCompressedSize : prev.DataUncompressedSize))}");
            //     if (file.DataOffset % Alignment != 0)
            //     {
            //         // var sameBlock = (file.DataOffset / Alignment) == ((prev.DataOffset + prev.Size) / Alignment);
            //         // counter++;
            //         // Console.WriteLine(sameBlock);
            //     }
            //     else
            //     {
            //         var sameBlock = (file.DataOffset / Alignment) == ((prev.DataOffset + prev.Size) / Alignment);
            //         counter++;
            //         Console.WriteLine(sameBlock);
            //     }
            // }

            // Console.WriteLine("Total: " + counter);
        }

        public static ArchiveFile FromStream(Stream stream)
        {
            return new ArchiveFile(stream);
        }

        public static ArchiveFile FromFile(string file)
        {
            return FromStream(File.OpenRead(file));
        }

        public static ArchiveFile FromDirectory(string directory)
        {
            var dir = new DirectoryInfo(directory);
            var archive = new ArchiveFile();

            foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var relativePath = file.FullName.Substring(dir.FullName.Length + 1);
                archive.AddFile(relativePath, File.ReadAllBytes(file.FullName));
            }

            return archive;
        }

        public void Dispose()
        {
            _reader.Dispose();
        }

        public void Save(string path, Endianness endianness = Endianness.Little)
        {
            using var fileStream = File.OpenWrite(path);
            fileStream.SetLength(0);
            Save(fileStream, endianness);
        }

        public void Save(Stream stream, Endianness endianness = Endianness.Little)
        {
            stream.SetLength(0);
            var writer = new EndiannessAwareBinaryWriter(stream);
            var root = BuildTree();

            List<string> fileNames = new List<string>();
            List<string> directoryNames = new List<string>();
            List<RawDirectoryEntry> dirEntries = new List<RawDirectoryEntry>();
            List<FileEntry> fileEntries = new List<FileEntry>();

            Queue<(DirectoryEntry Child, int Index)> queue = new Queue<(DirectoryEntry, int)>();

            queue.Enqueue((root, 0));
            directoryNames.Add(".");
            dirEntries.Add(default);

            while (queue.Count > 0)
            {
                var (dir, index) = queue.Dequeue();
                var entry = new RawDirectoryEntry();
                entry.DirectoryOffset = directoryNames.Count;
                entry.DirectoryCount = dir.Directories.Length;
                entry.FileOffset = fileNames.Count;
                entry.FileCount = dir.Files.Length;
                entry.Unknown10 = 0x10;
                dirEntries[index] = entry;

                foreach (var subdir in dir.Directories.OrderBy(dir => Path.GetFileName(dir.Path).ToLowerInvariant(), _NameComparer))
                {
                    var idx = directoryNames.Count;
                    directoryNames.Add(Path.GetFileName(subdir.Path));
                    dirEntries.Add(default);

                    queue.Enqueue((subdir, idx));
                }

                foreach (var file in dir.Files.OrderBy(f => f.Name.ToLowerInvariant(), _NameComparer))
                {
                    fileNames.Add(file.Name);
                    fileEntries.Add(file);
                }
            }

            var dirTableOffset = 32L + 64 * (directoryNames.Count + fileNames.Count);
            var fileTableOffset = dirTableOffset + directoryNames.Count * 32;
            var headerSize = fileTableOffset + fileNames.Count * 32;

            var headerSizeHi = (byte) (headerSize >> 32);
            var headerSizeLo = (uint) headerSize;

            writer.Write(Signature);
            writer.Write((byte) 2);
            writer.Write((byte) (endianness == Endianness.Little ? 0 : 1));

            writer.Endianness = endianness;

            writer.Write((byte) 0); // hi
            writer.Write((byte) 0);
            writer.Write(0x00020001u);

            writer.Write((uint) 0); // lo
            writer.Write((uint) directoryNames.Count);
            writer.Write((uint) dirTableOffset);
            writer.Write((uint) fileNames.Count);
            writer.Write((uint) fileTableOffset);

            foreach (var dir in directoryNames)
            {
                writer.WriteString(dir, 64, _Encoding);
            }

            foreach (var file in fileNames)
            {
                writer.WriteString(file, 64, _Encoding);
            }

            foreach (var dir in dirEntries)
            {
                dir.Write(writer);
            }

            long offset = headerSize;

            foreach (var file in fileEntries)
            {
                offset = offset.Aligned(Alignment, file.Size);
                var raw = RawFileEntry.FromEntry(file, offset);
                raw.Write(writer);

                offset += file.Size;
            }

            foreach (var file in fileEntries)
            {
                writer.WriteAlignment(Alignment, file.Size);
                _stream.Position = file.DataOffset;
                _stream.CopyRange(stream, file.Size);
            }

            writer.WriteAlignment(Alignment);
        }

        private int CountSubDirectories(DirectoryEntry directory)
        {
            int count = 0;
            foreach (var dir in directory.Directories)
            {
                count += CountSubDirectories(directory) + 1;
            }

            return count;
        }

        private DirectoryEntry BuildTree()
        {
            var entries = _entries.OrderBy(e => e.Path.ToLowerInvariant(), _NameComparer).ToArray();
            int offset = 0;
            return BuildTree("", ref offset, entries);
        }

        private DirectoryEntry BuildTree(string path, ref int offset, FileEntry[] sortedEntries)
        {
            var entry = new DirectoryEntry {Path = path};
            var files = new List<FileEntry>();
            var dirs = new List<DirectoryEntry>();

            if (path == "")
            {
                entry.Path = ".";
            }

            for (; offset < sortedEntries.Length; offset++)
            {
                var e = sortedEntries[offset];
                var dir = Path.GetDirectoryName(e.Path);
                if (dir == path)
                {
                    files.Add(e);
                }
                else if (IOUtils.IsSubDirectory(path, dir))
                {
                    var dirName = IOUtils.GetTopDirectory(dir, path);
                    dirs.Add(BuildTree(Path.Combine(path, dirName), ref offset, sortedEntries));
                }
                else
                {
                    // Couldn't deal with this.
                    offset--;
                    break;
                }
            }

            entry.Files = files.ToArray();
            entry.Directories = dirs.ToArray();

            return entry;
        }

        private struct RawDirectoryEntry
        {
            public int DirectoryCount;
            public int DirectoryOffset;
            public int FileCount;
            public int FileOffset;
            public uint Unknown10;
            public uint Unknown14;
            public uint Unknown18;
            public uint Unknown1C;

            public static RawDirectoryEntry Read(EndiannessAwareBinaryReader input)
            {
                RawDirectoryEntry instance;
                instance.DirectoryCount = input.ReadInt32();
                instance.DirectoryOffset = input.ReadInt32();
                instance.FileCount = input.ReadInt32();
                instance.FileOffset = input.ReadInt32();
                instance.Unknown10 = input.ReadUInt32();
                instance.Unknown14 = input.ReadUInt32();
                instance.Unknown18 = input.ReadUInt32();
                instance.Unknown1C = input.ReadUInt32();
                return instance;
            }

            public void Write(EndiannessAwareBinaryWriter output)
            {
                output.Write(DirectoryCount);
                output.Write(DirectoryOffset);
                output.Write(FileCount);
                output.Write(FileOffset);
                output.Write(Unknown10);
                output.Write(Unknown14);
                output.Write(Unknown18);
                output.Write(Unknown1C);
            }
        }

        private struct RawFileEntry
        {
            public uint CompressionFlags;
            public uint DataUncompressedSize;
            public uint DataCompressedSize;
            public uint DataOffsetLo;
            public uint HeaderSize; // probably header size?
            public uint Unknown14;
            public uint Unknown18;
            public uint Unknown1C; // probably data hash?

            public byte DataOffsetHi
            {
                get { return (byte) (this.Unknown14 & 0xFu); }
                set
                {
                    this.Unknown14 &= ~0xFu;
                    this.Unknown14 |= value & 0xFu;
                }
            }

            public static RawFileEntry Read(EndiannessAwareBinaryReader input)
            {
                RawFileEntry instance;
                instance.CompressionFlags = input.ReadUInt32();
                instance.DataUncompressedSize = input.ReadUInt32();
                instance.DataCompressedSize = input.ReadUInt32();
                instance.DataOffsetLo = input.ReadUInt32();
                instance.HeaderSize = input.ReadUInt32();
                instance.Unknown14 = input.ReadUInt32();
                instance.Unknown18 = input.ReadUInt32();
                instance.Unknown1C = input.ReadUInt32();
                return instance;
            }

            public static RawFileEntry FromEntry(FileEntry entry, long offset)
            {
                var e = new RawFileEntry();
                e.DataUncompressedSize = entry.DataUncompressedSize;
                e.DataCompressedSize = entry.DataCompressedSize;
                e.DataOffsetLo = (uint) offset;
                e.DataOffsetHi = (byte) ((offset >> 32) & 0xFu);
                e.HeaderSize = 0x20;
                e.Unknown1C = entry.Unknown1C;
                e.Unknown14 = entry.Unknown14;
                e.Unknown18 = entry.Unknown18;

                // e.Unknown1C = 0x5C_79_10_A5;
                // e.Unknown10 = 0x00_00_00_20;
                e.CompressionFlags = entry.IsCompressed ? 0x80000000u : 0u;

                // e.DataUncompressedSize = e.DataUncompressedSize;
                // e.DataCompressedSize = e.DataUncompressedSize;
                // e.CompressionFlags = 0u;

                return e;
            }

            public void Write(EndiannessAwareBinaryWriter output)
            {
                output.Write(CompressionFlags);
                output.Write(DataUncompressedSize);
                output.Write(DataCompressedSize);
                output.Write(DataOffsetLo);
                output.Write(HeaderSize);
                output.Write(Unknown14);
                output.Write(Unknown18);
                output.Write(Unknown1C);
            }
        }

        private struct DirectoryEntry
        {
            public string Path;
            public DirectoryEntry[] Directories;
            public FileEntry[] Files;
        }

        [Flags]
        public enum FileFlags : uint
        {
            None = 0u,
            IsCompressed = 1u << 31,
        }

        public void Swap(string lhsName, string rhsName)
        {
            Console.WriteLine($"{lhsName} <=> {rhsName}");
            var lhs = Find(lhsName);
            var rhs = Find(rhsName);

            var lhsData = Read(lhs).ToArray();
            var rhsData = Read(rhs).ToArray();

            ReplaceFile(rhs.Name, lhsData);
            ReplaceFile(lhs.Name, rhsData);
        }

        public void Extract(string path)
        {
            _entries
                .Select(e =>
                {
                    try
                    {
                        return (e.Path, Data: Read(e));
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("FAIL " + e.Path);
                        return (e.Path, Data: null);
                    }
                })
                .Where(f => f.Data != null)
                .AsParallel()
                .ForAll(file =>
                {
                    var outPath = Path.Combine(path, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    using var output = File.OpenWrite(outPath);
                    output.SetLength(0);
                    file.Data.CopyTo(output);
                });
        }

        private static string NormalizePath(string path)
        {
            return path.Replace("/", "\\");
        }
    }

    public struct FileEntry
    {
        public string Path;
        public string Name => System.IO.Path.GetFileName(Path);
        public uint Size => IsCompressed ? DataCompressedSize : DataUncompressedSize;

        public bool IsCompressed;
        public uint DataUncompressedSize;
        public uint DataCompressedSize;
        public long DataOffset;

        public uint Unknown10;
        public uint Unknown14;
        public uint Unknown18;
        public uint Unknown1C;


        public override string ToString()
        {
            return this.Path ?? base.ToString();
        }
    }
}