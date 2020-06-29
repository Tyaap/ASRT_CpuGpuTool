using System;
using System.Collections.Generic;
using System.IO;

namespace CpuGpuTool
{
    public static class BinaryTools
    {
        const int bufferSize = 4096;

        public static string ReadString(Stream s)
        {
            List<char> chars = new List<char>();
            char nextChar = (char)s.ReadByte();
            while (nextChar != (char)0)
            {
                chars.Add(nextChar);
                nextChar = (char)s.ReadByte();
            }
            return new string(chars.ToArray());
        }

        public static void WriteString(Stream s, string str)
        {
            int length = str.Length;
            byte[] bytes = new byte[length + 1];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = (byte)str[i];
            }
            bytes[length] = 0;
            s.Write(bytes, 0, length + 1);
        }

        public static void WriteData(string inFilePath, string outFilePath, int length, int inOffset = 0, int outOffset = 0)
        {
            using (FileStream fsIn = LongFile.GetFileStream(inFilePath))
            using (FileStream fsOut = LongFile.Exists(outFilePath) ? 
                LongFile.GetFileStream(outFilePath) : new FileStream(LongFile.CreateFileForWrite(outFilePath), FileAccess.Write))
            {
                WriteData(fsIn, fsOut, length, inOffset, outOffset);
            }
        }

        public static void WriteData(string inFilePath, Stream sOut, int length, int inOffset = 0, int outOffset = 0)
        {
            using (FileStream fsIn = LongFile.GetFileStream(inFilePath))
            {
                WriteData(fsIn, sOut, length, inOffset, outOffset);
            }
        }

        public static void WriteData(Stream sIn, Stream sOut, int length, int inOffset = 0, int outOffset = 0)
        {
            sIn.Seek(inOffset, SeekOrigin.Begin);
            sOut.Seek(outOffset, SeekOrigin.Begin);
            byte[] buffer = new byte[bufferSize];
            int pos = 0;
            while (pos < length)
            {
                int to_read = Math.Min(bufferSize, length - pos);
                pos += to_read;
                sIn.Read(buffer, 0, to_read);
                sOut.Write(buffer, 0, to_read);
            }
        }

        public static void InsertData(Stream sIn, Stream sOut, int length, int inOffset = 0, int outOffset = 0)
        {
            ExpandStream(sOut, outOffset, length);
            WriteData(sIn, sOut, length, inOffset, outOffset);
        }

        public static void ExpandStream(Stream s, int offset, int sizeIncrease)
        {
            byte[] buffer = new byte[bufferSize];
            int length = (int)s.Length;
            int pos = length;
            int to_read;
            s.SetLength(length + sizeIncrease);
            while (pos > offset)
            {
                to_read = Math.Min(bufferSize, pos - offset);
                pos -= to_read;
                s.Seek(pos, SeekOrigin.Begin);
                s.Read(buffer, 0, to_read);
                s.Seek(pos + sizeIncrease, SeekOrigin.Begin);
                s.Write(buffer, 0, to_read);
            }
        }

        public static void ShrinkStream(Stream s, int offset, int sizeDecrease)
        {
            byte[] buffer = new byte[bufferSize];
            int length = (int)s.Length;
            int pos = offset;
            int to_read;
            while (pos < length)
            {
                to_read = Math.Min(bufferSize, length - pos); 
                s.Seek(pos + sizeDecrease, SeekOrigin.Begin);
                s.Read(buffer, 0, to_read);
                s.Seek(pos, SeekOrigin.Begin);
                s.Write(buffer, 0, to_read);
                pos += to_read;
            }
            s.SetLength(length - sizeDecrease);
        }
    }
}
