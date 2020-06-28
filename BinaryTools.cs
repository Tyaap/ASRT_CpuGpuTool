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

        public static void SaveData(string inFilePath, int offset, int length, string outFilePath)
        {
            var handle = LongFile.CreateFileForWrite(outFilePath); // handle long paths
            
            using (FileStream fsIn = File.OpenRead(inFilePath))
            using (FileStream fsOut = new FileStream(handle, FileAccess.Write))
            {
                WriteData(fsIn, fsOut, length, offset);
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

        public static void ExpandStream(Stream s, int offset, int sizeChange)
        {
            byte[] buffer = new byte[bufferSize];
            int length = (int)s.Length;
            int pos = length;
            int to_read;
            s.SetLength(length + sizeChange);
            while (pos > offset)
            {
                to_read = Math.Min(bufferSize, pos - offset);
                pos -= to_read;
                s.Seek(pos, SeekOrigin.Begin);
                s.Read(buffer, 0, to_read);
                s.Seek(pos + sizeChange, SeekOrigin.Begin);
                s.Write(buffer, 0, to_read);
            }
        }
    }
}
