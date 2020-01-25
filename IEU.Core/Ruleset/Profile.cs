using System.Collections.Generic;
using System.IO;
using DdsFileTypePlus;
using ProtoBuf;
using ReactiveUI;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
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

        private bool _preciseTileResolution = false;
        [ProtoMember(12)]
        public bool PreciseTileResolution
        {
            get => _preciseTileResolution;
            set
            {
                OverlapSize = 0;
                this.RaiseAndSetIfChanged(ref _preciseTileResolution, value);
            }
        }

        private int _overlapSize = 16;
        [ProtoMember(13)]
        public int OverlapSize
        {
            get => _overlapSize;
            set => this.RaiseAndSetIfChanged(ref _overlapSize, value);
        }
               

        private int _overwriteMode = 0;
        [ProtoMember(15)]
        public int OverwriteMode
        {
            get => _overwriteMode;
            set => this.RaiseAndSetIfChanged(ref _overwriteMode, value);
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

        private List<ImageFormatInfo> _formatInfos;
        public List<ImageFormatInfo> FormatInfos
        {
            get => _formatInfos;
            set
            {
                this.RaiseAndSetIfChanged(ref _formatInfos, value);
            }
        }

        public ImageFormatInfo selectedOutputFormat;

        int _selectedOutputFormatIndex = 0;
        [ProtoMember(26)]
        public int SelectedOutputFormatIndex
        {
            get => _selectedOutputFormatIndex;
            set
            {
                if (FormatInfos != null)
                    selectedOutputFormat = FormatInfos[value];
                this.RaiseAndSetIfChanged(ref _selectedOutputFormatIndex, value);
            }
        }

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


        [ProtoMember(28)]
        public ImageFormatInfo pngFormat = new ImageFormatInfo(".png")
        { CompressionFactor = 0 };
        [ProtoMember(29)]
        public ImageFormatInfo tiffFormat = new ImageFormatInfo(".tiff")
        { CompressionMethod = Dictionaries.TiffCompressionModes[TiffCompression.None], QualityFactor = 100 };
        [ProtoMember(30)]
        public ImageFormatInfo webpFormat = new ImageFormatInfo(".webp")
        { CompressionMethod = Dictionaries.WebpPresets[WebpPreset.Default], QualityFactor = 100 };
        public ImageFormatInfo tgaFormat = new ImageFormatInfo(".tga");
        public ImageFormatInfo ddsFormat = new ImageFormatInfo(".dds");
        public ImageFormatInfo jpgFormat = new ImageFormatInfo(".jpg");
        public ImageFormatInfo bmpFormat = new ImageFormatInfo(".bmp");

        #region DDS SETTINGS
        
        private List<DdsFileFormatSetting> _ddsFileFormatCurrent = new List<DdsFileFormatSetting>();
        public List<DdsFileFormatSetting> DdsFileFormatsCurrent
        {
            get => _ddsFileFormatCurrent;
            set
            {
                DdsFileFormat = value[0];
                this.RaiseAndSetIfChanged(ref _ddsFileFormatCurrent, value);
            }
        }

        private int _ddsTextureTypeSelectedIndex = 0;
        [ProtoMember(31)]
        public int DdsTextureTypeSelectedIndex
        {
            get => _ddsTextureTypeSelectedIndex;
            set
            {
                DdsFileFormatsCurrent = ddsFileFormats[value];
                DdsFileFormat = DdsFileFormatsCurrent[0];
                this.RaiseAndSetIfChanged(ref _ddsTextureTypeSelectedIndex, value);
            }
        }

        private int _ddsFileFormatSelectedIndex = 0;
        [ProtoMember(32)]
        public int DdsFileFormatSelectedIndex
        {
            get => _ddsFileFormatSelectedIndex;
            set
            {
                if (DdsFileFormatsCurrent.Count > 0)
                    DdsFileFormat = DdsFileFormatsCurrent[value];
                this.RaiseAndSetIfChanged(ref _ddsFileFormatSelectedIndex, value);
            }
        }

        private int _ddsBC7CompressionSelected = 0;
        [ProtoMember(33)]
        public int DdsBC7CompressionSelected
        {
            get => _ddsBC7CompressionSelected;
            set => this.RaiseAndSetIfChanged(ref _ddsBC7CompressionSelected, value);
        }               

        readonly static List<DdsFileFormatSetting> ddsFileFormatsColor = new List<DdsFileFormatSetting>
        {
            new DdsFileFormatSetting("BC1 (Linear) [DXT1]", DdsFileTypePlus.DdsFileFormat.BC1),
            new DdsFileFormatSetting("BC1 (sRGB) [DXT1]", DdsFileTypePlus.DdsFileFormat.BC1Srgb),
            new DdsFileFormatSetting("BC7 (Linear)", DdsFileTypePlus.DdsFileFormat.BC7),
            new DdsFileFormatSetting("BC7 (sRGB)", DdsFileTypePlus.DdsFileFormat.BC7Srgb),
            new DdsFileFormatSetting("BC4 (Grayscale)", DdsFileTypePlus.DdsFileFormat.BC4),
            new DdsFileFormatSetting("Loseless", DdsFileTypePlus.DdsFileFormat.R8G8B8A8)
        };
        readonly static List<DdsFileFormatSetting> ddsFileFormatsColorAlpha = new List<DdsFileFormatSetting>
        {
            new DdsFileFormatSetting("BC3 (Linear) [DXT5]", DdsFileTypePlus.DdsFileFormat.BC3),
            new DdsFileFormatSetting("BC3 (sRGB) [DXT5]", DdsFileTypePlus.DdsFileFormat.BC3Srgb),
            new DdsFileFormatSetting("BC2 (Linear)", DdsFileTypePlus.DdsFileFormat.BC2),
            new DdsFileFormatSetting("BC7 (Linear)", DdsFileTypePlus.DdsFileFormat.BC7),
            new DdsFileFormatSetting("BC7 (sRGB)", DdsFileTypePlus.DdsFileFormat.BC7Srgb),
            new DdsFileFormatSetting("Loseless", DdsFileTypePlus.DdsFileFormat.R8G8B8A8)
        };
        readonly static List<DdsFileFormatSetting> ddsFileFormatsNormalMap = new List<DdsFileFormatSetting>
        {
            new DdsFileFormatSetting("BC5 (Two channels)", DdsFileTypePlus.DdsFileFormat.BC5),
            new DdsFileFormatSetting("Loseless", DdsFileTypePlus.DdsFileFormat.R8G8B8A8)
        };

        readonly static List<DdsFileFormatSetting>[] ddsFileFormats = new List<DdsFileFormatSetting>[] {
            ddsFileFormatsColor, ddsFileFormatsColorAlpha, ddsFileFormatsNormalMap };

        public static List<BC7CompressionMode> ddsBC7CompressionModes = new List<BC7CompressionMode>
        {
            BC7CompressionMode.Fast,
            BC7CompressionMode.Normal,
            BC7CompressionMode.Slow
        };
        private DdsFileFormatSetting _ddsFileFormat;
        public DdsFileFormatSetting DdsFileFormat
        {
            get => _ddsFileFormat;
            set => this.RaiseAndSetIfChanged(ref _ddsFileFormat, value);
        }
        BC7CompressionMode _ddsBC7CompressionMode;
        public BC7CompressionMode DdsBC7CompressionMode
        {
            get => _ddsBC7CompressionMode;
            set => this.RaiseAndSetIfChanged(ref _ddsBC7CompressionMode, value);
        }
        [ProtoMember(34, IsRequired = true)]
        public bool ddsGenerateMipmaps = true;

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

        #endregion

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
            DdsFileFormatsCurrent = ddsFileFormatsColor;
            FormatInfos = new List<ImageFormatInfo>() { pngFormat, tiffFormat, webpFormat, tgaFormat, ddsFormat, jpgFormat, bmpFormat };
            selectedOutputFormat = FormatInfos[0];
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
