using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Sakazuki;
using Sakazuki.Intermediate;

namespace Chinpira
{
    class Program
    {
        static void Main(string[] args)
        {
            var ddsPath = @"D:\Program Files (x86)\Steam\steamapps\common\Yakuza Kiwami 2\data\chara_unpack\lexus2\dds";
            using var archiveIn = ArchiveFile.FromFile(@"D:\Program Files (x86)\Steam\steamapps\common\Yakuza Kiwami 2\data\chara_original.par");
            using var archiveOut = File.OpenWrite(@"D:\Program Files (x86)\Steam\steamapps\common\Yakuza Kiwami 2\data\chara.par");

            var fileName = "c_am_kiryu.gmd";
            // var fileName = "c_am_dummy_01.gmd";
            // var fileName = "c_aw_haruka.gmd";
            // var fileName = "c_am_S03_soutenboripl.gmd";

            
            var kiryu = archiveIn.Find(fileName);
            using var tempFile = new MemoryStream();
            archiveIn.Read(kiryu).CopyTo(tempFile);
            tempFile.Seek(0, SeekOrigin.Begin);

            var gmd = GmdFile.FromStream(tempFile);
            var mesh = YakuzaMesh.FromGmdFile(gmd);
            
            mesh.SaveToGltf2("mesh.glb", ddsPath);
            if (Directory.Exists("textures"))
            {
                Directory.Delete("textures", true);
            }

            var mesh1 = YakuzaMesh.FromGlbFile("mesh_edited.glb", "textures");
            foreach (var file in Directory.EnumerateFiles("textures"))
            {
                Console.WriteLine($"Adding texture: {Path.GetFileName(file)}");
                archiveIn.AddFile("lexus2\\dds\\" + Path.GetFileName(file), File.ReadAllBytes(file));
            }

            var gmd2 = mesh1.ToGmdFile();

            tempFile.Seek(0, SeekOrigin.Begin);
            gmd2.Write(tempFile);


            tempFile.Seek(0, SeekOrigin.Begin);
            archiveIn.ReplaceFile(fileName, tempFile.ToArray());

            archiveIn.Save(archiveOut, Endianness.Big);
        }

        private static void ExtractEverything()
        {
            var modelDir = Directory.CreateDirectory("models");
            var texDir = Directory.CreateDirectory("models\\textures");

            using var archive = ArchiveFile.FromFile(@"D:\Program Files (x86)\Steam\steamapps\common\Yakuza Kiwami 2\data\chara.par");
            foreach (var texFile in archive.EnumerateFiles("*.dds"))
            {
                var targetFile = Path.Combine(texDir.FullName, texFile.Name);
                if (!File.Exists(targetFile))
                {
                    using var input = archive.Read(texFile);
                    using var output = File.OpenWrite(targetFile);
                    input.CopyTo(output);
                }
            }
        }
    }
}