using System;
using System.Collections.Generic;
using System.IO;

namespace CpuGpuTool
{
    public static class BinaryTools
    {
        const int bufferSize = 4096;

        public static string ReadString(BinaryReader b)
        {
            List<char> chars = new List<char>();
            char nextChar = (char)b.ReadByte();
            while (nextChar != (char)0)
            {
                chars.Add(nextChar);
                nextChar = (char)b.ReadByte();
            }
            return new string(chars.ToArray());
        }


        public static void SaveData(string inFilePath, int offset, int length, string outFilePath)
        {
            using (FileStream fsIn = File.OpenRead(inFilePath))
            using (FileStream fsOut = File.Create(outFilePath))
            {
                WriteData(fsIn, fsOut, length, offset);
            }
        }

        public static void WriteData(FileStream fsIn, FileStream fsOut, int length, int inOffset = 0, int outOffset = 0)
        {
            fsIn.Seek(inOffset, SeekOrigin.Begin);
            fsOut.Seek(outOffset, SeekOrigin.Begin);
            byte[] buffer = new byte[bufferSize];
            int pos = 0;
            while (pos < length)
            {
                int to_read = Math.Min(bufferSize, length - pos);
                pos += to_read;
                fsIn.Read(buffer, 0, to_read);
                fsOut.Write(buffer, 0, to_read);
            }
        }

        public static void ExpandFile(FileStream fs, int offset, int sizeChange)
        {
            byte[] buffer = new byte[bufferSize];
            int length = (int)fs.Length;
            int pos = length;
            int to_read;
            fs.SetLength(length + sizeChange);
            while (pos > offset)
            {
                to_read = Math.Min(bufferSize, pos - offset);
                pos -= to_read;
                fs.Seek(pos, SeekOrigin.Begin);
                fs.Read(buffer, 0, to_read);
                fs.Seek(pos + sizeChange, SeekOrigin.Begin);
                fs.Write(buffer, 0, to_read);
            }
        }
    }
}
