using ProtoBuf;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    public class ModelInfo
    {
        [ProtoMember(1)]
        public string Name
        { get; set; }

        [ProtoMember(2)]
        public string FullName
        { get; set; }

        [ProtoMember(3)]
        public string ParentFolder
        { get; set; }

        [ProtoMember(4)]
        public int UpscaleFactor
        { get; set; }

        public ModelInfo(string name, string path)
        {
            Name = name;
            FullName = path;
            ParentFolder = "";
        }

        public ModelInfo(string name, string path, string folder)
        {
            Name = name;
            FullName = path;
            ParentFolder = folder;
        }

        public ModelInfo() {}
    }
}
