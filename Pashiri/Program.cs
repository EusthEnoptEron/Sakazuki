using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using Sakazuki;
using Sakazuki.Intermediate;

namespace Pashiri
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var file in args.Where(f => File.Exists(f) || Directory.Exists(f)))
            {
                Console.WriteLine("Processing " + file + "...");
                try
                {
                    HandleFile(file);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }
        }

        private static void HandleFile(string path)
        {
            if (Directory.Exists(path))
            {
                Console.WriteLine($"Packing {Path.GetFileName(path)}...");
                using var archive = ArchiveFile.FromDirectory(path);
                if (File.Exists(path + ".par") && !File.Exists(path + ".par.bak"))
                {
                    File.Copy(path + ".par", path + ".par.bak");
                }

                archive.Save(File.OpenWrite(path + ".par"));
            }
            else
            {
                switch (Path.GetExtension(path))
                {
                    case ".par":
                        Console.WriteLine($"Extracting {Path.GetFileName(path)}");
                        using (var archive = ArchiveFile.FromFile(path))
                        {
                            archive.Extract(Path.ChangeExtension(path, null));
                        }

                        break;
                    case ".gmd":
                        Console.WriteLine($"Exporting {Path.GetFileName(path)}");
                        var gmdFile = GmdFile2.FromFile(path);
                        var mesh = YakuzaMesh.FromGmdFile(gmdFile);
                        mesh.SaveToGltf2(Path.ChangeExtension(path, ".glb"), FindTextureFolder(path));
                        break;
                }
            }
        }

        private static string FindTextureFolder(string basePath)
        {
            basePath = Path.GetFullPath(basePath);
            var parent = Path.GetDirectoryName(basePath);
            while (parent != null)
            {
                if (Directory.Exists(Path.Combine(parent, "dds")))
                {
                    return Path.Combine(parent, "dds");
                }
                parent = Path.GetDirectoryName(parent);
            }

            return Path.GetDirectoryName(basePath);
        }
    }
}