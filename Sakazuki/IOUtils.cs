using System;
using System.IO;
using System.Text;

namespace Sakazuki
{
    public static class IOUtils
    {
        private static byte[] EmptyBuffer = new byte[2048];

        public static string ReadString(this BinaryReader reader, int length, Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.ASCII;

            var bytes = reader.ReadBytes(length);
            var stopIdx = Array.IndexOf(bytes, (byte) 0);

            return encoding.GetString(bytes, 0, stopIdx < 0 ? length : stopIdx);
        }

        public static long WriteAlignment(this BinaryWriter writer, int alignment, uint requiredSize = uint.MaxValue)
        {
            long bytesToFill = writer.BaseStream.Position.Aligned(alignment, requiredSize) - writer.BaseStream.Position;
            long bytesToFillCopy = bytesToFill;
            while (bytesToFill > 0)
            {
                int bytesWritten = (int) Math.Min(bytesToFill, EmptyBuffer.Length);
                writer.Write(EmptyBuffer, 0, bytesWritten);
                bytesToFill -= bytesWritten;
            }

            return bytesToFillCopy;

            // writer.BaseStream.SetLength(writer.BaseStream.Position.Align(alignment));
            // writer.BaseStream.Seek(0, SeekOrigin.End);
        }

        public static byte[] ReadBytes(this BinaryReader reader, int count, long offset)
        {
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);

            return reader.ReadBytes(count);
        }

        public static long Aligned(this long offset, int alignment)
        {
            return offset + (alignment - (offset % alignment));
        }

        public static long Aligned(this long offset, int alignment, long requiredSize)
        {
            if (offset / alignment == (offset + requiredSize) / alignment)
            {
                return offset;
            }

            if (offset % alignment == 0)
            {
                return offset;
            }

            return offset.Aligned(alignment);
        }

        public static void WriteString(this BinaryWriter writer, string text, int length, Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.ASCII;

            var bytes = encoding.GetBytes(text);
            for (int i = 0; i < length; i++)
            {
                if (i < bytes.Length)
                {
                    writer.Write(bytes[i]);
                }
                else
                {
                    writer.Write((byte) 0);
                }
            }
        }

        public static void Write(this BinaryWriter writer, uint[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                writer.Write(values[i]);
            }
        }

        public static void Write(this BinaryWriter writer, int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                writer.Write(values[i]);
            }
        }

        public static bool IsSubDirectory(string basePath, string subPath)
        {
            if (basePath == "" || basePath == ".")
            {
                return true;
            }

            return subPath.StartsWith(basePath + Path.DirectorySeparatorChar);
        }

        public static string GetTopDirectory(string path, string basePath = "")
        {
            if (path == "" || path == ".") return path;

            var dir = path.Substring(basePath.Length);
            if (dir.StartsWith(Path.DirectorySeparatorChar))
            {
                dir = dir.Substring(1);
            }

            var trailingIdx = dir.IndexOf(Path.DirectorySeparatorChar);
            if (trailingIdx >= 0)
            {
                dir = dir.Substring(0, trailingIdx);
            }

            return dir;
        }

        public static void CopyRange(this Stream input, Stream output, long bytes)
        {
            byte[] buffer = new byte[32768];
            int read;
            while (bytes > 0 &&
                   (read = input.Read(buffer, 0, (int) Math.Min(buffer.Length, bytes))) > 0)
            {
                output.Write(buffer, 0, read);
                bytes -= read;
            }
        }

        public static float ReadByteAsFloat(this BinaryReader br)
        {
            return br.ReadByte() / 255.0f;
        }
    }
}