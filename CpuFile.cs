using System;
using System.Collections.Generic;
using System.IO;

namespace CpuGpuTool
{
    public class CpuFile
    {
        public string cpuFilePath;
        public string gpuFilePath;
        public List<CpuEntry> entries;
        public HashSet<DataType> usedDataTypes;
        public static readonly char[] invalidCharacters = { ':', '*', '?', '<', '>', '|' };
        public bool isLittleEndian;

        public CpuFile()
        {
            cpuFilePath = "";
            gpuFilePath = "";
            entries = new List<CpuEntry>();
        }
        public CpuFile(string filePath)
        {
            cpuFilePath = filePath;
            gpuFilePath = filePath.Replace(".cpu.", ".gpu.");
            (entries, usedDataTypes, isLittleEndian) = Parse(filePath);
        }

        public static (List<CpuEntry>, HashSet<DataType>, bool) Parse(string filePath)
        {
            List<CpuEntry> entries = new List<CpuEntry>();
            HashSet<DataType> usedDataTypes = new HashSet<DataType>();
            bool isLittleEndian;
            using (EndiannessAwareBinaryReader b = new EndiannessAwareBinaryReader(File.OpenRead(filePath)))
            {
                int test = b.ReadInt32();
                isLittleEndian = Enum.IsDefined(typeof(DataType), test);
                b.IsLittleEndian = isLittleEndian;

                int cpuOffset = 0;
                int gpuOffset = 0;
                while (cpuOffset < b.BaseStream.Length)
                {
                    b.BaseStream.Seek(cpuOffset, SeekOrigin.Begin);
                    CpuEntry entry = new CpuEntry
                    {
                        entryNumber = entries.Count + 1,
                        dataType = (DataType)b.ReadInt32(),
                        toolVersion = b.ReadInt32(),
                        cpuOffsetHeader = cpuOffset,
                        cpuOffsetData = cpuOffset + 0x20,
                        cpuOffsetNextEntry = b.ReadInt32(),
                        cpuDataLength = b.ReadInt32(),
                        gpuOffsetData = gpuOffset,
                        gpuOffsetNextEntry = b.ReadInt32(),
                        gpuDataLength = b.ReadInt32(),
                        unknown = b.ReadInt32(),
                        entryType = b.ReadBoolean() ? EntryType.Resource : EntryType.Node,
                        name = "[No name found]"
                    };

                    b.BaseStream.Seek(entry.cpuOffsetData + (entry.entryType == EntryType.Resource ? 4 : 8), SeekOrigin.Begin);
                    int nameOffset = b.ReadInt32();
                    if (nameOffset != 0)
                    {
                        b.BaseStream.Seek(entry.cpuOffsetData + nameOffset, SeekOrigin.Begin);
                        entry.name = BinaryTools.ReadString(b.BaseStream);
                        entry.id = NameChecksum(entry.name);
                    }

                    if (entry.dataType != DataType.Nothing)
                    {
                        entries.Add(entry);
                        usedDataTypes.Add(entry.dataType);
                    }

                    cpuOffset += entry.cpuOffsetNextEntry;
                    gpuOffset += entry.gpuOffsetNextEntry;
                }
            }

            return (entries, usedDataTypes, isLittleEndian);
        }

        public string SaveCpuData(int entryIndex, string outFolderPath)
        {
            CpuEntry entry = entries[entryIndex];
            if (entry.cpuDataLength > 0)
            {
                string fileName = string.Format("{0}_CPU_{1}", entry.entryNumber, Path.GetFileName(ReplaceInvalidChars(entry.name)));
                BinaryTools.SaveData(cpuFilePath, entry.cpuOffsetData, entry.cpuDataLength, Path.Combine(outFolderPath, fileName));
                return fileName;
            }
            else
            {
                return null;
            }
        }

        public string SaveGpuData(int entryIndex, string outFolderPath)
        {
            CpuEntry entry = entries[entryIndex];
            if (entry.gpuDataLength > 0)
            {
                string fileName = string.Format("{0}_GPU_{1}", entry.entryNumber, Path.GetFileName(ReplaceInvalidChars(entry.name)));
                BinaryTools.SaveData(gpuFilePath, entry.gpuOffsetData, entry.gpuDataLength, Path.Combine(outFolderPath, fileName));
                return fileName;
            }
            else
            {
                return null;
            }
        }

        public void ReplaceCpuData(int entryIndex, string filePath)
        {
            using (FileStream fsIn = File.OpenRead(filePath))
            using (MemoryStream resource = new MemoryStream((int)fsIn.Length))
            using (FileStream fsOut = File.Open(cpuFilePath, FileMode.Open))
            {
                CpuEntry entry = entries[entryIndex];
                BinaryTools.WriteData(fsIn, resource, (int)fsIn.Length);
                if (entry.entryType == EntryType.Resource)
                {
                    resource.Seek(0, SeekOrigin.Begin);
                    ChangeResourceName(resource, entry.name);
                }
                int length = (int)resource.Length;
                if (length + 0x20 > entry.cpuOffsetNextEntry)
                {
                    BinaryTools.ExpandStream(fsOut, entry.cpuOffsetData, length + 0x20 - entry.cpuOffsetNextEntry);
                    entry.cpuOffsetNextEntry = length + 0x20;
                }
                entry.cpuDataLength = length;
                fsOut.Seek(entry.cpuOffsetHeader, SeekOrigin.Begin);
                WriteHeader(fsOut, entry);
                BinaryTools.WriteData(resource, fsOut, length, outOffset: entry.cpuOffsetData);
            }
            (entries, usedDataTypes, isLittleEndian) = Parse(cpuFilePath);
        }

        public void ReplaceGpuData(int entryIndex, string filePath)
        {
            using (FileStream fsIn = File.OpenRead(filePath))
            using (MemoryStream resource = new MemoryStream((int)fsIn.Length))
            using (FileStream fsOut1 = File.Open(cpuFilePath, FileMode.Open))
            using (FileStream fsOut2 = File.Open(gpuFilePath, FileMode.Open))
            {
                CpuEntry entry = entries[entryIndex];

                int fileLength = (int)fsIn.Length;
                if (fileLength > entry.gpuOffsetNextEntry)
                {
                    BinaryTools.ExpandStream(fsOut2, entry.gpuOffsetData, fileLength - entry.gpuOffsetNextEntry);
                    entry.gpuOffsetNextEntry = fileLength;
                }
                entry.gpuDataLength = fileLength;
                fsOut1.Seek(entry.cpuOffsetHeader, SeekOrigin.Begin);
                WriteHeader(fsOut1, entry);
                BinaryTools.WriteData(fsIn, fsOut2, fileLength, outOffset: entry.gpuOffsetData);
            }
            (entries, usedDataTypes, isLittleEndian) = Parse(cpuFilePath);
        }

        public string ReplaceInvalidChars(string filename)
        {
            return string.Join(" ", filename.Split(invalidCharacters));
        }

        public void WriteHeader(FileStream fs, CpuEntry entry)
        {
            using (EndiannessAwareBinaryWriter b = new EndiannessAwareBinaryWriter(fs, isLittleEndian, System.Text.Encoding.ASCII, true))
            {
                b.Write((int)entry.dataType);
                b.Write(entry.toolVersion);
                b.Write(entry.cpuOffsetNextEntry);
                b.Write(entry.cpuDataLength);
                b.Write(entry.gpuOffsetNextEntry);
                b.Write(entry.gpuDataLength);
                b.Write(entry.unknown);
                b.Write(entry.entryType == EntryType.Resource);
            }
        }

        public void ChangeResourceName(Stream resource, string newName)
        {
            int startOffset = (int)resource.Position;
            resource.Seek(startOffset + 4, SeekOrigin.Begin);
            int nameOffset;
            using (EndiannessAwareBinaryReader b = new EndiannessAwareBinaryReader(resource, isLittleEndian, System.Text.Encoding.ASCII, true))
            {
                nameOffset = b.ReadInt32();
            }
            resource.Seek(startOffset + nameOffset, SeekOrigin.Begin);
            string oldName = BinaryTools.ReadString(resource);
            if (newName.Length > oldName.Length)
            {
                nameOffset = (int)resource.Length;
                resource.SetLength(resource.Length + newName.Length + 1);
            }

            resource.Seek(startOffset, SeekOrigin.Begin);
            using (EndiannessAwareBinaryWriter b = new EndiannessAwareBinaryWriter(resource, isLittleEndian, System.Text.Encoding.ASCII, true))
            {
                b.Write(NameChecksum(newName));
                b.Write(nameOffset);
            }
            resource.Seek(startOffset + nameOffset, SeekOrigin.Begin);
            BinaryTools.WriteString(resource, newName);
        }

        public static uint NameChecksum(string name)
        {
            uint x = 0x811C9DC5;
            foreach (char c in name)
            {
                x *= 0x1000193;
                x ^= c;
            }

            return x;
        }
    }
}
