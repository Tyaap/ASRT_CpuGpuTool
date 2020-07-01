using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CpuGpuTool
{
    public class CpuFile
    {
        public string cpuFilePath;
        public string gpuFilePath;
        public List<CpuEntry> entriesList;
        public Dictionary<uint, Node> nodeDictionary;
        public Dictionary<uint, Resource> resourceDictionary;
        public HashSet<DataType> usedDataTypes;
        public static readonly char[] invalidCharacters = { ':', '*', '?', '<', '>', '|' };
        public bool isLittleEndian;

        public CpuFile()
        {
            cpuFilePath = "";
            gpuFilePath = "";
            entriesList = new List<CpuEntry>();
            nodeDictionary = new Dictionary<uint, Node>();
            resourceDictionary = new Dictionary<uint, Resource>();
        }
        public CpuFile(string filePath)
        {
            cpuFilePath = filePath;
            gpuFilePath = filePath.Replace(".cpu.", ".gpu.");
            (entriesList, resourceDictionary, nodeDictionary, usedDataTypes, isLittleEndian) = Parse(filePath);
        }

        public CpuEntry this[int index]
        {
            get => entriesList[index];
        }

        public int Count
        {
            get => entriesList.Count;
        }

        public static (List<CpuEntry>, Dictionary<uint, Resource>, Dictionary<uint, Node>, HashSet<DataType>, bool) Parse(string filePath)
        {
            List<CpuEntry> entriesList = new List<CpuEntry>();
            Dictionary<uint, Node> nodeDictionary= new Dictionary<uint, Node>();
            Dictionary<uint, Resource> resourceDictionary = new Dictionary<uint, Resource>();
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
                        name = "[No name found]"
                    };

                    int tmpOffset;
                    if (b.ReadBoolean()) // Entry type
                    {
                        Resource resource = new Resource(entry);
                        entry = resource;

                        resource.id = b.ReadUInt32();
                        resourceDictionary[resource.id] = resource;

                        if (resource.dataType != DataType.SlResourceCollision)
                        {
                            tmpOffset = b.ReadInt32();
                        }
                        else
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + 0x20, SeekOrigin.Begin);
                            tmpOffset = 0x30 + b.ReadInt32() * 0x14;
                        }         
                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            resource.name = BinaryTools.ReadString(b.BaseStream);
                        }
                        switch (resource.dataType)
                        {
                            case DataType.SlMaterial2:
                                b.BaseStream.Seek(entry.cpuOffsetData + 0xC, SeekOrigin.Begin);
                                // shader
                                uint id = b.ReadUInt32();
                                resource.referencedResources[id] = new Resource() { id = id, dataType = DataType.SlShader };
                                // textures
                                for (int j = 0; j < 9; j++)
                                {
                                    if (j == 1)
                                    {
                                        continue;
                                    }
                                    b.BaseStream.Seek(resource.cpuOffsetData + 0x24 + j * 4, SeekOrigin.Begin);
                                    int offset = b.ReadInt32();
                                    if (offset == 0)
                                    {
                                        continue;
                                    }
                                    b.BaseStream.Seek(resource.cpuOffsetData + offset + 0xC, SeekOrigin.Begin);
                                    id = b.ReadUInt32();
                                    resource.referencedResources[id] = new Resource() { id = id, dataType = DataType.SlTexture };
                                }
                                // cbdesc
                                for (int j = 0; j < 9; j++)
                                {
                                    b.BaseStream.Seek(resource.cpuOffsetData + 0x50 + j * 4, SeekOrigin.Begin);
                                    int offset = b.ReadInt32();
                                    if (offset == 0)
                                    {
                                        continue;
                                    }
                                    b.BaseStream.Seek(resource.cpuOffsetData + offset + 0xC, SeekOrigin.Begin);
                                    id = b.ReadUInt32();
                                    resource.referencedResources[id] = new Resource() { id = id, dataType = DataType.SlConstantBufferDesc };
                                }
                                break;
                        }
                    }
                    else
                    {
                        Node node = new Node(entry);
                        entry = node;

                        b.BaseStream.Seek(entry.cpuOffsetData + 0x14, SeekOrigin.Begin);
                        node.id = b.ReadUInt32();
                        nodeDictionary[node.id] = node;

                        b.BaseStream.Seek(entry.cpuOffsetData + 0x1C, SeekOrigin.Begin);
                        tmpOffset = b.ReadInt32();
                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            node.name = BinaryTools.ReadString(b.BaseStream);
                        }
                        b.BaseStream.Seek(entry.cpuOffsetData + 0x24, SeekOrigin.Begin);
                        tmpOffset = b.ReadInt32();
                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            node.shortName = BinaryTools.ReadString(b.BaseStream);
                        }
                        b.BaseStream.Seek(entry.cpuOffsetData + 0x40, SeekOrigin.Begin);
                        tmpOffset = b.ReadInt32();
                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            node.parent = new Node() { id = b.ReadUInt32() };
                        }
                        b.BaseStream.Seek(entry.cpuOffsetData + 0x68, SeekOrigin.Begin);
                        tmpOffset = b.ReadInt32();
                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            node.definition = new Node() { id = b.ReadUInt32() };
                        }
                    }

                    cpuOffset += entry.cpuRelativeOffsetNextEntry;
                    b.BaseStream.Seek(cpuOffset + 8, SeekOrigin.Begin);
                    entry.cpuOffsetPointersHeader = cpuOffset;
                    entry.cpuOffsetPointers = cpuOffset + 0x20;
                    entry.cpuRelativeOffsetNextEntry += b.ReadInt32();
                    entry.cpuPointersLength = b.ReadInt32();

                    entriesList.Add(entry);
                    usedDataTypes.Add(entry.dataType);

                    cpuOffset = entry.cpuOffsetDataHeader + entry.cpuRelativeOffsetNextEntry;
                    gpuOffset = entry.gpuOffsetData + entry.gpuRelativeOffsetNextEntry;
                }
            }
            foreach (var pair in nodeDictionary)
            {
                Node node = pair.Value;
                if (node.parent != null && nodeDictionary.TryGetValue(node.parent.id, out Node parent))
                {
                    node.parent = parent;
                    parent.daughters[node.id] = node;    
                }
                if (node.definition != null && nodeDictionary.TryGetValue(node.definition.id, out Node definition))
                {
                    node.definition = definition;
                    definition.instances[node.id] = node;
                }
                if (resourceDictionary.TryGetValue(node.id, out Resource resource))
                {
                    node.partener = resource;
                    resource.partener = node;
                }
            }

            
            foreach (CpuEntry entry in entriesList)
            {
                List<uint> ids = new List<uint>(entry.referencedResources.Keys);
                foreach (uint id in ids)
                {
                    if(resourceDictionary.TryGetValue(id, out Resource foundResource))
                    {
                        entry.referencedResources[id] = foundResource;
                        foundResource.referees[entry.id] = entry;
                    }
                }
            }

            return (entriesList, resourceDictionary, nodeDictionary, usedDataTypes, isLittleEndian);
        }

        public void Reload()
        {
            if (!string.IsNullOrEmpty(cpuFilePath))
            {
                (entriesList, resourceDictionary, nodeDictionary, usedDataTypes, isLittleEndian) = Parse(cpuFilePath);
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
                b.Write(entry as Resource != null);
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
                if (entry as Resource != null)
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
                if (entry as Resource != null)
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
            if (entry as Node == null)
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
            if (entry as Node == null)
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
