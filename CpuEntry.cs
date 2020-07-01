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
    }

    public class Node : CpuEntry
    {
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
        public uint definitionId;
        public uint parentId;
        public List<uint> daughterIds;
        public List<uint> instanceIds;
        public uint partenerResourceId;
    }

    public class Resource : CpuEntry
    {
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

        public uint partenerNodeId;
    }
}
