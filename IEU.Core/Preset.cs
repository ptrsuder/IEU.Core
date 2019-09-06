using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    public class Preset
    {
        [ProtoMember(1)]
        public string Name;
        [ProtoMember(2)]
        public string Description;
        [ProtoMember(3)]
        public IEU Config;

        public bool IsGlobal = false;
        public int Priority = 0;


        public Preset()
        {

        }
        public Preset(string name)
        {
            Name = name;
        }

        bool ApplyRuleset()
        {
            return true;
        }

        void ApplyPreset()
        {
            Config.IgnoreAlpha;
            Config.IgnoreSingleColorAlphas;
            Config.BalanceAlphas;
            
            Config.ModelForAlpha;
            Config.UseDifferentModelForAlpha;
            Config.UseOriginalImageFormat;
            Config.SelectedOutputFormat;
            Config.SeamlessTexture;


        }

        void ApplyGlobalPreset(IEU ieu)
        {
            ieu = Config;
        }

        public static Preset Load(string name)
        {
            string path = "/presets/" + name + ".proto";
            if (!File.Exists(path))
                return null;
            FileStream fileStream = new FileStream(path, FileMode.Open)
            {
                Position = 0
            };
            Preset result = Serializer.Deserialize<Preset>(fileStream);
            fileStream.Close();
            return result;
        }

        public void Write(string path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = "/presets/" + Name + ".proto";
            FileStream fileStream = new FileStream(path, FileMode.Create);
            Serializer.Serialize(fileStream, this);
            fileStream.Close();
        }
    }
}
