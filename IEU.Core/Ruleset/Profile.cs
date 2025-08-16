using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using DdsFileTypePlus;
using Newtonsoft.Json;
using ProtoBuf;
using ReactiveUI;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    [JsonObject(MemberSerialization.OptOut)]
    public class Profile: ReactiveObject
    {
        [ProtoMember(1)]        
        public string Name { get; set; }
        [ProtoMember(2)]
        public string Description;   

        public bool IsGlobal = false;

        bool _ignoreAlpha = false;
        [ProtoMember(4)]
        public bool IgnoreAlpha
        {
            get => _ignoreAlpha;
            set => this.RaiseAndSetIfChanged(ref _ignoreAlpha, value);            
        }

        bool _ignoreSingleColorAlphas = true;
        [ProtoMember(5, IsRequired = true)]
        public bool IgnoreSingleColorAlphas
        {
            get => _ignoreSingleColorAlphas;
            set => this.RaiseAndSetIfChanged(ref _ignoreSingleColorAlphas, value);
        }

        bool _balanceMonotonicImages = false;
        [ProtoMember(6)]
        public bool BalanceMonotonicImages
        {
            get => _balanceMonotonicImages;
            set => this.RaiseAndSetIfChanged(ref _balanceMonotonicImages, value);
        }

        bool _balanceAlphas = false;
        [ProtoMember(7, IsRequired = true)]
        public bool BalanceAlphas
        {
            get => _balanceAlphas;
            set => this.RaiseAndSetIfChanged(ref _balanceAlphas, value);
        }

        bool _balanceRgb = false;
        [ProtoMember(38, IsRequired = true)]
        public bool BalanceRgb
        {
            get => _balanceRgb;
            set => this.RaiseAndSetIfChanged(ref _balanceRgb, value);
        }

        bool _useOriginalImageFormat = false;
        [ProtoMember(8)]
        public bool UseOriginalImageFormat
        {
            get => _useOriginalImageFormat;
            set => this.RaiseAndSetIfChanged(ref _useOriginalImageFormat, value);
        }

        bool _deleteResults = false;
        [ProtoMember(9)]
        public bool DeleteResults
        {
            get => _deleteResults;
            set => this.RaiseAndSetIfChanged(ref _deleteResults, value);
        }
                      
        private int _noiseReductionType = 0;
        [ProtoMember(16)]
        public int NoiseReductionType
        {
            get => _noiseReductionType;
            set => _noiseReductionType = value;
        }

        bool _thresholdEnabled = false;
        [ProtoMember(17)]
        public bool ThresholdEnabled
        {
            get => _thresholdEnabled;
            set => this.RaiseAndSetIfChanged(ref _thresholdEnabled, value);
        }

        bool _thresholdEnabledAlpha = false;
        [ProtoMember(39)]
        public bool ThresholdAlphaEnabled
        {
            get => _thresholdEnabledAlpha;
            set => this.RaiseAndSetIfChanged(ref _thresholdEnabledAlpha, value);
        }

        private int _thresholdBlackValue = 0;
        [ProtoMember(18)]
        public int ThresholdBlackValue
        {
            get => _thresholdBlackValue;
            set => this.RaiseAndSetIfChanged(ref _thresholdBlackValue, value);
        }
        private int _thresholdWhiteValue = 100;
        [ProtoMember(19)]
        public int ThresholdWhiteValue
        {
            get => _thresholdWhiteValue;
            set => this.RaiseAndSetIfChanged(ref _thresholdWhiteValue, value);
        }

        private double _resizeImageBeforeScaleFactor = 1.0;
        [ProtoMember(20)]
        public double ResizeImageBeforeScaleFactor
        {
            get => _resizeImageBeforeScaleFactor;
            set => this.RaiseAndSetIfChanged(ref _resizeImageBeforeScaleFactor, value);
        }
        private double _resizeImageAfterScaleFactor = 1.0;
        [ProtoMember(21)]
        public double ResizeImageAfterScaleFactor
        {
            get => _resizeImageAfterScaleFactor;
            set => this.RaiseAndSetIfChanged(ref _resizeImageAfterScaleFactor, value);
        }

        private int _resizeImageBeforeFilterType = 2;
        [ProtoMember(22)]
        public int ResizeImageBeforeFilterType
        {
            get => _resizeImageBeforeFilterType;
            set => this.RaiseAndSetIfChanged(ref _resizeImageBeforeFilterType, value);
        }
        private int _resizeImageAfterFilterType = 2;
        [ProtoMember(23)]
        public int ResizeImageAfterFilterType
        {
            get => _resizeImageAfterFilterType;
            set => this.RaiseAndSetIfChanged(ref _resizeImageAfterFilterType, value);
        }

        bool _splitRGB = false;
        [ProtoMember(24)]
        public bool SplitRGB
        {
            get => _splitRGB;
            set => this.RaiseAndSetIfChanged(ref _splitRGB, value);
        }

        bool _seamlessTexture = false;
        [ProtoMember(25)]
        public bool SeamlessTexture
        {
            get => _seamlessTexture;
            set => this.RaiseAndSetIfChanged(ref _seamlessTexture, value);
        }
             

        public ImageFormatInfo OutputFormat;

       

        bool _useDifferentModelForAlpha = false;
        [ProtoMember(27)]
        public bool UseDifferentModelForAlpha
        {
            get => _useDifferentModelForAlpha;
            set
            {
                if (value == true && UseFilterForAlpha == true)
                    UseFilterForAlpha = false;
                this.RaiseAndSetIfChanged(ref _useDifferentModelForAlpha, value);
            }
        }

        private ModelInfo _modelForAlpha;
        [ProtoMember(37)]
        public ModelInfo ModelForAlpha
        {
            get => _modelForAlpha;
            set => this.RaiseAndSetIfChanged(ref _modelForAlpha, value);
        }

        bool _useFilterForAlpha = false;
        [ProtoMember(40)]
        public bool UseFilterForAlpha
        {
            get => _useFilterForAlpha;
            set
            {
                if (value == true && UseDifferentModelForAlpha == true)
                    UseDifferentModelForAlpha = false;
                this.RaiseAndSetIfChanged(ref _useFilterForAlpha, value);
            }
        }

        private int _alphaFilterType = 2;
        [ProtoMember(41)]
        public int AlphaFilterType
        {
            get => _alphaFilterType;
            set => this.RaiseAndSetIfChanged(ref _alphaFilterType, value);
        }   


        

        bool _useModel = false;
        [ProtoMember(35)]
        public bool UseModel
        {
            get => _useModel;
            set => this.RaiseAndSetIfChanged(ref _useModel, value);
        }      

        ModelInfo _model;
        [ProtoMember(36)]
        public ModelInfo Model
        {
            get => _model;
            set => this.RaiseAndSetIfChanged(ref _model, value);
        }

       
        private int _paddingSize = 0;
        [ProtoMember(53)]
        public int PaddingSize
        {
            get => _paddingSize;
            set => this.RaiseAndSetIfChanged(ref _paddingSize, value);
        }

        private bool _useJoey = false;
        [ProtoMember(60)]
        public bool UseJoey
        {
            get => _useJoey;
            set => this.RaiseAndSetIfChanged(ref _useJoey, value);
        }

        private bool _rgbaModel = false;
        [ProtoMember(61)]
        public bool RgbaModel
        {
            get => _rgbaModel;
            set => this.RaiseAndSetIfChanged(ref _rgbaModel, value);
        }

        private bool _strictTiling = true;
        [ProtoMember(62)]
        public bool StrictTiling
        {
            get => _strictTiling;
            set => this.RaiseAndSetIfChanged(ref _strictTiling, value);
        }

        public Profile()
        {
            Init();
        }

        public Profile(string name)
        {
            Name = name;
            Init();
        }  
        
        void Init()
        {
            OutputFormat = IEU.DefaultFormats[0];
            OutputFormat.DdsFileFormatsCurrent = ImageFormatInfo.ddsFileFormatsColor;           
            
        }
                
        public static Profile Load(string name)
        {
            string path = "/presets/" + name + ".proto";
            if (!File.Exists(path))
                return null;
            FileStream fileStream = new FileStream(path, FileMode.Open)
            {
                Position = 0
            };
            Profile result = Serializer.Deserialize<Profile>(fileStream);
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

        public Profile Clone()
        {
            MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, this);
            stream.Position = 0;
            Profile clone = Serializer.Deserialize<Profile>(stream);
            stream.Close();
            return clone;
        }

        public static void Copy(Profile profileFrom, Profile profileTo)
        {
            MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, profileFrom);
            stream.Position = 0;
            Serializer.Merge(stream, profileTo);
            stream.Close();          
        }
    }

}
