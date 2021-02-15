using System.Collections.Generic;
using System.IO;

namespace CpuGpuTool
{
    public class Asset
    {
        // todo: more subclasses of asset
        public MemoryStream msCpuData; // todo: individual memory streams for each asset
        public MemoryStream msGpuData; // todo: individual memory streams for each asset
        public int assetNumber = -1;
        public DataType dataType;
        public int toolVersion;
        public int cpuOffsetDataHeader;
        public int cpuOffsetData;
        public int cpuDataLength;
        public int cpuOffsetPointersHeader;
        public int cpuOffsetPointers;
        public int cpuPointersLength;
        public int cpuRelativeOffsetNextEntry;
        public int gpuOffsetData;
        public int gpuDataLength;
        public int gpuRelativeOffsetNextEntry;
        public int unknown;
        public string name;
        public uint id;
        public List<Asset> references = new List<Asset>();
        public List<Asset> referees = new List<Asset>();

        public override int GetHashCode()
        {
            return (int)id;
        }

        public override bool Equals(object obj)
        {
            Asset entry = obj as Asset;
            return entry != null && entry.id == id;
        }
    }

    public class Node : Asset
    {
        public Node() { }
        public Node(Asset entry)
        {
            assetNumber = entry.assetNumber;
            dataType = entry.dataType;
            toolVersion = entry.toolVersion;
            cpuOffsetDataHeader = entry.cpuOffsetDataHeader;
            cpuOffsetData = entry.cpuOffsetData;
            cpuDataLength = entry.cpuDataLength;
            cpuOffsetPointersHeader = entry.cpuOffsetPointersHeader;
            cpuOffsetPointers = entry.cpuOffsetPointers;
            cpuPointersLength = entry.cpuPointersLength;
            cpuRelativeOffsetNextEntry = entry.cpuRelativeOffsetNextEntry;
            gpuOffsetData = entry.gpuOffsetData;
            gpuDataLength = entry.gpuDataLength;
            gpuRelativeOffsetNextEntry = entry.gpuRelativeOffsetNextEntry;
            unknown = entry.unknown;
            name = entry.name;
            id = entry.id;
        }

        public string shortName;
        public List<Node> definition = new List<Node>();
        public List<Node> parent = new List<Node>();
        public List<Node> daughters = new List<Node>();
        public List<Node> instances = new List<Node>();
    }

    public class Resource : Asset
    {
        public Resource() { }
        public Resource(Asset entry)
        {
            assetNumber = entry.assetNumber;
            dataType = entry.dataType;
            toolVersion = entry.toolVersion;
            cpuOffsetDataHeader = entry.cpuOffsetDataHeader;
            cpuOffsetData = entry.cpuOffsetData;
            cpuDataLength = entry.cpuDataLength;
            cpuOffsetPointersHeader = entry.cpuOffsetPointersHeader;
            cpuOffsetPointers = entry.cpuOffsetPointers;
            cpuPointersLength = entry.cpuPointersLength;
            cpuRelativeOffsetNextEntry = entry.cpuRelativeOffsetNextEntry;
            gpuOffsetData = entry.gpuOffsetData;
            gpuDataLength = entry.gpuDataLength;
            gpuRelativeOffsetNextEntry = entry.gpuRelativeOffsetNextEntry;
            unknown = entry.unknown;
            name = entry.name;
            id = entry.id;
        }
    }
}
