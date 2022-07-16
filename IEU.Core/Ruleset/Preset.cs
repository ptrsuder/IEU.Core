using System.Collections.Generic;
using System.IO;
using ProtoBuf;
using ReactiveUI;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    public class Preset : ReactiveObject
    {
        [ProtoMember(1)]
        public string Name { get; set; }
        [ProtoMember(2)]
        public Profile Profile { get; set; }
        [ProtoMember(3)]
        public Filter Filter { get; set; }

        private int _overwriteMode = 0;
        public int OverwriteMode
        {
            get => _overwriteMode;
            set => this.RaiseAndSetIfChanged(ref _overwriteMode, value);
        }

        private int _overlapSize = 16;
        [ProtoMember(12, IsRequired = true)]
        public int OverlapSize
        {
            get => _overlapSize;
            set => this.RaiseAndSetIfChanged(ref _overlapSize, value);
        }

        Dictionary<string, int> _outputDestinationModes = Dictionaries.OutputDestinationModesSingleModel;
        public Dictionary<string, int> OutputDestinationModes
        {
            get => _outputDestinationModes;
            set
            {
                if (value == Dictionaries.OutputDestinationModesMultModels)
                    OverwriteModes = Dictionaries.OverwriteModesNone;
                else
                    OverwriteModes = Dictionaries.OverwriteModesAll;
                this.RaiseAndSetIfChanged(ref _outputDestinationModes, value);
            }
        }

        private int _outputDestinationMode = 0;
        [ProtoMember(13)]
        public int OutputDestinationMode
        {
            get => _outputDestinationMode;
            set
            {
                if (value == 1 || value == 2)
                    OverwriteModes = Dictionaries.OverwriteModesNone;
                else
                    OverwriteModes = Dictionaries.OverwriteModesAll;
                this.RaiseAndSetIfChanged(ref _outputDestinationMode, value);
            }
        }

        Dictionary<string, int> _overwriteModes = Dictionaries.OverwriteModesAll;
        public Dictionary<string, int> OverwriteModes
        {
            get => _overwriteModes;
            set => this.RaiseAndSetIfChanged(ref _overwriteModes, value);
        }

        bool _createMemoryImage = false;
        [ProtoMember(14, IsRequired = true)]
        public bool CreateMemoryImage
        {
            get => _createMemoryImage;
            set => this.RaiseAndSetIfChanged(ref _createMemoryImage, value);
        }

        bool _useCPU = false;
        [ProtoMember(16)]
        public bool UseCPU
        {
            get => _useCPU;
            set => this.RaiseAndSetIfChanged(ref _useCPU, value);
        }

        bool _useBasicSR = false;
        [ProtoMember(17)]
        public bool UseBasicSR
        {
            get => _useBasicSR;
            set => this.RaiseAndSetIfChanged(ref _useBasicSR, value);
        }       

        bool _inMemoryMode = true;
        [ProtoMember(29, IsRequired = true)]
        public bool InMemoryMode
        {
            get => _inMemoryMode;
            set => this.RaiseAndSetIfChanged(ref _inMemoryMode, value);
        }

        bool _useImageMagickMerge = false;
        [ProtoMember(30, IsRequired = true)]
        public bool UseImageMagickMerge
        {
            get => _useImageMagickMerge;
            set => this.RaiseAndSetIfChanged(ref _useImageMagickMerge, value);
        }

        bool _debugMode = false;
        [ProtoMember(31)]
        public bool DebugMode
        {
            get => _debugMode;
            set => this.RaiseAndSetIfChanged(ref _debugMode, value);
        }

        bool _useModelChain = false;
        [ProtoMember(32)]
        public bool UseModelChain
        {
            get => _useModelChain;
            set => this.RaiseAndSetIfChanged(ref _useModelChain, value);
        }

        private bool _useResultSuffix = false;
        [ProtoMember(42)]
        public bool UseResultSuffix
        {
            get => _useResultSuffix;
            set => this.RaiseAndSetIfChanged(ref _useResultSuffix, value);
        }

        private string _resultSuffix = "";
        [ProtoMember(43)]
        public string ResultSuffix
        {
            get => _resultSuffix;
            set => this.RaiseAndSetIfChanged(ref _resultSuffix, value);
        }

        private bool _autoSetTileSizeEnable = false;
        [ProtoMember(46, IsRequired = true)]
        public bool AutoSetTileSizeEnable
        {
            get => _autoSetTileSizeEnable;
            set => this.RaiseAndSetIfChanged(ref _autoSetTileSizeEnable, value);
        }


        private int _maxTileResolution = 512 * 380;
        public int MaxTileResolution
        {
            get => _maxTileResolution;
            set => this.RaiseAndSetIfChanged(ref _maxTileResolution, value);
        }

        private int _maxTileResolutionWidth = 512;
        [ProtoMember(70)]
        public int MaxTileResolutionWidth
        {
            get => _maxTileResolutionWidth;
            set
            {
                MaxTileResolution = value * MaxTileResolutionHeight;
                this.RaiseAndSetIfChanged(ref _maxTileResolutionWidth, value);
            }
        }

        private int _maxTileResolutionHeight = 380;
        [ProtoMember(71)]
        public int MaxTileResolutionHeight
        {
            get => _maxTileResolutionHeight;
            set
            {
                if (value == 0)
                    value = 16;
                MaxTileResolution = value * MaxTileResolutionWidth;
                this.RaiseAndSetIfChanged(ref _maxTileResolutionHeight, value);
            }
        }
        
        public Preset() { }
        public Preset(string name) { Name = name; }
        public Preset(string name, Profile profile, Filter filter)
        {
            Name = name;
            Profile = profile;
            Filter = filter;
        }
        public Preset Clone()
        {
            MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, this);
            stream.Position = 0;
            Preset clone = Serializer.Deserialize<Preset>(stream);
            stream.Close();
            return clone;
        }
        public static Preset Load(string name)
        {
            string path = "./presets/" + name + ".proto";
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
                path = "./presets/" + Name + ".proto";
            Directory.CreateDirectory("./presets/");
            FileStream fileStream = new FileStream(path, FileMode.Create);
            Serializer.Serialize(fileStream, this);
            fileStream.Close();
        }


    }
}
