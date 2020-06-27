using System.Collections.Generic;

namespace CpuGpuTool
{
    public struct CpuEntry
    {
        public int entryNumber;
        public EntryType entryType;
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
        public string shortName;
        public uint id;
        public uint definitionId;
        public uint parentId;
        public List<uint> daughterIds;
        public List<uint> instanceIds;
    }
}
