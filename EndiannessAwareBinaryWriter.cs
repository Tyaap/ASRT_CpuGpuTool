using System;
using System.IO;
using System.Text;

namespace CpuGpuTool
{
    public class EndiannessAwareBinaryWriter : BinaryWriter
    {
        public bool IsLittleEndian = BitConverter.IsLittleEndian;

        public EndiannessAwareBinaryWriter(Stream input) : base(input)
        {
        }

        public EndiannessAwareBinaryWriter(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        public EndiannessAwareBinaryWriter(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
        }

        public EndiannessAwareBinaryWriter(Stream input, bool isLittleEndian) : base(input)
        {
            IsLittleEndian = isLittleEndian;
        }

        public EndiannessAwareBinaryWriter(Stream input, bool isLittleEndian, Encoding encoding) : base(input, encoding)
        {
            IsLittleEndian = isLittleEndian;
        }

        public EndiannessAwareBinaryWriter(Stream input, bool isLittleEndian, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
            IsLittleEndian = isLittleEndian;
        }

        public override void Write(short value) => Write(value, IsLittleEndian);

        public override void Write(int value) => Write(value, IsLittleEndian);

        public override void Write(long value) => Write(value, IsLittleEndian);

        public override void Write(ushort value) => Write(value, IsLittleEndian);

        public override void Write(uint value) => Write(value, IsLittleEndian);

        public override void Write(ulong value) => Write(value, IsLittleEndian);

        public override void Write(float value) => Write(value, IsLittleEndian);

        public override void Write(double value) => Write(value, IsLittleEndian);

        public override void Write(bool value) => Write(value, IsLittleEndian);

        public void Write(short value, bool endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);

        public void Write(int value, bool endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);

        public void Write(long value, bool endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);

        public void Write(ushort value, bool endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);
        
        public void Write(uint value, bool endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);

        public void Write(ulong value, bool endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);

        public void Write(float value, bool endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);

        public void Write(double value, bool endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);

        public void Write(bool value, bool endianness) => WriteForEndianness(BitConverter.GetBytes(value), endianness);

        private void WriteForEndianness(byte[] buffer, bool isLittleEndian)
        {
            if (isLittleEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }
            Write(buffer);
        }
    }
}
