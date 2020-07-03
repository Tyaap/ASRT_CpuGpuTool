using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Schema;

namespace CpuGpuTool
{
    public class CpuFile
    {
        public string cpuFilePath;
        public string gpuFilePath;
        public MemoryStream msCpuFile;
        public MemoryStream msGpuFile;
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
            msCpuFile = new MemoryStream();
            msGpuFile = new MemoryStream();
            entriesList = new List<CpuEntry>();
            nodeDictionary = new Dictionary<uint, Node>();
            resourceDictionary = new Dictionary<uint, Resource>();
        }
        public CpuFile(string filePath)
        {
            cpuFilePath = filePath;
            gpuFilePath = filePath.Replace(".cpu.", ".gpu.");
            msCpuFile = new MemoryStream();
            msGpuFile = new MemoryStream();
            BinaryTools.WriteData(cpuFilePath, msCpuFile);
            BinaryTools.WriteData(gpuFilePath, msGpuFile);
            Parse(msCpuFile, out entriesList, out resourceDictionary, out nodeDictionary, out usedDataTypes, out isLittleEndian);
        }

        public CpuEntry this[int index]
        {
            get => entriesList[index];
        }

        public int Count
        {
            get => entriesList.Count;
        }

        public static void Parse(
            Stream sCpuFile, 
            out List<CpuEntry> entriesList, 
            out Dictionary<uint, Resource> resourceDictionary,
            out Dictionary<uint, Node> nodeDictionary,
            out HashSet<DataType> usedDataTypes, 
            out bool isLittleEndian)
        {
            entriesList = new List<CpuEntry>();
            resourceDictionary = new Dictionary<uint, Resource>();
            nodeDictionary = new Dictionary<uint, Node>();
            usedDataTypes = new HashSet<DataType>();

            if (sCpuFile.Length == 0)
            {
                isLittleEndian = true;
                return;
            }
            sCpuFile.Seek(0, SeekOrigin.Begin);
            using (EndiannessAwareBinaryReader b = new EndiannessAwareBinaryReader(sCpuFile, Encoding.ASCII, true))
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
                                uint id1 = b.ReadUInt32();
                                resource.references[id1] = new Resource() { id = id1, dataType = DataType.SlShader };
                                // textures
                                for (int j = 0; j < 9; j++)
                                {
                                    if (j == 1)
                                    {
                                        continue;
                                    }
                                    b.BaseStream.Seek(resource.cpuOffsetData + 0x24 + j * 4, SeekOrigin.Begin);
                                    int offset1 = b.ReadInt32();
                                    if (offset1 == 0)
                                    {
                                        continue;
                                    }
                                    b.BaseStream.Seek(resource.cpuOffsetData + offset1 + 0xC, SeekOrigin.Begin);
                                    id1 = b.ReadUInt32();
                                    resource.references[id1] = new Resource() { id = id1, dataType = DataType.SlTexture };
                                }
                                // cbdesc
                                for (int j = 0; j < 9; j++)
                                {
                                    b.BaseStream.Seek(resource.cpuOffsetData + 0x50 + j * 4, SeekOrigin.Begin);
                                    int offset1 = b.ReadInt32();
                                    if (offset1 == 0)
                                    {
                                        continue;
                                    }
                                    b.BaseStream.Seek(resource.cpuOffsetData + offset1 + 0xC, SeekOrigin.Begin);
                                    id1 = b.ReadUInt32();
                                    resource.references[id1] = new Resource() { id = id1, dataType = DataType.SlConstantBufferDesc };
                                }
                                break;
                            case DataType.SlAnim:
                                // SlSkeleton
                                b.BaseStream.Seek(resource.cpuOffsetData + 0x10, SeekOrigin.Begin);
                                uint id2 = b.ReadUInt32();
                                resource.references[id2] = new Resource() { id = id2, dataType = DataType.SlSkeleton };
                                break;
                            case DataType.SlModel:
                                // SlSkeleton
                                b.BaseStream.Seek(resource.cpuOffsetData + 0xC, SeekOrigin.Begin);
                                int offset2 = b.ReadInt32();
                                b.BaseStream.Seek(resource.cpuOffsetData + offset2 + 0xC, SeekOrigin.Begin);
                                uint id3 = b.ReadUInt32();
                                if (id3 != 0)
                                {
                                    resource.references[id3] = new Resource() { id = id3, dataType = DataType.SlSkeleton };
                                }
                                // SlMaterial
                                b.BaseStream.Seek(resource.cpuOffsetData + 0x40, SeekOrigin.Begin);
                                int materialCount = b.ReadInt32();
                                b.BaseStream.Seek(resource.cpuOffsetData + 0x60, SeekOrigin.Begin);
                                for (int i = 0; i < materialCount; i++)
                                {
                                    id3 = b.ReadUInt32();
                                    resource.references[id3] = new Resource() { id = id3, dataType = DataType.SlMaterial2 };
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
                        switch (node.dataType)
                        {
                            case DataType.Water13DefNode:
                                // Water13Simulation
                                b.BaseStream.Seek(entry.cpuOffsetData + 0xD0, SeekOrigin.Begin);
                                uint id1 = b.ReadUInt32();
                                node.references[id1] = new Resource() { id = id1, dataType = DataType.Water13Simulation };
                                // Water13Renderable
                                id1 = b.ReadUInt32();
                                node.references[id1] = new Resource() { id = id1, dataType = DataType.Water13Renderable };
                                break;
                            case DataType.Water13InstanceNode:
                                // Water13SurfaceWavesDefNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x1D0, SeekOrigin.Begin);
                                uint id2 = b.ReadUInt32();
                                node.references[id2] = new Node() { id = id2, dataType = DataType.Water13SurfaceWavesDefNode };
                                // WaterShader4DefinitionNode
                                id2 = b.ReadUInt32();
                                node.references[id2] = new Node() { id = id2, dataType = DataType.WaterShader4DefinitionNode };
                                break;
                            case DataType.SeDefinitionParticleEmitterNode:
                                // SeDefinitionParticleStyleNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x198, SeekOrigin.Begin);
                                uint id3 = b.ReadUInt32();
                                node.references[id3] = new Node() { id = id3, dataType = DataType.SeDefinitionParticleStyleNode };
                                break;
                            case DataType.SeDefinitionParticleStyleNode:
                                // SeDefinitionTextureNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x1D0, SeekOrigin.Begin);
                                uint id4 = b.ReadUInt32();
                                node.references[id4] = new Node() { id = id4, dataType = DataType.SeDefinitionTextureNode };
                                break;
                            case DataType.CameoObjectInstanceNode:
                                // SeInstanceSplineNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x1A4, SeekOrigin.Begin);
                                uint id5 = b.ReadUInt32();
                                if (id5 != 0)
                                {
                                    node.references[id5] = new Node() { id = id5, dataType = DataType.SeInstanceSplineNode };
                                }
                                break;
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
                    node.references[resource.id] = resource;
                    resource.referees[node.id] = node;
                }
            }
          
            foreach (CpuEntry entry in entriesList)
            {
                List<uint> ids = new List<uint>(entry.references.Keys);
                foreach (uint id in ids)
                {
                    if(resourceDictionary.TryGetValue(id, out Resource foundResource))
                    {
                        entry.references[id] = foundResource;
                        foundResource.referees[entry.id] = entry;
                    }
                    else if (nodeDictionary.TryGetValue(id, out Node foundNode))
                    {
                        if (foundNode.parent == null || foundNode.parent.id != entry.id)
                        {
                            entry.references[id] = foundNode;
                            foundNode.referees[entry.id] = entry;
                        }
                        else
                        {
                            // If a reference is already a node parent, remove it from references
                            entry.references.Remove(id);
                        }
                    }
                }
            }
        }

        public void Reload()
        {
            Parse(msCpuFile, out entriesList, out resourceDictionary, out nodeDictionary, out usedDataTypes, out isLittleEndian);
        }

        public void GetCpuData(int entryIndex, Stream sOut, int outOffset = 0)
        {
            CpuEntry entry = entriesList[entryIndex];
            BinaryTools.WriteData(msCpuFile, sOut, entry.cpuRelativeOffsetNextEntry, entry.cpuOffsetDataHeader, outOffset);
        }

        public void GetGpuData(int entryIndex, Stream sOut, int outOffset = 0)
        {
            CpuEntry entry = entriesList[entryIndex];
            BinaryTools.WriteData(msGpuFile, sOut, entry.gpuRelativeOffsetNextEntry, entry.gpuOffsetData, outOffset);
        }

        public void InsertCpuData(int entryIndex, string inFilePath, int length = -1, int inOffset = 0)
        {
            using (FileStream fsIn = File.OpenRead(inFilePath))
            {
                CpuEntry entry = entriesList[entryIndex];
                BinaryTools.InsertData(fsIn, msCpuFile, length, inOffset, entry.cpuOffsetDataHeader + entry.cpuRelativeOffsetNextEntry);
            }
        }

        public void InsertCpuData(int entryIndex, Stream sIn, int length = -1, int inOffset = 0)
        {
            CpuEntry entry = entriesList[entryIndex];
            BinaryTools.InsertData(sIn, msCpuFile, length, inOffset, entry.cpuOffsetDataHeader + entry.cpuRelativeOffsetNextEntry);
        }

        public void InsertGpuData(int entryIndex, string inFilePath, int length = -1, int inOffset = 0)
        {
            using (FileStream fsIn = File.OpenRead(inFilePath))
            {
                CpuEntry entry = entriesList[entryIndex];
                BinaryTools.InsertData(fsIn, msGpuFile, length, inOffset, entry.gpuOffsetData + entry.gpuRelativeOffsetNextEntry);
            }
        }
        public void InsertGpuData(int entryIndex, Stream sIn, int length = -1, int inOffset = 0)
        {
            CpuEntry entry = entriesList[entryIndex];
            BinaryTools.InsertData(sIn, msGpuFile, length, inOffset, entry.gpuOffsetData + entry.gpuRelativeOffsetNextEntry);
        }

        public void DeleteCpuData(int entryIndex)
        {
            CpuEntry entry = entriesList[entryIndex];
            BinaryTools.ShrinkStream(msCpuFile, entry.cpuOffsetDataHeader, entry.cpuRelativeOffsetNextEntry);
        }

        public void DeleteGpuData(int entryIndex)
        {
            CpuEntry entry = entriesList[entryIndex];
            BinaryTools.ShrinkStream(msGpuFile, entry.gpuOffsetData, entry.gpuRelativeOffsetNextEntry);
        }

        public void ReplaceCpuData(int entryIndex, string inFilePath, int length = -1, int inOffset = 0)
        {
            using (FileStream fsIn = File.OpenRead(inFilePath))
            {
                ReplaceCpuData(entryIndex, fsIn, length, inOffset);
            }
        }

        public void ReplaceCpuData(int entryIndex, Stream sIn, int length = -1, int inOffset = 0)
        {
            DeleteCpuData(entryIndex);
            InsertCpuData(Math.Max(0, entryIndex - 1), sIn, length, inOffset);

            // update gpuRelativeOffsetNextEntry and gpuFataLength in the cpu data header
            CpuEntry entry = entriesList[entryIndex];
            using (EndiannessAwareBinaryWriter b = new EndiannessAwareBinaryWriter(msCpuFile, isLittleEndian, Encoding.ASCII, true))
            {
                msCpuFile.Seek(entry.cpuOffsetDataHeader + 0x10, SeekOrigin.Begin);
                b.Write(entry.gpuRelativeOffsetNextEntry);
                b.Write(entry.gpuDataLength);
            }
        }

        public void ReplaceGpuData(int entryIndex, string inFilePath, int length = -1, int inOffset = 0)
        {
            using (FileStream fsIn = File.OpenRead(inFilePath))
            {
                ReplaceGpuData(entryIndex, fsIn, length, inOffset);
            }
        }

        public void ReplaceGpuData(int entryIndex, Stream sIn, int length = -1, int inOffset = 0)
        {
            if (length == -1)
            {
                length = (int)sIn.Length - (inOffset != -1 ? inOffset : 0);
            }
            DeleteGpuData(entryIndex);
            InsertGpuData(Math.Max(0, entryIndex - 1), sIn, length, inOffset);

            // update gpuRelativeOffsetNextEntry and gpuFataLength in the cpu data header
            CpuEntry entry = entriesList[entryIndex];
            using (EndiannessAwareBinaryWriter b = new EndiannessAwareBinaryWriter(msCpuFile, isLittleEndian, Encoding.ASCII, true))
            {
                msCpuFile.Seek(entry.cpuOffsetDataHeader + 0x10, SeekOrigin.Begin);
                b.Write(length);
                b.Write(length);
            }
        }

        public string SaveCpuData(int entryIndex, string outFolderPath)
        {
            CpuEntry entry = entriesList[entryIndex];
            string start = entry.entryNumber + "_CPU_";
            string end = Path.GetFileName(ReplaceInvalidChars(entry.name));
            string fileName = start + end.Substring(Math.Max(0, start.Length + end.Length - 255));

            BinaryTools.WriteData(msCpuFile, Path.Combine(outFolderPath, fileName), entry.cpuRelativeOffsetNextEntry, entry.cpuOffsetDataHeader);
            return fileName;
        }

        public string SaveGpuData(int entryIndex, string outFolderPath)
        {
            CpuEntry entry = entriesList[entryIndex];
            if (entry.gpuDataLength > 0)
            {
                string fileName = string.Format("{0}_GPU_{1}", entry.entryNumber, Path.GetFileName(ReplaceInvalidChars(entry.name)));
                BinaryTools.WriteData(msGpuFile, Path.Combine(outFolderPath, fileName), entry.gpuDataLength, entry.gpuOffsetData);
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

        public void WriteHeader(Stream s, CpuEntry entry)
        {
            using (EndiannessAwareBinaryWriter b = new EndiannessAwareBinaryWriter(s, isLittleEndian, Encoding.ASCII, true))
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
            using (EndiannessAwareBinaryReader br = new EndiannessAwareBinaryReader(msCpuFile, isLittleEndian, Encoding.ASCII, true))
            using (EndiannessAwareBinaryWriter bw = new EndiannessAwareBinaryWriter(msCpuFile, isLittleEndian, Encoding.ASCII, true))
            {
                int nameOffset;
                if (entry as Resource != null)
                {
                    msCpuFile.Seek(entry.cpuOffsetData + 4, SeekOrigin.Begin);
                    nameOffset = entry.cpuOffsetData + br.ReadInt32();
                    if (entry.name.Length < newName.Length || nameOffset == 0)
                    {
                        // change name offset to end of cpu data
                        msCpuFile.Seek(entry.cpuOffsetData + 4, SeekOrigin.Begin);
                        bw.Write(entry.cpuDataLength);
                        nameOffset = entry.cpuOffsetData + entry.cpuDataLength;
                    }
                }
                else
                {
                    msCpuFile.Seek(entry.cpuOffsetData + 0x1C, SeekOrigin.Begin);
                    nameOffset = entry.cpuOffsetData + br.ReadInt32();
                    if (entry.name.Length < newName.Length || nameOffset == 0)
                    {
                        // change name offset to end of cpu data
                        msCpuFile.Seek(entry.cpuOffsetData + 0x1C, SeekOrigin.Begin);
                        bw.Write(entry.cpuDataLength);
                        nameOffset = entry.cpuOffsetData + entry.cpuDataLength;
                    }
                }

                // expand the file if there is not enough space before the pointers header
                if (nameOffset + newName.Length > entry.cpuOffsetPointersHeader)
                {
                    BinaryTools.ExpandStream(msCpuFile, nameOffset, nameOffset + newName.Length - entry.cpuOffsetPointersHeader);
                    // Update offset to the pointers header
                    msCpuFile.Seek(entry.cpuOffsetDataHeader + 0x8, SeekOrigin.Begin);
                    bw.Write(nameOffset + newName.Length);
                }

                msCpuFile.Seek(nameOffset, SeekOrigin.Begin);
                BinaryTools.WriteString(msCpuFile, newName);
            }
        }

        public void ChangeEntryID(int entryIndex, uint id)
        {
            CpuEntry entry = entriesList[entryIndex];
            using (EndiannessAwareBinaryWriter bw = new EndiannessAwareBinaryWriter(msCpuFile, isLittleEndian, Encoding.Default, true))
            {
                if (entry as Resource != null)
                {
                    msCpuFile.Seek(entry.cpuOffsetData, SeekOrigin.Begin);      
                }
                else
                {
                    msCpuFile.Seek(entry.cpuOffsetData + 0x14, SeekOrigin.Begin);
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
            using (EndiannessAwareBinaryReader br = new EndiannessAwareBinaryReader(msCpuFile, isLittleEndian, Encoding.ASCII, true))
            using (EndiannessAwareBinaryWriter bw = new EndiannessAwareBinaryWriter(msCpuFile, isLittleEndian, Encoding.ASCII, true))
            {     
                msCpuFile.Seek(entry.cpuOffsetData + 0x68, SeekOrigin.Begin);
                int idRelOffset = br.ReadInt32();
                if (idRelOffset == 0)
                {
                    return false;
                }
                msCpuFile.Seek(entry.cpuOffsetData + idRelOffset, SeekOrigin.Begin);
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
            using (EndiannessAwareBinaryReader br = new EndiannessAwareBinaryReader(msCpuFile, isLittleEndian, Encoding.ASCII, true))
            using (EndiannessAwareBinaryWriter bw = new EndiannessAwareBinaryWriter(msCpuFile, isLittleEndian, Encoding.ASCII, true))
            {
                msCpuFile.Seek(entry.cpuOffsetData + 0x40, SeekOrigin.Begin);
                int idRelOffset = br.ReadInt32();
                if (idRelOffset == 0)
                {
                    return false;
                }
                msCpuFile.Seek(entry.cpuOffsetData + idRelOffset, SeekOrigin.Begin);
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

        public void Save()
        {
            if (string.IsNullOrEmpty(cpuFilePath))
            {
                return;
            }
            File.Delete(cpuFilePath);
            File.Delete(gpuFilePath);
            BinaryTools.WriteData(msCpuFile, cpuFilePath);
            BinaryTools.WriteData(msGpuFile, gpuFilePath);
        }
    }
}
