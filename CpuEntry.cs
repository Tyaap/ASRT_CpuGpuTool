using System.Collections.Generic;

namespace CpuGpuTool
{
    public class CpuEntry
    {
        public int entryNumber;
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
        public Dictionary<uint, Resource> referencedResources = new Dictionary<uint, Resource>();

        public override int GetHashCode()
        {
            return (int)id;
        }

        public override bool Equals(object obj)
        {
            CpuEntry entry = obj as CpuEntry;
            return entry != null && entry.id == id;
        }
    }

    public class Node : CpuEntry
    {
        public Node() { }
        public Node(CpuEntry entry)
        {
            entryNumber = entry.entryNumber;
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
        public Node definition;
        public Node parent;
        public Dictionary<uint, Node> daughters = new Dictionary<uint, Node>();
        public Dictionary<uint, Node> instances = new Dictionary<uint, Node>();
    }

    public class Resource : CpuEntry
    {
        public Resource() { }
        public Resource(CpuEntry entry)
        {
            entryNumber = entry.entryNumber;
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

        public Dictionary<uint, CpuEntry> referees = new Dictionary<uint, CpuEntry>();
    }
}
