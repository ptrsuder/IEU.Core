using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    class SplitInfo
    {
        [ProtoMember(1)]
        public int OverlapSize;
        [ProtoMember(2, IsRequired = true)]
        public bool Seamless;
        [ProtoMember(3)]
        public double ResizeModifier;
        [ProtoMember(4, IsRequired = true)]
        public bool IgnoreAlpha;
        [ProtoMember(5, IsRequired = true)]
        public bool AlphaDifModel;
        [ProtoMember(6, IsRequired = true)]
        public bool AlphaFilter;
        [ProtoMember(7, IsRequired = true)]
        public bool SplitRGB;

        public SplitInfo() { }

        public void WriteToFile(string path)
        {
            FileStream fileStream = new FileStream(path + "split_info.proto", FileMode.Create);
            Serializer.Serialize(fileStream, this);
            fileStream.Close();
        }
        public void ReadSettings(string path)
        {
            if (!File.Exists(path + "split_info.proto"))
                return;
            FileStream fileStream = new FileStream(path + "split_info.proto", FileMode.Open)
            {
                Position = 0
            };
            Serializer.Merge(fileStream, this);
            fileStream.Close();
        }
    }
}
