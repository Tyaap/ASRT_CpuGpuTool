using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace CpuGpuTool
{
    public class CpuFile
    {
        public string cpuFilePath;
        public string gpuFilePath;
        public List<CpuEntry> entriesList;
        public Dictionary<uint, CpuEntry> entriesDictionary;
        public HashSet<DataType> usedDataTypes;
        public static readonly char[] invalidCharacters = { ':', '*', '?', '<', '>', '|' };
        public bool isLittleEndian;

        public CpuFile()
        {
            cpuFilePath = "";
            gpuFilePath = "";
            entriesList = new List<CpuEntry>();
            entriesDictionary = new Dictionary<uint, CpuEntry>();
        }
        public CpuFile(string filePath)
        {
            cpuFilePath = filePath;
            gpuFilePath = filePath.Replace(".cpu.", ".gpu.");
            (entriesList, entriesDictionary, usedDataTypes, isLittleEndian) = Parse(filePath);
        }

        public CpuEntry this[int index]
        {
            get => entriesList[index];
        }

        public CpuEntry this[uint id]
        {
            get => entriesDictionary[id];
        }

        public int Count
        {
            get => entriesList.Count;
        }

        public static (List<CpuEntry>, Dictionary<uint, CpuEntry>, HashSet<DataType>, bool) Parse(string filePath)
        {
            List<CpuEntry> entriesList = new List<CpuEntry>();
            Dictionary<uint, CpuEntry> entriesDictionary= new Dictionary<uint, CpuEntry>();
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
                        entryNumber = entriesList.Count + 1,
                        dataType = (DataType)b.ReadInt32(),
                        toolVersion = b.ReadInt32(),
                        cpuOffsetDataHeader = cpuOffset,
                        cpuOffsetData = cpuOffset + 0x20,
                        cpuRelativeOffsetNextEntry = b.ReadInt32(),
                        cpuDataLength = b.ReadInt32(),
                        gpuOffsetData = gpuOffset,
                        gpuRelativeOffsetNextEntry = b.ReadInt32(),
                        gpuDataLength = b.ReadInt32(),
                        unknown = b.ReadInt32(),
                        entryType = b.ReadBoolean() ? EntryType.Resource : EntryType.Node,
                        name = "[No name found]",
                        daughterIds = new List<uint>(),
                        instanceIds = new List<uint>()
                    };

                    int tempOffset;
                    if (entry.entryType == EntryType.Resource)
                    {
                        entry.id = b.ReadUInt32();
                        tempOffset = b.ReadInt32();
                        if (tempOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tempOffset, SeekOrigin.Begin);
                            entry.name = BinaryTools.ReadString(b.BaseStream);
                        }
                    }
                    else
                    {
                        b.BaseStream.Seek(entry.cpuOffsetData + 0x14, SeekOrigin.Begin);
                        entry.id = b.ReadUInt32();

                        b.BaseStream.Seek(entry.cpuOffsetData + 0x1C, SeekOrigin.Begin);
                        tempOffset = b.ReadInt32();
                        if (tempOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tempOffset, SeekOrigin.Begin);
                            entry.name = BinaryTools.ReadString(b.BaseStream);
                        }

                        b.BaseStream.Seek(entry.cpuOffsetData + 0x24, SeekOrigin.Begin);
                        tempOffset = b.ReadInt32();
                        if (tempOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tempOffset, SeekOrigin.Begin);
                            entry.shortName = BinaryTools.ReadString(b.BaseStream);
                        }

                        b.BaseStream.Seek(entry.cpuOffsetData + 0x40, SeekOrigin.Begin);
                        tempOffset = b.ReadInt32();
                        if (tempOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tempOffset, SeekOrigin.Begin);
                            entry.parentId = b.ReadUInt32();
                        }

                        b.BaseStream.Seek(entry.cpuOffsetData + 0x68, SeekOrigin.Begin);
                        tempOffset = b.ReadInt32();
                        if (tempOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tempOffset, SeekOrigin.Begin);
                            entry.definitionId = b.ReadUInt32();
                        }
                    }

                    cpuOffset += entry.cpuRelativeOffsetNextEntry;

                    b.BaseStream.Seek(cpuOffset + 8, SeekOrigin.Begin);
                    entry.cpuOffsetPointersHeader = cpuOffset;
                    entry.cpuOffsetPointers = cpuOffset + 0x20;
                    entry.cpuRelativeOffsetNextEntry += b.ReadInt32();
                    entry.cpuPointersLength = b.ReadInt32();

                    entriesList.Add(entry);
                    entriesDictionary[entry.id] = entry;
                    usedDataTypes.Add(entry.dataType);

                    cpuOffset = entry.cpuOffsetDataHeader + entry.cpuRelativeOffsetNextEntry;
                    gpuOffset = entry.gpuOffsetData + entry.gpuRelativeOffsetNextEntry;
                }
            }

            int length = entriesList.Count;
            for (int i = 0; i < length; i++)
            {
                CpuEntry entry = entriesList[i];
                if (entry.parentId != 0 && entriesDictionary.TryGetValue(entry.parentId, out CpuEntry parent))
                {
                    parent.daughterIds.Add(entry.id);
                }
                if (entry.definitionId != 0 && entriesDictionary.TryGetValue(entry.definitionId, out CpuEntry definition))
                {
                    definition.instanceIds.Add(entry.id);
                }
            }

            return (entriesList, entriesDictionary, usedDataTypes, isLittleEndian);
        }

        public void Reload()
        {
            if (!string.IsNullOrEmpty(cpuFilePath))
            {
                (entriesList, entriesDictionary, usedDataTypes, isLittleEndian) = Parse(cpuFilePath);
            }
        }

        public void GetCpuData(int entryIndex, Stream sOut, int outOffset = 0)
        {
            CpuEntry entry = entriesList[entryIndex];
            BinaryTools.WriteData(cpuFilePath, sOut, entry.cpuRelativeOffsetNextEntry, entry.cpuOffsetDataHeader, outOffset);
        }

        public void GetGpuData(int entryIndex, Stream sOut, int outOffset = 0)
        {
            CpuEntry entry = entriesList[entryIndex];
            BinaryTools.WriteData(gpuFilePath, sOut, entry.gpuRelativeOffsetNextEntry, entry.gpuOffsetData, outOffset);
        }

        public void InsertCpuData(int entryIndex, Stream sIn, int length = -1, int inOffset = 0)
        {
            if (length == -1)
            {
                length = (int)sIn.Length;
            }
            using (FileStream fsOut = File.Open(cpuFilePath, FileMode.Open))
            {
                CpuEntry entry = entriesList[entryIndex];
                BinaryTools.InsertData(sIn, fsOut, length, inOffset, entry.cpuOffsetDataHeader + entry.cpuRelativeOffsetNextEntry);
            }
        }

        public void InsertGpuData(int entryIndex, Stream sIn, int length = -1, int inOffset = 0)
        {
            if (length == -1)
            {
                length = (int)sIn.Length;
            }
            using (FileStream fsOut = File.Open(gpuFilePath, FileMode.Open))
            {
                CpuEntry entry = entriesList[entryIndex];
                BinaryTools.InsertData(sIn, fsOut, length, inOffset, entry.gpuOffsetData + entry.gpuRelativeOffsetNextEntry);
            }
        }

        public void DeleteCpuData(int entryIndex)
        {
            CpuEntry entry = entriesList[entryIndex];
            using (FileStream fs = File.Open(cpuFilePath, FileMode.Open))
            {
                BinaryTools.ShrinkStream(fs, entry.cpuOffsetDataHeader, entry.cpuRelativeOffsetNextEntry);
            }
        }

        public void DeleteGpuData(int entryIndex)
        {
            CpuEntry entry = entriesList[entryIndex];
            using (FileStream fs = File.Open(gpuFilePath, FileMode.Open))
            {
                BinaryTools.ShrinkStream(fs, entry.gpuOffsetData, entry.gpuRelativeOffsetNextEntry);
            }
        }

        public string SaveCpuData(int entryIndex, string outFolderPath)
        {
            CpuEntry entry = entriesList[entryIndex];
            string start = entry.entryNumber + "_CPU_";
            string end = Path.GetFileName(ReplaceInvalidChars(entry.name));
            string fileName = start + end.Substring(Math.Max(0, start.Length + end.Length - 255));

            BinaryTools.WriteData(cpuFilePath, Path.Combine(outFolderPath, fileName), entry.cpuRelativeOffsetNextEntry, entry.cpuOffsetDataHeader);
            return fileName;
        }

        public string SaveGpuData(int entryIndex, string outFolderPath)
        {
            CpuEntry entry = entriesList[entryIndex];
            if (entry.gpuDataLength > 0)
            {
                string fileName = string.Format("{0}_GPU_{1}", entry.entryNumber, Path.GetFileName(ReplaceInvalidChars(entry.name)));
                BinaryTools.WriteData(gpuFilePath, Path.Combine(outFolderPath, fileName), entry.gpuDataLength, entry.gpuOffsetData);
                return fileName;
            }
            else
            {
                return null;
            }
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
                b.Write(entry.cpuRelativeOffsetNextEntry);
                b.Write(entry.cpuDataLength);
                b.Write(entry.gpuRelativeOffsetNextEntry);
                b.Write(entry.gpuDataLength);
                b.Write(entry.unknown);
                b.Write(entry.entryType == EntryType.Resource);
            }
        }

        public void ChangeEntryName(int entryIndex, string newName)
        {
            CpuEntry entry = entriesList[entryIndex];
            using (FileStream fs = File.Open(cpuFilePath, FileMode.Open, FileAccess.ReadWrite))
            using (EndiannessAwareBinaryReader br = new EndiannessAwareBinaryReader(fs))
            using (EndiannessAwareBinaryWriter bw = new EndiannessAwareBinaryWriter(fs))
            {
                int nameOffset;
                if (entry.entryType == EntryType.Resource)
                {
                    fs.Seek(entry.cpuOffsetData + 4, SeekOrigin.Begin);
                    nameOffset = entry.cpuOffsetData + br.ReadInt32();
                    if (entry.name.Length < newName.Length || nameOffset == 0)
                    {
                        // change name offset to end of cpu data
                        fs.Seek(entry.cpuOffsetData + 4, SeekOrigin.Begin);
                        bw.Write(entry.cpuDataLength);
                        nameOffset = entry.cpuOffsetData + entry.cpuDataLength;
                    }
                }
                else
                {
                    fs.Seek(entry.cpuOffsetData + 0x1C, SeekOrigin.Begin);
                    nameOffset = entry.cpuOffsetData + br.ReadInt32();
                    if (entry.name.Length < newName.Length || nameOffset == 0)
                    {
                        // change name offset to end of cpu data
                        fs.Seek(entry.cpuOffsetData + 0x1C, SeekOrigin.Begin);
                        bw.Write(entry.cpuDataLength);
                        nameOffset = entry.cpuOffsetData + entry.cpuDataLength;
                    }
                }

                // expand the file if there is not enough space before the pointers header
                if (nameOffset + newName.Length > entry.cpuOffsetPointersHeader)
                {
                    BinaryTools.ExpandStream(fs, nameOffset, nameOffset + newName.Length - entry.cpuOffsetPointersHeader);
                    // Update offset to the pointers header
                    fs.Seek(entry.cpuOffsetDataHeader + 0x8, SeekOrigin.Begin);
                    bw.Write(nameOffset + newName.Length);
                }

                fs.Seek(nameOffset, SeekOrigin.Begin);
                BinaryTools.WriteString(fs, newName);
            }
        }

        public void ChangeEntryID(int entryIndex, uint id)
        {
            CpuEntry entry = entriesList[entryIndex];
            using (EndiannessAwareBinaryWriter bw = new EndiannessAwareBinaryWriter(File.OpenWrite(cpuFilePath)))
            {
                if (entry.entryType == EntryType.Resource)
                {
                    bw.BaseStream.Seek(entry.cpuOffsetData, SeekOrigin.Begin);      
                }
                else
                {
                    bw.BaseStream.Seek(entry.cpuOffsetData + 0x14, SeekOrigin.Begin);
                }
                bw.Write(id);
            }
        }

        public bool ChangeDefinitionID(int entryIndex, uint id)
        {
            CpuEntry entry = entriesList[entryIndex];
            if (entry.entryType == EntryType.Resource)
            {
                return false;
            }
            using (FileStream fs = File.Open(cpuFilePath, FileMode.Open, FileAccess.ReadWrite))
            using (EndiannessAwareBinaryReader br = new EndiannessAwareBinaryReader(fs))
            using (EndiannessAwareBinaryWriter bw = new EndiannessAwareBinaryWriter(fs))
            {     
                fs.Seek(entry.cpuOffsetData + 0x68, SeekOrigin.Begin);
                int idRelOffset = br.ReadInt32();
                if (idRelOffset == 0)
                {
                    return false;
                }
                fs.Seek(entry.cpuOffsetData + idRelOffset, SeekOrigin.Begin);
                bw.Write(id);
            }
            return true;
        }

        public bool ChangeParentID(int entryIndex, uint id)
        {
            CpuEntry entry = entriesList[entryIndex];
            if (entry.entryType == EntryType.Resource)
            {
                return false;
            }
            using (FileStream fs = File.Open(cpuFilePath, FileMode.Open, FileAccess.ReadWrite))
            using (EndiannessAwareBinaryReader br = new EndiannessAwareBinaryReader(fs))
            using (EndiannessAwareBinaryWriter bw = new EndiannessAwareBinaryWriter(fs))
            {
                fs.Seek(entry.cpuOffsetData + 0x40, SeekOrigin.Begin);
                int idRelOffset = br.ReadInt32();
                if (idRelOffset == 0)
                {
                    return false;
                }
                fs.Seek(entry.cpuOffsetData + idRelOffset, SeekOrigin.Begin);
                bw.Write(id);
            }
            return true;
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
