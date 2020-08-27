using System;
using System.IO;
using System.Text;
using ImageMagick;

namespace Sakazuki
{
    public static class ParCompression
    {
        public static void Decompress(Stream input, Stream output, uint byteCount)
        {
            using var reader = new EndiannessAwareBinaryReader(input, Encoding.ASCII, true);
            const uint signature = 0x534C4C5A; // 'SLLZ'

            var magic = reader.ReadUInt32(Endianness.Big);
            if (magic != signature)
            {
                throw new FormatException();
            }

            var endianness = reader.ReadByte();
            if (endianness != 0 && endianness != 1)
            {
                throw new FormatException();
            }

            var endian = endianness == 0 ? Endianness.Little : Endianness.Big;
            reader.Endianness = endian;

            var version = reader.ReadByte();
            if (version != 1)
            {
                throw new FormatException();
            }

            var headerSize = reader.ReadUInt16();
            if (headerSize != 16)
            {
                throw new FormatException();
            }

            var uncompressedSize = reader.ReadUInt32();
            var compressedSize = reader.ReadUInt32();

            // if (entry.DataUncompressedSize != uncompressedSize || byteCount != compressedSize)
            // {
            //     throw new FormatException();
            // }

            compressedSize -= 16; // compressed size includes SLLZ header

            var block = new byte[18];
            long compressedCount = 0;
            long uncompressedCount = 0;

            byte opFlags = reader.ReadByte();
            compressedCount++;
            int opBits = 8;

            int literalCount = 0;
            while (compressedCount < compressedSize)
            {
                var isCopy = (opFlags & 0x80) != 0;
                opFlags <<= 1;
                opBits--;

                if (opBits == 0)
                {
                    if (literalCount > 0)
                    {
                        input.Read(block, 0, literalCount);
                        output.Write(block, 0, literalCount);
                        uncompressedCount += literalCount;
                        literalCount = 0;
                    }

                    opFlags = reader.ReadByte();
                    compressedCount++;
                    opBits = 8;
                }

                if (isCopy == false)
                {
                    literalCount++;
                    compressedCount++;
                    continue;
                }

                if (literalCount > 0)
                {
                    input.Read(block, 0, literalCount);
                    output.Write(block, 0, literalCount);
                    uncompressedCount += literalCount;
                    literalCount = 0;
                }

                var copyFlags = reader.ReadUInt16(Endianness.Little);
                compressedCount += 2;

                var copyDistance = 1 + (copyFlags >> 4);
                var copyCount = 3 + (copyFlags & 0xF);

                var originalPosition = output.Position;
                output.Position = uncompressedCount - copyDistance;
                output.Read(block, 0, copyCount);
                output.Position = originalPosition;
                output.Write(block, 0, copyCount);
                uncompressedCount += copyCount;
            }

            if (literalCount > 0)
            {
                input.Read(block, 0, literalCount);
                output.Write(block, 0, literalCount);
                uncompressedCount += literalCount;
            }

            if (uncompressedCount != uncompressedSize)
            {
                throw new InvalidOperationException();
            }
        }
    }
}