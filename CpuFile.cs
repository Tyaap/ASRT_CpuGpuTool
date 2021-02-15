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
        public List<Asset> entriesList;
        public Dictionary<uint, List<Node>> nodeDictionary;
        public Dictionary<uint, List<Resource>> resourceDictionary;
        public HashSet<DataType> usedDataTypes;
        public static readonly char[] invalidCharacters = { ':', '*', '?', '<', '>', '|' };
        public bool isLittleEndian;

        public CpuFile()
        {
            cpuFilePath = "";
            gpuFilePath = "";
            msCpuFile = new MemoryStream();
            msGpuFile = new MemoryStream();
            entriesList = new List<Asset>();
            nodeDictionary = new Dictionary<uint, List<Node>>();
            resourceDictionary = new Dictionary<uint, List<Resource>>();
        }
        public CpuFile(string filePath)
        {
            cpuFilePath = filePath;
            gpuFilePath = filePath.Replace(".cpu.", ".gpu.");
            msCpuFile = new MemoryStream();
            msGpuFile = new MemoryStream();
            BinaryTools.WriteData(cpuFilePath, msCpuFile);
            if (File.Exists(gpuFilePath))
            {
                // Note: any operation that needs gpu data will fail without this file
                BinaryTools.WriteData(gpuFilePath, msGpuFile);
            }
            Parse(msCpuFile, out entriesList, out resourceDictionary, out nodeDictionary, out usedDataTypes, out isLittleEndian);
        }

        public Asset this[int index]
        {
            get => entriesList[index];
        }

        public int Count
        {
            get => entriesList.Count;
        }

        public static void Parse(
            Stream sCpuFile,
            out List<Asset> entriesList,
            out Dictionary<uint, List<Resource>> resourceDictionary,
            out Dictionary<uint, List<Node>> nodeDictionary,
            out HashSet<DataType> usedDataTypes,
            out bool isLittleEndian)
        {
            Node rootNode = new Node { dataType = DataType.SeRootFolderNode, id = 0xC5952A50, assetNumber = 0, name = "RootFolder" };
            entriesList = new List<Asset> { rootNode };
            resourceDictionary = new Dictionary<uint, List<Resource>>();
            nodeDictionary = new Dictionary<uint, List<Node>> { {0xC5952A50, new List<Node> { rootNode } } };
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
                    Asset entry = new Asset
                    {
                        assetNumber = entriesList.Count,
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
                    uint id;
                    if (b.ReadBoolean()) // Entry type
                    {
                        Resource resource = new Resource(entry);
                        entry = resource;

                        resource.id = b.ReadUInt32();
                        FindOrCreateAssetList(resourceDictionary, resource.id).Add(resource);

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
                                id = b.ReadUInt32();
                                resource.references.Add(new Resource() { id = id, dataType = DataType.SlShader });
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
                                    id = b.ReadUInt32();
                                    resource.references.Add(new Resource() { id = id, dataType = DataType.SlTexture });
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
                                    id = b.ReadUInt32();
                                    resource.references.Add(new Resource() { id = id, dataType = DataType.SlConstantBufferDesc });
                                }
                                break;
                            case DataType.SlAnim:
                                // SlSkeleton
                                b.BaseStream.Seek(resource.cpuOffsetData + 0x10, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                resource.references.Add(new Resource() { id = id, dataType = DataType.SlSkeleton });
                                break;
                            case DataType.SlModel:
                                // SlSkeleton
                                b.BaseStream.Seek(resource.cpuOffsetData + 0xC, SeekOrigin.Begin);
                                int offset2 = b.ReadInt32();
                                b.BaseStream.Seek(resource.cpuOffsetData + offset2 + 0xC, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                if (id != 0)
                                {
                                    resource.references.Add(new Resource() { id = id, dataType = DataType.SlSkeleton });
                                }
                                // SlMaterial
                                b.BaseStream.Seek(resource.cpuOffsetData + 0x40, SeekOrigin.Begin);
                                int materialCount = b.ReadInt32();
                                b.BaseStream.Seek(resource.cpuOffsetData + 0x60, SeekOrigin.Begin);
                                for (int i = 0; i < materialCount; i++)
                                {
                                    id = b.ReadUInt32();
                                    resource.references.Add(new Resource() { id = id, dataType = DataType.SlMaterial2 });
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
                        FindOrCreateAssetList(nodeDictionary, node.id).Add(node);

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
                            node.parent.Add(new Node { id = b.ReadUInt32() });
                        }
                        b.BaseStream.Seek(entry.cpuOffsetData + 0x68, SeekOrigin.Begin);
                        tmpOffset = b.ReadInt32();
                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            node.definition.Add(new Node { id = b.ReadUInt32() });
                        }
                        switch (node.dataType)
                        {
                            case DataType.Water13DefNode:
                                // Water13Simulation
                                b.BaseStream.Seek(entry.cpuOffsetData + 0xD0, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                node.references.Add(new Resource() { id = id, dataType = DataType.Water13Simulation });
                                // Water13Renderable
                                id = b.ReadUInt32();
                                node.references.Add(new Resource() { id = id, dataType = DataType.Water13Renderable });
                                break;
                            case DataType.Water13InstanceNode:
                                // Water13SurfaceWavesDefNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x1D0, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                node.references.Add(new Node() { id = id, dataType = DataType.Water13SurfaceWavesDefNode });
                                // WaterShader4DefinitionNode
                                id = b.ReadUInt32();
                                node.references.Add(new Node() { id = id, dataType = DataType.WaterShader4DefinitionNode });
                                break;
                            case DataType.SeDefinitionParticleEmitterNode:
                                // SeDefinitionParticleStyleNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x198, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                node.references.Add(new Node() { id = id, dataType = DataType.SeDefinitionParticleStyleNode });
                                break;
                            case DataType.SeDefinitionParticleStyleNode:
                                // SeDefinitionTextureNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x1D0, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                node.references.Add(new Node() { id = id, dataType = DataType.SeDefinitionTextureNode });
                                break;
                            case DataType.CameoObjectInstanceNode:
                                // SeInstanceSplineNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x1A4, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                if (id != 0)
                                {
                                    node.references.Add(new Node() { id = id, dataType = DataType.SeInstanceSplineNode });
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

            // Determine node links
            foreach (var pair in nodeDictionary)
            {
                foreach (var node in pair.Value)
                {
                    if (node.parent.Count > 0 && nodeDictionary.TryGetValue(node.parent[0].id, out List<Node> parents))
                    {
                        node.parent = parents;
                        foreach (Node parent in parents)
                        {
                            parent.daughters.Add(node);
                        }
                    }
                    if (node.definition.Count > 0 && nodeDictionary.TryGetValue(node.definition[0].id, out List<Node> definitions))
                    {
                        node.definition = definitions;
                        foreach (Node definition in definitions)
                        {
                            definition.instances.Add(node);
                        }
                    }
                    if (resourceDictionary.TryGetValue(node.id, out List<Resource> resources))
                    {
                        node.references.AddRange(resources);          
                    }
                }
            }
         
            // Determine referees and full asset references
            foreach (Asset entry in entriesList)
            {
                List<Asset> fullReferences = new List<Asset>();
                foreach (Asset reference in entry.references)
                {
                    if(resourceDictionary.TryGetValue(reference.id, out List<Resource> foundResources))
                    {
                        foreach(Resource resource in foundResources)
                        {
                            if (resource.dataType == reference.dataType)
                            {
                                fullReferences.Add(resource);
                                resource.referees.Add(entry);
                            }
                        }
                    }
                    else if (nodeDictionary.TryGetValue(reference.id, out List<Node> foundNodes))
                    {
                        foreach(Node node in foundNodes)
                        {
                            if(node.dataType == reference.dataType && 
                                (node.parent.Count == 0 || node.parent[0].id != entry.id)) // If a reference is already a node parent, don't add it to reference
                            {
                                fullReferences.Add(node);
                                node.referees.Add(entry);
                            }
                        }
                    }
                }
                entry.references = fullReferences;
            }
        }

        public static List<T> FindOrCreateAssetList<T>(Dictionary<uint, List<T>> dict, uint id) where T : Asset
        {
            if (!dict.TryGetValue(id, out List<T> assetList))
            {
                assetList = new List<T>();
                dict.Add(id, assetList);
            }      
            return assetList;
        }

        public void Reload()
        {
            Parse(msCpuFile, out entriesList, out resourceDictionary, out nodeDictionary, out usedDataTypes, out isLittleEndian);
        }

        public void GetCpuData(int entryIndex, Stream sOut, int outOffset = 0)
        {
            Asset entry = entriesList[entryIndex];
            BinaryTools.WriteData(msCpuFile, sOut, entry.cpuRelativeOffsetNextEntry, entry.cpuOffsetDataHeader, outOffset);
        }

        public void GetGpuData(int entryIndex, Stream sOut, int outOffset = 0)
        {
            Asset entry = entriesList[entryIndex];
            BinaryTools.WriteData(msGpuFile, sOut, entry.gpuRelativeOffsetNextEntry, entry.gpuOffsetData, outOffset);
        }

        public void InsertCpuData(int entryIndex, string inFilePath, int length = -1, int inOffset = 0)
        {
            using (FileStream fsIn = File.OpenRead(inFilePath))
            {
                Asset entry = entriesList[entryIndex];
                BinaryTools.InsertData(fsIn, msCpuFile, length, inOffset, entry.cpuOffsetDataHeader + entry.cpuRelativeOffsetNextEntry);
            }
        }

        public int InsertCpuData(int entryIndex, Stream sIn, int length = -1, int inOffset = 0)
        {
            Asset entry = entriesList[entryIndex];
            return BinaryTools.InsertData(sIn, msCpuFile, length, inOffset, entry.cpuOffsetDataHeader + entry.cpuRelativeOffsetNextEntry);
        }

        public int InsertGpuData(int entryIndex, string inFilePath, int length = -1, int inOffset = 0)
        {
            using (FileStream fsIn = File.OpenRead(inFilePath))
            {
                Asset entry = entriesList[entryIndex];
                return BinaryTools.InsertData(fsIn, msGpuFile, length, inOffset, entry.gpuOffsetData + entry.gpuRelativeOffsetNextEntry);
            }
        }
        public int InsertGpuData(int entryIndex, Stream sIn, int length = -1, int inOffset = 0)
        {
            Asset entry = entriesList[entryIndex];
            return BinaryTools.InsertData(sIn, msGpuFile, length, inOffset, entry.gpuOffsetData + entry.gpuRelativeOffsetNextEntry);
        }

        public void DeleteCpuData(int entryIndex)
        {
            Asset entry = entriesList[entryIndex];
            BinaryTools.ShrinkStream(msCpuFile, entry.cpuOffsetDataHeader, entry.cpuRelativeOffsetNextEntry);
        }

        public void DeleteGpuData(int entryIndex)
        {
            Asset entry = entriesList[entryIndex];
            BinaryTools.ShrinkStream(msGpuFile, entry.gpuOffsetData, entry.gpuRelativeOffsetNextEntry);
        }

        public int ReplaceCpuData(int entryIndex, string inFilePath, int length = -1, int inOffset = 0, bool updateHeader = true)
        {
            using (FileStream fsIn = File.OpenRead(inFilePath))
            {
                return ReplaceCpuData(entryIndex, fsIn, length, inOffset, updateHeader);
            }
        }

        public int ReplaceCpuData(int entryIndex, Stream sIn, int length = -1, int inOffset = 0, bool updateHeader = true)
        {
            DeleteCpuData(entryIndex);
            int bytesWritten = InsertCpuData(Math.Max(0, entryIndex - 1), sIn, length, inOffset);
            if (!updateHeader)
            {
                return 0;
            }
            // update gpuRelativeOffsetNextEntry and gpuFataLength in the cpu data header
            Asset entry = entriesList[entryIndex];
            ChangeGpuDataInfo(entryIndex, entry.gpuRelativeOffsetNextEntry, entry.gpuDataLength);
            return bytesWritten;
        }

        public int ReplaceGpuData(int entryIndex, string inFilePath, int length = -1, int inOffset = 0, bool updateHeader = true)
        {
            using (FileStream fsIn = File.OpenRead(inFilePath))
            {
                return ReplaceGpuData(entryIndex, fsIn, length, inOffset, updateHeader);
            }
        }

        public int ReplaceGpuData(int entryIndex, Stream sIn, int length = -1, int inOffset = 0, bool updateHeader = true)
        {
            if (length == -1)
            {
                length = (int)sIn.Length - (inOffset != -1 ? inOffset : 0);
            }
            DeleteGpuData(entryIndex);
            int bytesWritten = InsertGpuData(Math.Max(0, entryIndex - 1), sIn, length, inOffset);
            if (!updateHeader)
            {
                return 0;
            }
            // update gpuRelativeOffsetNextEntry and gpuDataLength in the cpu data header
            ChangeGpuDataInfo(entryIndex, length, length);
            return bytesWritten;
        }

        public void ChangeGpuDataInfo(int entryIndex, int gpuRelativeOffsetNextEntry, int gpuDataLength)
        {
            using (EndiannessAwareBinaryWriter b = new EndiannessAwareBinaryWriter(msCpuFile, isLittleEndian, Encoding.ASCII, true))
            {
                msCpuFile.Seek(entriesList[entryIndex].cpuOffsetDataHeader + 0x10, SeekOrigin.Begin);
                b.Write(gpuRelativeOffsetNextEntry);
                b.Write(gpuDataLength);
            }
        }

        public string SaveCpuData(int entryIndex, string outFolderPath)
        {
            Asset entry = entriesList[entryIndex];
            string start = entry.id.ToString("X8") + "_CPU_" + entry.dataType + "_";
            string end = Path.GetFileName(ReplaceInvalidChars(entry.name));
            string fileName = start + end.Substring(Math.Max(0, start.Length + end.Length - 255));

            BinaryTools.WriteData(msCpuFile, Path.Combine(outFolderPath, fileName), entry.cpuRelativeOffsetNextEntry, entry.cpuOffsetDataHeader);
            return fileName;
        }

        public string SaveGpuData(int entryIndex, string outFolderPath)
        {
            Asset entry = entriesList[entryIndex];
            if (entry.gpuDataLength > 0)
            {
                string start = entry.id.ToString("X8") + "_GPU_" + entry.dataType + "_";
                string end = Path.GetFileName(ReplaceInvalidChars(entry.name));
                string fileName = start + end.Substring(Math.Max(0, start.Length + end.Length - 255));
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

        public void WriteHeader(Stream s, Asset entry)
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
            Asset entry = entriesList[entryIndex];
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
            Asset entry = entriesList[entryIndex];
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
            Asset entry = entriesList[entryIndex];
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
            Asset entry = entriesList[entryIndex];
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
