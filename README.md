# Sakazuki
A C# library for working with Yakuza Kiwami 2 files (and possibly other entries of the series.)

Currently supported:

- Extract *.par archives.
- Pack *.par archives.
- Convert *.gmd meshes to GLTF2.
- Convert GLTF2 meshes to *.gmd (with caveats).


| Project  | Explanation |
| ------------- | ------------- |
| Sakazuki  | Library for usage in a C# project.  |
| Pashiri  | CLI for the library.  |
| Chinpira | Testbed. |

# Usage

```csharp
// Load archive
using var archive = ArchiveFile.FromFile(@"path/to/archive.par");

// Extract contents to a folder
archive.Extract("some/path");

// Search for a file
var kiryu = archive.Find("c_am_kiryu.gmd");

// Swap files
archive.Swap("c_am_kiryu.gmd", "c_am_hanaya.glb");

// Add file
archive.AddFile(@"some\path\file.txt", Encoding.UTF8.GetBytes("This is a test"));

// Replace file
archive.ReplaceFile("c_am_kiryu.gmd", Encoding.UTF8.GetBytes("This is a test"));

// Read file (returns a stream that you can copy into a file stream.)
var dataStream = archive.Read(kiryuu);

// Read mesh data (internally, the process is [data] => [GmdFile] => [YakuzaMesh])
var mesh = YakuzaMesh.FromGmdStream(dataStream);

// Save as GLTF2
mesh.SaveToGltf2("c_am_kiryu.glb", "path/to/dds/textures");

// Load GLTF2 file
mesh = YakuzaMesh.FromGltf2("c_am_kiryu.glb", "path/to/empty/directory");

// Write back into archive
var stream = new MemoryStream();
mesh.WriteGmd(stream);
archive.ReplaceFile("c_am_kiryu.gmd", stream.ToArray());
foreach (var file in Directory.EnumerateFiles("path/to/empty/directory"))
{
    archiveIn.AddFile("lexus2\\dds\\" + Path.GetFileName(file), File.ReadAllBytes(file));
}

// Save archive
archive.Save("path/to/new/archive.par", Endianness.Big);
```

# Things to keep in mind

- When importing the models into Blender (preferably 2.90), make sure to to pick "Blender" for the Bone Dir option, otherwise their transforms will be messed up.
- It seems that Yakuza is very pedantic about the bone structure. Either leave it *exactly* as is, or delete the unneeded ones and use `YakuzaMesh.CopySkin()` to restore the bone structure.