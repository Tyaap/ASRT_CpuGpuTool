namespace CpuGpuTool
{
    public struct CpuEntry
    {
        public int entryNumber;
        public EntryType entryType;
        public DataType dataType;
        public int toolVersion;
        public int cpuOffsetData;
        public int cpuOffsetHeader;
        public int cpuOffsetNextEntry;
        public int cpuDataLength;
        public int gpuOffsetData;
        public int gpuOffsetNextEntry;
        public int gpuDataLength;
        public int unknown;
        public string name;
        public uint id;
    }
}
