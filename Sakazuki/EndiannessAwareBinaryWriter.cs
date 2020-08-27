using System;
using System.IO;
using System.Text;

namespace Sakazuki
{
    public class EndiannessAwareBinaryWriter : BinaryWriter
    {
        public Endianness Endianness { get; set; } = Endianness.Little;

        public EndiannessAwareBinaryWriter(Stream input) : base(input)
        {
        }

        public EndiannessAwareBinaryWriter(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        public EndiannessAwareBinaryWriter(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
        }

        public EndiannessAwareBinaryWriter(Stream input, Endianness endianness) : base(input)
        {
            Endianness = endianness;
        }

        public EndiannessAwareBinaryWriter(Stream input, Encoding encoding, Endianness endianness) : base(input, encoding)
        {
            Endianness = endianness;
        }

        public EndiannessAwareBinaryWriter(Stream input, Encoding encoding, bool leaveOpen, Endianness endianness) : base(input, encoding, leaveOpen)
        {
            Endianness = endianness;
        }

        public override void Write(short value) => Write(value, Endianness);
        public override void Write(ushort value) => Write(value, Endianness);
        public override void Write(int value) => Write(value, Endianness);
        public override void Write(uint value) => Write(value, Endianness);
        public override void Write(long value) => Write(value, Endianness);
        public override void Write(ulong value) => Write(value, Endianness);

        public void Write(short value, Endianness endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);
        public void Write(ushort value, Endianness endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);
        public void Write(int value, Endianness endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);
        public void Write(uint value, Endianness endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);
        public void Write(long value, Endianness endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);
        public void Write(ulong value, Endianness endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);

        private void WriteForEndianness(byte[] bytes, Endianness endianness)
        {
            if ((endianness == Endianness.Little && !BitConverter.IsLittleEndian)
                || (endianness == Endianness.Big && BitConverter.IsLittleEndian))
            {
                Array.Reverse(bytes);
            }

            Write(bytes);
        }
    }
}