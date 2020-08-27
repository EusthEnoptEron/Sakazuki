using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using ImageMagick;
using OpenCvSharp;
using SharpGLTF.Memory;
using SharpGLTF.Schema2;
using TeximpNet;
using TeximpNet.Compression;
using ImageFormat = Pfim.ImageFormat;

namespace Sakazuki
{
    public class ImageConverter : IDisposable
    {
        private List<string> _tempFiles = new List<string>();
        private string _texturePath;
        private Dictionary<string, string> _processedEntries = new Dictionary<string, string>();

        public ImageConverter(string texturePath)
        {
            _texturePath = texturePath;

            if (_texturePath != null)
            {
                Directory.CreateDirectory(_texturePath);
            }
        }

        public string GetName(ArraySegment<byte> bytes)
        {
            foreach (var entry in _processedEntries)
            {
                if (bytes.SequenceEqual(File.ReadAllBytes(entry.Value)))
                {
                    return entry.Key;
                }
            }

            return null;
        }


        public string GetImage(string imageName, out int channels)
        {
            return GetImage(imageName, out channels, null);
        }

        private delegate void FilterFunc(ref Vec4b pixel);

        private unsafe string GetImage(string imageName, out int channels, FilterFunc filter = null)
        {
            if (_processedEntries.TryGetValue(imageName, out var existing))
            {
                using var img = new MagickImage();
                img.Ping(existing);
                channels = img.ChannelCount;
                return existing;
            }

            try
            {
                var tempPath = Path.GetTempFileName();
                _tempFiles.Add(tempPath);

                var fullPath = Path.Combine(_texturePath, imageName + ".dds");
                if (!File.Exists(fullPath))
                {
                    channels = 0;
                    _processedEntries[imageName] = null;
                    return null;
                }

                Console.WriteLine($"Converting {imageName}...");

                using var surface = Pfim.Pfim.FromFile(fullPath);

                surface.Decompress();

                int width = surface.Width;
                int height = surface.Height;
                int offset = 0;
                int length = surface.DataLen;

                var data = new byte[length];
                Array.Copy(surface.Data, offset, data, 0, length);
                var mat = new Mat();

                if (surface.Format == ImageFormat.Rgb24)
                {
                    mat = new Mat(height, width, MatType.CV_8UC3, data);

                    channels = 3;

                    if (filter != null)
                    {
                        mat.ForEachAsVec3b((p, pos) =>
                        {
                            // We marshal a Vec3b as Vec4b which is dangerous
                            filter(ref Unsafe.AsRef<Vec4b>(p));
                        });
                    }
                }
                else if (surface.Format == ImageFormat.Rgba32)
                {
                    mat = new Mat(height, width, MatType.CV_8UC4, data);

                    channels = 4;
                    //
                    if (filter != null)
                    {
                        mat.ForEachAsVec4b((p, pos) => { filter(ref Unsafe.AsRef<Vec4b>(p)); });
                    }
                }
                else
                {
                    throw new Exception($"Unsupported format {imageName}: {surface.Format}");
                }


                using var fileStream = File.OpenWrite(tempPath);
                // mat.ImWrite(imageName + ".png");
                mat.WriteToStream(fileStream);
                _processedEntries[imageName] = tempPath;

                mat.Dispose();
                return tempPath;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                _processedEntries[imageName] = null;
                channels = 0;
                return null;
            }
        }

        public string GetNormalImage(string imageName)
        {
            return GetImage(imageName, out var _, (ref Vec4b pixel) => { pixel[0] = (byte) (255 - pixel[0]); });
        }


        public string GetMetallicRoughnessImage(string imageName)
        {
            /*  mt.g = AO?
                mt.a = AO Opacity?
                mt.r = metallic?
                mt.b = roughness?
                */
            return GetImage(imageName, out var _, (ref Vec4b pixel) =>
            {
                // b => g
                // g => r
                // r => b
                // Occlusion (R)
                pixel[2] = pixel[1];

                // Roughness (G)
                pixel[1] = (byte) (255 - pixel[0]);

                // Metallic (B)
                pixel[0] = pixel[2];
            });
        }

        public void ConvertBaseColorToDDS(Image image)
        {
            if (_processedEntries.ContainsKey(image.Name)) return;

            var data = LoadImage(image.Content);
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, data);
                ConvertToDDS(tempFile, Path.Combine(_texturePath, Path.ChangeExtension(image.Name, ".dds")!));
            }
            finally
            {
                File.Delete(tempFile);
                _processedEntries.Add(image.Name, null);
            }
        }

        public void ConvertNormalToDDS(Image image)
        {
            if (_processedEntries.ContainsKey(image.Name)) return;

            var data = LoadImage(image.Content, (ref Vec4b pixel) => { pixel[0] = (byte) (255 - pixel[0]); });
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, data);
                ConvertToDDS(tempFile, Path.Combine(_texturePath, Path.ChangeExtension(image.Name, ".dds")!));
            }
            finally
            {
                File.Delete(tempFile);
                _processedEntries.Add(image.Name, null);
            }
        }

        public void ConvertMetallicRoughnessToDDS(Image image)
        {
            if (_processedEntries.ContainsKey(image.Name)) return;

                var data = LoadImage(image.Content, (ref Vec4b pixel) =>
            {
                // Occlusion (R)
                pixel[1] = pixel[2];

                // Roughness (G)
                pixel[0] = (byte) (255 - pixel[1]);

                // Metallic (B)
                pixel[2] = pixel[0];
            });
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, data);
                ConvertToDDS(tempFile, Path.Combine(_texturePath, Path.ChangeExtension(image.Name, ".dds")!));
            }
            finally
            {
                File.Delete(tempFile);
                _processedEntries.Add(image.Name, null);
            }
        }

        private unsafe byte[] LoadImage(MemoryImage image, FilterFunc filter = null)
        {
            using var originalMat = Mat.FromImageData(image.Content.ToArray());
            using var mat = originalMat.Flip(FlipMode.X);

            if (filter != null)
            {
                if (mat.Channels() == 3)
                {
                    mat.ForEachAsVec3b((p, pos) =>
                    {
                        // We marshal a Vec3b as Vec4b which is dangerous
                        filter(ref Unsafe.AsRef<Vec4b>(p));
                    });
                }
                else if (mat.Channels() == 4)
                {
                    mat.ForEachAsVec4b((p, pos) => { filter(ref Unsafe.AsRef<Vec4b>(p)); });
                }
                else
                {
                    throw new Exception($"Unsupported channel count {mat.Channels()}");
                }
            }


            using var memory = new MemoryStream();
            mat.WriteToStream(memory);

            return memory.ToArray();
        }

        public void GenerateMetallicRoughnessOcclusion(string name, float metallic, float roughness, float occlusion)
        {
            using var mat = new Mat(4, 4, MatType.CV_8UC4);
            var vec = new Scalar(
                (byte) ((1 - roughness) * 255),
                (byte) (occlusion * 255),
                (byte) (metallic * 255)
            );

            mat.SetTo(vec);

            using var memory = new MemoryStream();
            mat.WriteToStream(memory);

            var tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, memory.ToArray());
            try
            {
                ConvertToDDS(tempFile, Path.Combine(_texturePath, Path.ChangeExtension(name, ".dds")));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }


        private string ConvertToDDS(string input, string output, double? factor = null)
        {
            // var output = Path.ChangeExtension(input, ".dds");
            using (var albedo = Surface.LoadFromFile(input))
            using (var compressor = new Compressor())
            {
                if (factor.HasValue)
                {
                    albedo.Resize((int) Math.Round(albedo.Width * factor.Value), (int) Math.Round(albedo.Height * factor.Value), ImageFilter.Lanczos3);
                }

                compressor.Compression.Format = CompressionFormat.DXT1;
                compressor.Compression.Quality = CompressionQuality.Normal;
                compressor.Input.SetMipmapGeneration(true);
                compressor.Input.MipmapFilter = MipmapFilter.Kaiser;
                compressor.Input.SetData(albedo);
                compressor.Process(output);
            }

            return output;
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    Console.Error.WriteLine($"Failed to clean up {file}");
                }
            }
        }
    }
}