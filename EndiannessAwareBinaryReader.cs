using System;
using System.IO;
using System.Text;

namespace CpuGpuTool
{
    public class EndiannessAwareBinaryReader : BinaryReader
    {
        public bool IsLittleEndian = BitConverter.IsLittleEndian;

        public EndiannessAwareBinaryReader(Stream input) : base(input)
        {
        }

        public EndiannessAwareBinaryReader(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        public EndiannessAwareBinaryReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
        }

        public EndiannessAwareBinaryReader(Stream input, bool isLittleEndian) : base(input)
        {
            IsLittleEndian = isLittleEndian;
        }

        public EndiannessAwareBinaryReader(Stream input, bool isLittleEndian, Encoding encoding) : base(input, encoding)
        {
            IsLittleEndian = isLittleEndian;
        }

        public EndiannessAwareBinaryReader(Stream input, bool isLittleEndian, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
            IsLittleEndian = isLittleEndian;
        }

        public override short ReadInt16() => ReadInt16(IsLittleEndian);

        public override int ReadInt32() => ReadInt32(IsLittleEndian);

        public override long ReadInt64() => ReadInt64(IsLittleEndian);

        public override ushort ReadUInt16() => ReadUInt16(IsLittleEndian);

        public override uint ReadUInt32() => ReadUInt32(IsLittleEndian);

        public override ulong ReadUInt64() => ReadUInt64(IsLittleEndian);

        public override float ReadSingle() => ReadSingle(IsLittleEndian);

        public override double ReadDouble() => ReadDouble(IsLittleEndian);

        public override bool ReadBoolean() => ReadBoolean(IsLittleEndian);

        public short ReadInt16(bool isLittleEndian) => BitConverter.ToInt16(ReadForEndianness(sizeof(short), isLittleEndian), 0);

        public int ReadInt32(bool isLittleEndian) => BitConverter.ToInt32(ReadForEndianness(sizeof(int), isLittleEndian), 0);

        public long ReadInt64(bool isLittleEndian) => BitConverter.ToInt64(ReadForEndianness(sizeof(long), isLittleEndian), 0);

        public ushort ReadUInt16(bool isLittleEndian) => BitConverter.ToUInt16(ReadForEndianness(sizeof(ushort), isLittleEndian), 0);

        public uint ReadUInt32(bool isLittleEndian) => BitConverter.ToUInt32(ReadForEndianness(sizeof(uint), isLittleEndian), 0);

        public ulong ReadUInt64(bool isLittleEndian) => BitConverter.ToUInt64(ReadForEndianness(sizeof(ulong), isLittleEndian), 0);

        public float ReadSingle(bool isLittleEndian) => BitConverter.ToSingle(ReadForEndianness(sizeof(short), isLittleEndian), 0);

        public double ReadDouble(bool isLittleEndian) => BitConverter.ToDouble(ReadForEndianness(sizeof(double), isLittleEndian), 0);

        public bool ReadBoolean(bool isLittleEndian) => BitConverter.ToInt32(ReadForEndianness(sizeof(int), isLittleEndian), 0) == 1;

        private byte[] ReadForEndianness(int bytesToRead, bool isLittleEndian)
        {
            byte[] bytesRead = ReadBytes(bytesToRead);
            if (isLittleEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytesRead);
            }
            return bytesRead;
        }
    }
}
