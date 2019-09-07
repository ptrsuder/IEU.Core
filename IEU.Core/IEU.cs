using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DdsFileTypePlus;
using DynamicData;
using ImageEnhancingUtility.BasicSR;
using ImageMagick;
using NetVips;
using PaintDotNet;
using ProtoBuf;
using ReactiveUI;
using Color = System.Drawing.Color;
using Image = NetVips.Image;
using Path = System.IO.Path;
using ReactiveCommand = ReactiveUI.ReactiveCommand;
using Unit = System.Reactive.Unit;


//TODO:
//new filter: (doesn't)have result
//write log file
[assembly: InternalsVisibleTo("ImageEnhancingUtility.Tests")]
namespace ImageEnhancingUtility.Core
{
    [ProtoContract]    
    public class IEU : ReactiveObject
    {
        public readonly string AppVersion = "0.11.01";
        public readonly string GitHubRepoName = "IEU.Core";

        public static Dictionary<TiffCompression, string> TiffCompressionModes = new Dictionary<TiffCompression, string>()
        {
            { TiffCompression.None, "VIPS_FOREIGN_TIFF_COMPRESSION_NONE" },
            { TiffCompression.Jpeg, "VIPS_FOREIGN_TIFF_COMPRESSION_JPEG" },
            { TiffCompression.Deflate, "VIPS_FOREIGN_TIFF_COMPRESSION_DEFLATE" },
            { TiffCompression.LZW, "VIPS_FOREIGN_TIFF_COMPRESSION_LZW" }
        };

        public static Dictionary<WebpPreset, string> WebpPresets = new Dictionary<WebpPreset, string>()
        {
            { WebpPreset.Default, "VIPS_FOREIGN_WEBP_PRESET_DEFAULT" },
            { WebpPreset.Picture, "VIPS_FOREIGN_WEBP_PRESET_PICTURE" },
            { WebpPreset.Photo, "VIPS_FOREIGN_WEBP_PRESET_PHOTO" },
            { WebpPreset.Drawing, "VIPS_FOREIGN_WEBP_PRESET_DRAWING" },
            { WebpPreset.Icon, "VIPS_FOREIGN_WEBP_PRESET_ICON" },
            { WebpPreset.Text, "VIPS_FOREIGN_WEBP_PRESET_TEXT" },
        };
        
        #region PROPERTIES

        public double WindowMinWidth = 800, WindowMinHeight = 650;

        public bool IsSub = false;

        List<Task> tasks;
        int hotModelUpscaleSize = 0;

        #region DDS SETTINGS
      
        public static Dictionary<string, int> ddsTextureType = new Dictionary<string, int>
            {
                { "Color", 0 },
                { "Color + Alpha", 1 },
                { "Normal Map", 2 }
            };

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
        [ProtoMember(20)]
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
        [ProtoMember(21)]
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
        [ProtoMember(22)]
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
        BC7CompressionMode DdsBC7CompressionMode
        {
            get => _ddsBC7CompressionMode;
            set => this.RaiseAndSetIfChanged(ref _ddsBC7CompressionMode, value);
        }
        [ProtoMember(31, IsRequired = true)]
        public bool ddsGenerateMipmaps = true;

        #endregion

        private List<ModelInfo> _modelsItems = new List<ModelInfo>();
        public List<ModelInfo> ModelsItems
        {
            get => _modelsItems;
            set => this.RaiseAndSetIfChanged(ref _modelsItems, value);
        }
        private List<ModelInfo> _selectedModelsItems = new List<ModelInfo>();
        public List<ModelInfo> SelectedModelsItems
        {
            get => _selectedModelsItems;
            set
            {
                if (value.Count > 1 && _selectedModelsItems.Count <= 1)
                    OutputDestinationModes = outputDestinationModesMultModels;
                if (value.Count <= 1 && _selectedModelsItems.Count > 1)
                {
                    int temp = OutputDestinationMode;
                    OutputDestinationModes = outputDestinationModesSingleModel;
                    OutputDestinationMode = temp;
                }
                this.RaiseAndSetIfChanged(ref _selectedModelsItems, value);
            }
        }
        public List<ModelInfo> checkedModels;

        private double _windowWidth = 800;
        [ProtoMember(1)]
        public double WindowWidth
        {
            get => _windowWidth;
            set => this.RaiseAndSetIfChanged(ref _windowWidth, value);
        }
        private double _windowHeight = 650;
        [ProtoMember(2)]
        public double WindowHeight
        {
            get => _windowHeight;
            set => this.RaiseAndSetIfChanged(ref _windowHeight, value);
        }
        private double _logPanelWidth = 200;
        [ProtoMember(24)]
        public double LogPanelWidth
        {
            get => _logPanelWidth;
            set => this.RaiseAndSetIfChanged(ref _logPanelWidth, value);
        }
              
        //Preset GloabalPreset;

        #region FOLDER_PATHS
        private string _esrganPath;
        [ProtoMember(8)]
        public string EsrganPath
        {
            get => _esrganPath;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (string.IsNullOrEmpty(ModelsPath))
                        ModelsPath = $"{value}{DirectorySeparator}models";
                    if (string.IsNullOrEmpty(LrPath))
                        LrPath = $"{value}{DirectorySeparator}LR";
                    if (string.IsNullOrEmpty(ResultsPath))
                        ResultsPath = $"{value}{DirectorySeparator}results";
                    if (string.IsNullOrEmpty(InputDirectoryPath))
                        InputDirectoryPath = $"{value}{DirectorySeparator}IEU_input";
                    if (!Directory.Exists(InputDirectoryPath))
                        Directory.CreateDirectory(InputDirectoryPath);
                    if (string.IsNullOrEmpty(OutputDirectoryPath))
                        OutputDirectoryPath = $"{value}{DirectorySeparator}IEU_output";
                    if (!Directory.Exists(OutputDirectoryPath))
                        Directory.CreateDirectory(OutputDirectoryPath);
                    this.RaiseAndSetIfChanged(ref _esrganPath, value);
                }
            }
        }
        private string _modelsPath;
        [ProtoMember(3)]
        public string ModelsPath
        {
            get => _modelsPath;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    this.RaiseAndSetIfChanged(ref _modelsPath, value);
                    CreateModelTree();
                    if (ModelsItems != null && ModelsItems.Count > 0)
                        LastModelForAlphaPath = ModelsItems[0].FullName;
                }
            }
        }
        private string _imgPath;
        [ProtoMember(4)]
        public string InputDirectoryPath
        {
            get => _imgPath;
            set => this.RaiseAndSetIfChanged(ref _imgPath, value);
        }
        private string _resultsMergedPath;
        [ProtoMember(5)]
        public string OutputDirectoryPath
        {
            get => _resultsMergedPath;
            set => this.RaiseAndSetIfChanged(ref _resultsMergedPath, value);
        }
        private string _lrPath;
        [ProtoMember(6)]
        public string LrPath
        {
            get => _lrPath;
            set => this.RaiseAndSetIfChanged(ref _lrPath, value);
        }
        private string _resultsPath;
        [ProtoMember(7)]
        public string ResultsPath
        {
            get => _resultsPath;
            set => this.RaiseAndSetIfChanged(ref _resultsPath, value);
        }
        #endregion

        #region SETTINGS

       
        [ProtoMember(9)]
        public bool CreateMemoryImage = false;      
          
        [ProtoMember(10)]
        public bool IgnoreAlpha = false;
       
        [ProtoMember(34, IsRequired = true)]
        public bool IgnoreSingleColorAlphas = true;

        [ProtoMember(35)]
        public bool BalanceMonotonicImages = false;

        [ProtoMember(36, IsRequired = true)]
        public bool BalanceAlphas = true;
        
        [ProtoMember(11)]
        public bool UseOriginalImageFormat = false;     
               
        [ProtoMember(12)]
        public bool DeleteResults = false;      

        private int _maxTileResolution = 512 * 380;
        public int MaxTileResolution
        {
            get => _maxTileResolution;
            set => this.RaiseAndSetIfChanged(ref _maxTileResolution, value);
        }           

        private int _maxTileResolutionWidth = 512;
        [ProtoMember(14)]
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
        [ProtoMember(15)]
        public int MaxTileResolutionHeight
        {
            get => _maxTileResolutionHeight;
            set
            {
                if(value == 0)
                    value = 16;
                MaxTileResolution = value * MaxTileResolutionWidth;
                this.RaiseAndSetIfChanged(ref _maxTileResolutionHeight, value);
            }
        }

        private bool _preciseTileResolution = false;
        [ProtoMember(40)]
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
        [ProtoMember(25)]
        public int OverlapSize
        {
            get => _overlapSize;
            set => this.RaiseAndSetIfChanged(ref _overlapSize, value);
        }
        
        readonly static Dictionary<string, int> outputDestinationModesMultModels = new Dictionary<string, int>
        {
                { "Folder for each image", 1 },
                { "Folder for each model", 2 }
        };
        readonly static Dictionary<string, int> outputDestinationModesSingleModel = new Dictionary<string, int>
        {
                { "Default", 0 },
                { "Preserve folder structure", 3 },
                { "Folder for each image", 1 },
                { "Folder for each model", 2 }    
        };

        Dictionary<string, int> _outputDestinationModes = outputDestinationModesSingleModel;
        public Dictionary<string, int> OutputDestinationModes
        {
            get => _outputDestinationModes;
            set
            {
                if (value == outputDestinationModesMultModels)
                    OverwriteModes = overwriteModesNone;
                else
                    OverwriteModes = overwriteModesAll;
                this.RaiseAndSetIfChanged(ref _outputDestinationModes, value);
            }
        }            

        private int _outputDestinationMode = 0;
        [ProtoMember(17)]
        public int OutputDestinationMode
        {
            get => _outputDestinationMode;
            set
            {
                if (value == 1 || value == 2)
                {
                    OverwriteModes = overwriteModesNone;                    
                }
                else
                    OverwriteModes = overwriteModesAll;
                this.RaiseAndSetIfChanged(ref _outputDestinationMode, value);
            }
        }

        Dictionary<string, int> _overwriteModes = overwriteModesAll;
        public Dictionary<string, int> OverwriteModes
        {
            get => _overwriteModes;
            set
            {
                this.RaiseAndSetIfChanged(ref _overwriteModes, value);
            }
        } 
        readonly static Dictionary<string, int> overwriteModesAll = new Dictionary<string, int>
        {
                { "None", 0 },
                { "Tiles", 1 },
                { "Original image", 2 }
        };
        readonly static Dictionary<string, int> overwriteModesNone = new Dictionary<string, int>{ { "None", 0 } };

        private int _overwriteMode = 0;
        [ProtoMember(18)]
        public int OverwriteMode
        {
            get => _overwriteMode;
            set => this.RaiseAndSetIfChanged(ref _overwriteMode, value);
        }

        [ProtoMember(19, IsRequired = true)]
        public bool CheckForUpdates = true;       
               
        #endregion

        #region MAINTAB_PROGRESS
        private string logs;
        public string Logs
        {
            get => logs;
            set => this.RaiseAndSetIfChanged(ref logs, value);
        }

        public SourceList<LogMessage> Log = new SourceList<LogMessage>();

        private double _progressBarValue = 0;
        public double ProgressBarValue
        {
            get => _progressBarValue;
            set => this.RaiseAndSetIfChanged(ref _progressBarValue, value);
        }

        private int _filesDone = 0;
        public int FilesDone { get { return _filesDone; } }
        public int[] IncrementDoneCounter(bool success = true)
        { 
            return new int[] { Interlocked.Increment(ref _filesDone), success ? Interlocked.Increment(ref _filesDoneSuccesfully): _filesDoneSuccesfully };            
        }
        public void ResetDoneCounter() { _filesDone = 0; _filesDoneSuccesfully = 0; }

        private int _filesDoneSuccesfully = 0;
        public int FilesDoneSuccesfully { get { return _filesDoneSuccesfully; } }

        private int _filesTotal = 0;
        public int FilesTotal
        {
            get => _filesTotal;
            set => this.RaiseAndSetIfChanged(ref _filesTotal, value);
        }

        private string _progressLabel = "0/0";
        public string ProgressLabel
        {
            get => _progressLabel;
            set => this.RaiseAndSetIfChanged(ref _progressLabel, value);
        }
        #endregion

        #region ADVANCED

        private bool _advancedUseResultSuffix = false;
        public bool AdvancedUseResultSuffix
        {
            get => _advancedUseResultSuffix;
            set => _advancedUseResultSuffix = value;
        }
        private string _advancedResultSuffix = "";
        public string AdvancedResultSuffix
        {
            get => _advancedResultSuffix;
            set => _advancedResultSuffix = value;
        }

        private bool _filterFilenameContainsEnabled = false;
        public bool FilterFilenameContainsEnabled
        {
            get => _filterFilenameContainsEnabled;
            set => _filterFilenameContainsEnabled = value;
        }
        private bool _filterFilenameNotContainsEnabled = false;
        public bool FilterFilenameNotContainsEnabled
        {
            get => _filterFilenameNotContainsEnabled;
            set => _filterFilenameNotContainsEnabled = value;
        }
        private bool _filterFilenameCaseSensitive = false;
        public bool FilterFilenameCaseSensitive
        {
            get => _filterFilenameCaseSensitive;
            set => _filterFilenameCaseSensitive = value;
        }
        private string _filterFilenameContainsPattern = "";
        public string FilterFilenameContainsPattern
        {
            get => _filterFilenameContainsPattern;
            set => _filterFilenameContainsPattern = value;
        }
        private string _filterFilenameNotContainsPattern = "";
        public string FilterFilenameNotContainsPattern
        {
            get => _filterFilenameNotContainsPattern;
            set => _filterFilenameNotContainsPattern = value;
        }

        private int _filterAlpha = 0;
        public int FilterAlpha
        {
            get => _filterAlpha;
            set => _filterAlpha = value;
        }
        private bool _filterImageResolutionEnabled = false;
        public bool FilterImageResolutionEnabled
        {
            get => _filterImageResolutionEnabled;
            set => _filterImageResolutionEnabled = value;
        }
        private bool _filterImageResolutionOr = true;
        public bool FilterImageResolutionOr
        {
            get => _filterImageResolutionOr;
            set => _filterImageResolutionOr = value;
        }
        private int _filterImageResolutionMaxWidth = 4096;
        public int FilterImageResolutionMaxWidth
        {
            get => _filterImageResolutionMaxWidth;
            set => _filterImageResolutionMaxWidth = value;
        }
        private int _filterImageResolutionMaxHeight = 4096;
        public int FilterImageResolutionMaxHeight
        {
            get => _filterImageResolutionMaxHeight;
            set => _filterImageResolutionMaxHeight = value;
        }

        public static readonly List<string> filterExtensionsList = new List<string>() {
            ".PNG", ".TGA", ".DDS", ".JPG",
            ".JPEG", ".BMP", ".TIFF", ".WEBP"};
        public List<string> FilterSelectedExtensionsList { get; set; }

        public readonly string[] postprocessNoiseFilter = new string[] {
             "None", "Enhance", "Despeckle", "Adaptive blur"};
        private int _noiseReductionType = 0;
        public int NoiseReductionType
        {
            get => _noiseReductionType;
            set => _noiseReductionType = value;
        }

        public bool ThresholdEnabled { get; set; } = false;
        private int _thresholdBlackValue = 0;
        public int ThresholdBlackValue
        {
            get => _thresholdBlackValue;
            set => _thresholdBlackValue = value;
        }
        private int _thresholdWhiteValue = 100;
        public int ThresholdWhiteValue
        {
            get => _thresholdWhiteValue;
            set => _thresholdWhiteValue = value;
        }

        public static List<double> ResizeImageScaleFactors = new List<double>() { 0.25, 0.5, 1.0, 2.0, 4.0 };
        private double _resizeImageBeforeScaleFactor = 1.0;
        public double ResizeImageBeforeScaleFactor
        {
            get => _resizeImageBeforeScaleFactor;
            set => _resizeImageBeforeScaleFactor = value;
        }
        private double _resizeImageAfterScaleFactor = 1.0;
        public double ResizeImageAfterScaleFactor
        {
            get => _resizeImageAfterScaleFactor;
            set => _resizeImageAfterScaleFactor = value;
        }

        public static Dictionary<int, string> MagickFilterTypes = new Dictionary<int, string>() {
            { (int)FilterType.Box, "Box" },
            { (int)FilterType.Catrom, "Catrom" },
            { (int)FilterType.Point, "Point" },
            { (int)FilterType.Lanczos, "Lanczos" }
        };
        private int _resizeImageBeforeFilterType = 2;
        public int ResizeImageBeforeFilterType
        {
            get => _resizeImageBeforeFilterType;
            set => _resizeImageBeforeFilterType = value;
        }
        private int _resizeImageAfterFilterType = 2;
        public int ResizeImageAfterFilterType
        {
            get => _resizeImageAfterFilterType;
            set => _resizeImageAfterFilterType = value;
        }
        #endregion
              
        [ProtoMember(26)]
        public bool SplitRGB = false;

        [ProtoMember(27)]
        public bool UseCPU = false;

        [ProtoMember(41)]
        public bool UseBasicSR = false;

        [ProtoMember(28)]
        public bool SeamlessTexture = false;
        readonly int SeamlessExpandSize = 16;        

        ImageFormatInfo selectedOutputFormat;

        int _selectedOutputFormatIndex = 0;
        [ProtoMember(29)]
        public int SelectedOutputFormatIndex
        {
            get => _selectedOutputFormatIndex;
            set
            {
                if(FormatInfos != null)
                    selectedOutputFormat = FormatInfos[value];
                this.RaiseAndSetIfChanged(ref _selectedOutputFormatIndex, value);
            }
        }
              
      
        [ProtoMember(33)]
        public bool UseDifferentModelForAlpha = false;
      
        private ModelInfo _modelForAlpha;
        public ModelInfo ModelForAlpha
        {
            get => _modelForAlpha;
            set => _modelForAlpha = value;
        }

        string _lastModelForAlphaPath;
        [ProtoMember(32)]
        public string LastModelForAlphaPath
        {
            get => _lastModelForAlphaPath;
            set
            {
                if (value != "" && !File.Exists(value))
                {
                    WriteToLog($"{value} is saved as model for alphas, but it is missing", Color.LightYellow);
                    if (ModelsItems.Count > 0)
                        value = ModelsItems[0].FullName;
                    else
                        value = "";
                }
                _lastModelForAlphaPath = value;
            }
        }
       
        #endregion

        public ReactiveCommand<FileInfo[], Unit> SplitCommand { get; }
        public ReactiveCommand<bool, bool> UpscaleCommand { get; }
        public ReactiveCommand<Unit, Unit> MergeCommand { get; }
        public ReactiveCommand<Unit, Unit> SplitUpscaleMergeCommand { get; }
                
        [ProtoMember(23)]
        public bool DarkThemeEnabled = false;       

        readonly string DirectorySeparator = @"\"; //Path.DirectorySeparatorChar

        [ProtoMember(37)]
        public ImageFormatInfo pngFormat = new ImageFormatInfo(".png")
        { CompressionFactor = 0 };
        [ProtoMember(38)]
        public ImageFormatInfo tiffFormat = new ImageFormatInfo(".tiff")
        { CompressionMethod = TiffCompressionModes[TiffCompression.None], QualityFactor = 100 };
        [ProtoMember(39)]
        public ImageFormatInfo webpFormat = new ImageFormatInfo(".webp")
        { CompressionMethod = WebpPresets[WebpPreset.Default], QualityFactor = 100};
        public ImageFormatInfo tgaFormat = new ImageFormatInfo(".tga");   
        public ImageFormatInfo ddsFormat = new ImageFormatInfo(".dds");     
        public ImageFormatInfo jpgFormat = new ImageFormatInfo(".jpg");
        public ImageFormatInfo bmpFormat = new ImageFormatInfo(".bmp");
        private List<ImageFormatInfo> _formatInfos;
        public List<ImageFormatInfo> FormatInfos
        {
            get => _formatInfos;
            set
            {
                this.RaiseAndSetIfChanged(ref _formatInfos, value);
            }
        }

        public bool GreyscaleModel = false;

        public IEU(bool isSub = false)
        {
            IsSub = isSub;
            Task splitFunc(FileInfo[] x) => Split(x);
            SplitCommand = ReactiveCommand.CreateFromTask((Func<FileInfo[], Task>)splitFunc);
            
            Task<bool> upscaleFunc(bool x) => Upscale(x);
            UpscaleCommand = ReactiveCommand.CreateFromTask((Func<bool, Task<bool>>)upscaleFunc);
          
            MergeCommand = ReactiveCommand.CreateFromTask(Merge);
          
            SplitUpscaleMergeCommand = ReactiveCommand.CreateFromTask(SplitUpscaleMerge);
           

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                DirectorySeparator = @"\";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                DirectorySeparator = @"/";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                DirectorySeparator = @":";

            WriteToLog(RuntimeInformation.OSDescription);
            WriteToLog(RuntimeInformation.FrameworkDescription);
            //WriteToLogsThreadSafe("Linux: " + RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
            //WriteToLogsThreadSafe("Windows: " + RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            //WriteToLogsThreadSafe("OSx: " + RuntimeInformation.IsOSPlatform(OSPlatform.OSX));      

            DdsFileFormatsCurrent = ddsFileFormatsColor;   

            if(!IsSub)
                ReadSettings();

            FormatInfos = new List<ImageFormatInfo>() { pngFormat, tiffFormat, webpFormat, tgaFormat, ddsFormat, jpgFormat, bmpFormat };
            selectedOutputFormat = FormatInfos[0];     
        }

        public void ReadSettings()
        {
            if (!File.Exists("settings.proto"))
                return;
            FileStream fileStream = new FileStream("settings.proto", FileMode.Open)
            {
                Position = 0
            };
            Serializer.Merge(fileStream, this);
            fileStream.Close();           
        }
        public void SaveSettings()
        {
            FileStream fileStream = new FileStream("settings.proto", FileMode.Create);
            Serializer.Serialize(fileStream, this);
            fileStream.Close();
        }

        void CreateModelTree()
        {
            List<ModelInfo> ModelsItemsTemp = new List<ModelInfo>();
            DirectoryInfo di = new DirectoryInfo(ModelsPath);
            if (!di.Exists)
            {
                WriteToLog($"{di.FullName} doesn't exist!");
                return;
            }

            foreach (DirectoryInfo d in di.GetDirectories("*", SearchOption.TopDirectoryOnly))
                foreach (FileInfo fi in d.GetFiles("*.pth", SearchOption.TopDirectoryOnly))
                    ModelsItemsTemp.Add(new ModelInfo(fi.Name, fi.FullName, d.Name));

            foreach (FileInfo fi in di.GetFiles("*.pth", SearchOption.TopDirectoryOnly))
                ModelsItemsTemp.Add(new ModelInfo(fi.Name, fi.FullName));
            ModelsItems = ModelsItemsTemp;
        }
                     

        public void WriteToLog(string text)
        {
            WriteToLog(text, Color.White);
        }
        
        public void WriteToLog(string text, Color color)
        {
            text = $"\n[{DateTime.Now}] {text}";
            WriteToLog(new LogMessage(text, color));            
        }

        public void WriteToLog(LogMessage message)
        {
            Log.Add(message);
            Logs += message.Text;
        }
               
        public void WriteToLogOpenError(FileInfo file, string exMessage)
        {
            WriteToLog("ERROR opening tile!", Color.Red);
            WriteToLog($"{exMessage}", Color.Red);
            WriteToLog($"Skipping <{file.Name}>...", Color.Red);
        }

        private void ReportProgress()
        {                
            ProgressBarValue = ((double)FilesDone / (double)FilesTotal) * 100.00;            
            ProgressLabel = $@"{FilesDone}/{FilesTotal}";
        }
            

        MagickImage LoadImage(FileInfo file)
        {
            MagickImage image = null;
            if (file.Extension.ToLower() == ".dds" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Surface surface = DdsFile.Load(file.FullName);     
                image = Helper.ConvertToMagickImage(surface);
                image.HasAlpha = DdsFile.HasTransparency(surface);
            }
            else        
                image = new MagickImage(file.FullName);            
            return image;
        }
        
        MagickImage ExpandTiledTexture(MagickImage image)
        {
            int imageHeight = image.Height, imageWidth = image.Width;
            int expandSize = SeamlessExpandSize;
            if (imageHeight <= 32 || imageWidth <= 32)
                expandSize = SeamlessExpandSize/2;

            MagickImage expandedImage = (MagickImage)image.Clone();
            MagickImage bottomEdge = (MagickImage)image.Clone();
            bottomEdge.Crop(new MagickGeometry(0, imageHeight - expandSize, imageWidth, expandSize));
            expandedImage.Page = new MagickGeometry($"+0+{expandSize}");
            bottomEdge.Page = new MagickGeometry("+0+0");
            MagickImageCollection edges = new MagickImageCollection() { bottomEdge, expandedImage };
            expandedImage = (MagickImage)edges.Mosaic();

            MagickImage topEdge = (MagickImage)image.Clone();
            topEdge.Crop(new MagickGeometry(0, 0, imageWidth, expandSize));
            topEdge.Page = new MagickGeometry($"+0+{expandedImage.Height}");
            expandedImage.Page = new MagickGeometry("+0+0");
            edges.Add(topEdge);
            edges = new MagickImageCollection() { expandedImage, topEdge };
            expandedImage = (MagickImage)edges.Mosaic();

            image = (MagickImage)expandedImage.Clone();

            MagickImage rightEdge = (MagickImage)image.Clone();
            edges = new MagickImageCollection() { rightEdge, expandedImage };
            rightEdge.Crop(new MagickGeometry(image.Width - expandSize, 0, expandSize, image.Height));
            expandedImage.Page = new MagickGeometry($"+{expandSize}+0");
            rightEdge.Page = new MagickGeometry("+0+0");
            expandedImage = (MagickImage)edges.Mosaic();

            MagickImage leftEdge = (MagickImage)image.Clone();
            edges = new MagickImageCollection() { expandedImage, leftEdge };
            leftEdge.Crop(new MagickGeometry(0, 0, expandSize, image.Height));
            leftEdge.Page = new MagickGeometry($"+{expandedImage.Width}+0");
            expandedImage.Page = new MagickGeometry($"+0+0");
            expandedImage = (MagickImage)edges.Mosaic();

            edges.Dispose();

            return expandedImage;            
        }
        
        void ImagePreprocess(MagickImage image)
        {
            if (ResizeImageBeforeScaleFactor != 1.0)
            {
                image = ResizeImage(image, ResizeImageBeforeScaleFactor, (FilterType)ResizeImageBeforeFilterType);
                //imageWidth = Convert.ToInt32(image.Width * resizeImageBeforeScaleFactor);
                //imageHeight = Convert.ToInt32(image.Height * resizeImageBeforeScaleFactor);
            }

            switch (NoiseReductionType)
            {
                case 0:
                    break;
                case 1:
                    image.Enhance();
                    break;
                case 2:
                    image.Despeckle();
                    break;
                case 3:
                    image.AdaptiveBlur();
                    break;
            }
        }
        
        void CreateTiles(FileInfo file, MagickImage inputImage, bool imageHasAlpha, MagickImage inputImageAlpha = null)
        {
            FileInfo fileAlpha = new FileInfo(file.DirectoryName + DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name) + "_alpha.png");
            string lrPathAlpha = LrPath + "_alpha";
            int imageWidth = inputImage.Width, imageHeight = inputImage.Height;
            MagickImage inputImageRed = null, inputImageGreen = null, inputImageBlue = null;

            if (SplitRGB)
            {
                if (inputImage.ColorSpace == ColorSpace.RGB || inputImage.ColorSpace == ColorSpace.sRGB)
                {
                    inputImageRed = (MagickImage)inputImage.Separate(Channels.Red).FirstOrDefault();
                    inputImageGreen = (MagickImage)inputImage.Separate(Channels.Green).FirstOrDefault();
                    inputImageBlue = (MagickImage)inputImage.Separate(Channels.Blue).FirstOrDefault();
                }
                else
                {
                    //TODO: convert to RGB
                    WriteToLog("Image is not RGB");
                    return;
                }
            }

            int[] tiles;
            if (PreciseTileResolution)
            {
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolutionWidth, MaxTileResolutionHeight);
                if(tiles[0] == 0 || tiles[1] == 0)
                {
                    WriteToLog(file.Name + " resolution is smaller than specified tile size");
                    return;
                }
            }
            else
            {
                if (imageHeight * imageWidth > MaxTileResolution)
                {                    
                    tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolution);
                    bool dimensionsAreOK = imageWidth % tiles[0] == 0 && imageHeight % tiles[1] == 0;
                    if(!dimensionsAreOK && !SeamlessTexture)
                    {                        
                        inputImage = PadImage(inputImage, tiles[0], tiles[1]);
                        imageWidth = inputImage.Width;
                        imageHeight = inputImage.Height;
                        tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolution);
                    }
                }
                else
                    tiles = new int[] { 1, 1 };
            }

            int tileWidth = imageWidth / tiles[0];
            int tileHeight = imageHeight / tiles[1];
            if (PreciseTileResolution)
            {
                tileWidth = MaxTileResolutionWidth;
                tileHeight = MaxTileResolutionHeight;
            }
            int rightOverlap, bottomOverlap;

            for (int i = 0; i < tiles[1]; i++)
            {
                for (int j = 0; j < tiles[0]; j++)
                {
                    if (i < tiles[1] - 1)
                        bottomOverlap = 1;
                    else
                        bottomOverlap = 0;
                    if (j < tiles[0] - 1)
                        rightOverlap = 1;
                    else
                        rightOverlap = 0;

                    int tileIndex = i * tiles[0] + j;
                    int xOffset = rightOverlap * OverlapSize;
                    int yOffset = bottomOverlap * OverlapSize;
                    int tile_X1 = j * tileWidth;
                    int tile_Y1 = i * tileHeight;

                    Directory.CreateDirectory($"{LrPath}{DirectorySeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}");

                    if (imageHasAlpha && !IgnoreAlpha) //crop alpha
                    {
                        MagickImage outputImageAlpha = (MagickImage)inputImageAlpha.Clone();
                        outputImageAlpha.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        if (UseDifferentModelForAlpha)
                            outputImageAlpha.Write($"{lrPathAlpha}{DirectorySeparator}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex.ToString("D2")}.png");
                        else
                            outputImageAlpha.Write($"{LrPath}{DirectorySeparator}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex.ToString("D2")}.png");
                    }
                    if (SplitRGB)
                    {
                        MagickImage outputImageRed = (MagickImage)inputImageRed.Clone();
                        outputImageRed.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        outputImageRed.Write($"{LrPath}{DirectorySeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}_R_tile-{tileIndex.ToString("D2")}.png");

                        MagickImage outputImageGreen = (MagickImage)inputImageGreen.Clone();
                        outputImageGreen.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        outputImageGreen.Write($"{LrPath}{DirectorySeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}_G_tile-{tileIndex.ToString("D2")}.png");

                        MagickImage outputImageBlue = (MagickImage)inputImageBlue.Clone();
                        outputImageBlue.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        outputImageBlue.Write($"{LrPath}{DirectorySeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}_B_tile-{tileIndex.ToString("D2")}.png");
                    }
                    else
                    {
                        MagickImage outputImage = (MagickImage)inputImage.Clone();                      
                        outputImage.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        MagickFormat format = MagickFormat.Png24;                 
                        outputImage.Write($"{LrPath}{DirectorySeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}_tile-{tileIndex.ToString("D2")}.png", format);
                    }
                }
            }
            WriteToLog($"{file.Name} DONE", Color.LightGreen);
        }
               
        void SplitTask(FileInfo file)
        {
            MagickImage image = null, inputImage = null, inputImageAlpha = null;           
            bool imageHasAlpha = false;
            
            try
            {
                image = LoadImage(file);
                imageHasAlpha = image.HasAlpha;
            }
            catch (Exception ex)
            {
                WriteToLog($"Failed to read file {file.Name}!", Color.Red);
                WriteToLog(ex.Message);
                return;
            }                      
            
            if (SeamlessTexture)
            {
                image = ExpandTiledTexture(image);              
            }

            ImagePreprocess(image);

            inputImage = (MagickImage)image.Clone();
            inputImage.HasAlpha = false;

            if (imageHasAlpha && !IgnoreAlpha)
            {              
                inputImageAlpha = (MagickImage)image.Separate(Channels.Alpha).First();
                bool isSolidWhite = inputImageAlpha.TotalColors == 1 && inputImageAlpha.Histogram().ContainsKey(new MagickColor("#FFFFFF"));
                if (IgnoreSingleColorAlphas && isSolidWhite)
                {
                    inputImageAlpha.Dispose();
                    imageHasAlpha = false;
                }
            }
            CreateTiles(file, inputImage, imageHasAlpha, inputImageAlpha);
            IncrementDoneCounter();            
            ReportProgress();
            GC.Collect();
        }

        async public Task Split(FileInfo[] inputFiles = null)
        {
            if(!IsSub)
                SaveSettings();
            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;

            DirectoryInfo inputDirectory = new DirectoryInfo(InputDirectoryPath);
            DirectoryInfo lrDirectory = new DirectoryInfo(LrPath);
            FileInfo[] inputDirectoryFiles = inputDirectory.GetFiles("*", searchOption);
            if(inputDirectoryFiles.Count() == 0)
            {
                WriteToLog("No files in input folder!", Color.Red);
                return;
            }                 

            DirectoryInfo lrAlphaDirectory = new DirectoryInfo(LrPath + "_alpha");
            if (UseDifferentModelForAlpha && !lrAlphaDirectory.Exists)
                lrAlphaDirectory.Create();
     
            lrDirectory.GetFiles("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
            WriteToLog($"'{LrPath}' is cleared", Color.LightBlue);

            if (UseDifferentModelForAlpha)
            {
                lrAlphaDirectory.GetFiles("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
                WriteToLog($"'{LrPath + "_alpha"}' is cleared", Color.LightBlue);
            }
            
            WriteToLog("Creating tiles...");

            if (inputFiles == null)
                inputFiles = inputDirectoryFiles;

            tasks = new List<Task>();
            ResetDoneCounter();            
            FilesTotal = inputFiles.Length;
                        
            if (CreateMemoryImage)
            {
                Image image = Image.Black(MaxTileResolutionWidth, MaxTileResolutionHeight);
                image.WriteToFile($"{LrPath}{DirectorySeparator}([000])000)_memory_helper_image.png");
            }           

            foreach (var file in inputFiles)
            {
                if (!file.Exists)
                    continue;

                if (!ApplyFilters(file))
                {
                    IncrementDoneCounter(false);
                    WriteToLog($"{file.Name} is filtered, skipping", Color.HotPink);
                    continue;
                }
                //SplitTask(file);
                tasks.Add(Task.Factory.StartNew(() => SplitTask(file)));
            }
            await Task.WhenAll(tasks.ToArray());
            tasks.Clear();
            WriteToLog("Finished!", Color.LightGreen);
        }

        async public Task<bool> Upscale(bool NoWindow = false)
        {
            if (!IsSub)
                SaveSettings();
            checkedModels = SelectedModelsItems;

            if (checkedModels.Count == 0)
            {
                WriteToLog("No models selected!");
                return false;
            }

            DirectoryInfo directory = new DirectoryInfo(ResultsPath);
            if (!directory.Exists)
                directory.Create();


            Process process;
            if(UseBasicSR)
                process = await BasicSR_Test(NoWindow);
            else
                process = await ESRGAN(NoWindow);          

            int processExitCode = await RunProcessAsync(process);
            if (processExitCode == -666)
                return false;
            if (processExitCode != 0)
            {
                WriteToLog("Error ocured during ESRGAN work!", Color.Red);
                return false;
            }
            WriteToLog("ESRGAN finished!", Color.LightGreen);
            return true;
        }

        bool GetTileResolution(FileInfo file, string basePath, int[] tiles, string resultSuffix, ref int tileWidth, ref int tileHeight)
        {          
            MagickImageInfo lastTile;
            try
            {
                string pathToLastTile = $"{ResultsPath + basePath}_tile-{((tiles[1] - 1) * tiles[0] + tiles[0] - 1).ToString("D2")}{resultSuffix}.png";
                if (SplitRGB)
                    if (OutputDestinationMode == 1)
                    {
                        string baseName = Path.GetFileNameWithoutExtension(file.Name);
                        pathToLastTile = $"{ResultsPath + basePath.Replace(baseName, baseName + "_R")}_tile-{((tiles[1] - 1) * tiles[0] + tiles[0] - 1).ToString("D2")}{resultSuffix}.png";
                    }
                    else
                        pathToLastTile = $"{ResultsPath + basePath}_R_tile-{((tiles[1] - 1) * tiles[0] + tiles[0] - 1).ToString("D2")}{resultSuffix}.png";

                lastTile = new MagickImageInfo(pathToLastTile);
            }
            catch (Exception ex)
            {
                WriteToLogOpenError(file, ex.Message);
                return false;
            }
            tileWidth = lastTile.Width; tileHeight = lastTile.Height;
            return true;
        }
        
        Image ExtractTiledTexture(Image imageResult, int upscaleModificator, int expandSize)
        {            
            int edgeSize = upscaleModificator * expandSize;
            Image tempImage = imageResult.Copy();
            tempImage = tempImage.ExtractArea(edgeSize, edgeSize, imageResult.Width - edgeSize * 2, imageResult.Height - edgeSize * 2).Copy();
            return tempImage;
        }
        
        bool WriteToFileVipsNative(Image imageResult, ImageFormatInfo outputFormat, string destinationPath)
        {
            try
            {
                if (outputFormat.Extension == ".png")
                    imageResult.Pngsave(destinationPath, outputFormat.CompressionFactor);        
                if (outputFormat.Extension == ".tiff")
                    imageResult.Tiffsave(destinationPath, outputFormat.CompressionMethod, outputFormat.QualityFactor);
                if (outputFormat.Extension == ".webp")
                    imageResult.Webpsave(destinationPath, lossless: true, nearLossless: true, q: outputFormat.QualityFactor, preset: outputFormat.CompressionMethod);
            }
            catch (Exception ex)
            {
                WriteToLog($"{ex.Message}");
                return false;
            }
            imageResult.Dispose();
            return true;
        }
        
        void WriteToFileDds(MagickImage finalImage, string destinationPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Surface processedSurface = Helper.ConvertToSurface(finalImage);
                FileStream fileStream = new FileStream(destinationPath, FileMode.Create);
                DdsFile.Save(
                    fileStream,
                    DdsFileFormat.DdsFileFormat,
                    DdsErrorMetric.Perceptual,
                    DdsBC7CompressionMode,
                    false,
                    ddsGenerateMipmaps,
                    ResamplingAlgorithm.Bilinear,
                    processedSurface,
                    null);
                    fileStream.Close();
            }
            else
                finalImage.Write(destinationPath, MagickFormat.Dds);      
        }
        
        void ImagePostrpocess(MagickImage finalImage)
        {
            if (ThresholdBlackValue != 0)
                finalImage.BlackThreshold(new Percentage((double)ThresholdBlackValue));
            if (ThresholdWhiteValue != 100)
                finalImage.WhiteThreshold(new Percentage((double)ThresholdWhiteValue));

            if (ResizeImageAfterScaleFactor != 1.0)
            {
                finalImage = ResizeImage(finalImage, ResizeImageAfterScaleFactor, (FilterType)ResizeImageAfterFilterType);
            }
        }
        
        Image JoinRGB(string basePath, string baseName, int tileIndex, string resultSuffix, List<FileInfo> tileFilesToDelete)
        {
            Image imageNextTileR = null, imageNextTileG = null, imageNextTileB = null;
            FileInfo tileR, tileG, tileB;
            if(OutputDestinationMode == 1)
            {                
                tileR = new FileInfo($"{ResultsPath + basePath.Replace(baseName, baseName + "_R")}_tile-{tileIndex.ToString("D2")}{resultSuffix}.png");
                tileG = new FileInfo($"{ResultsPath + basePath.Replace(baseName, baseName + "_G")}_tile-{tileIndex.ToString("D2")}{resultSuffix}.png");
                tileB = new FileInfo($"{ResultsPath + basePath.Replace(baseName, baseName + "_B")}_tile-{tileIndex.ToString("D2")}{resultSuffix}.png");
            }
            else
            {
                tileR = new FileInfo($"{ResultsPath + basePath}_R_tile-{tileIndex.ToString("D2")}{resultSuffix}.png");
                tileG = new FileInfo($"{ResultsPath + basePath}_G_tile-{tileIndex.ToString("D2")}{resultSuffix}.png");
                tileB = new FileInfo($"{ResultsPath + basePath}_B_tile-{tileIndex.ToString("D2")}{resultSuffix}.png");
            }

            imageNextTileR = Image.NewFromFile(tileR.FullName, false, Enums.Access.Sequential)[0];
            tileFilesToDelete.Add(tileR);
            imageNextTileG = Image.NewFromFile(tileG.FullName, false, Enums.Access.Sequential)[0];
            tileFilesToDelete.Add(tileG);
            imageNextTileB = Image.NewFromFile(tileB.FullName, false, Enums.Access.Sequential)[0];
            tileFilesToDelete.Add(tileB);
            return imageNextTileR.Bandjoin(imageNextTileG).Bandjoin(imageNextTileB);
        }
        
        void UseGlobalbalance(ref Image imageResult, ref bool cancelGlobalbalance, string filename)
        {            
            try
            {
                //Image hist = imageResult.HistFind();
                //bool histIsmonotonic = hist.HistIsmonotonic();                
                //if (!cancelGlobalbalance && histIsmonotonic)
                //{
                //    cancelGlobalbalance = true;
                //    WriteToLogsThreadSafe($"{filename} is monotonic, globalbalance is canceled", Color.LightYellow);
                //}

                if (!cancelGlobalbalance)
                {
                    //Image tempImage = imageResult.CopyMemory();
                    ////imageResult = tempImage.Globalbalance().Copy(interpretation: "srgb").Cast("uchar");
                    //tempImage = tempImage.Globalbalance();
                    //imageResult = tempImage.CopyMemory();
                    imageResult = imageResult.Globalbalance();
                }
            }
            catch(Exception ex) 
            {
                cancelGlobalbalance = true;
                if (ex.HResult == -2146233088)
                    WriteToLog($"{filename}: globabalance is canceled", Color.LightYellow);
                else
                    WriteToLog($"{filename}: {ex.Message}", Color.Red);
            }
        }
        
        void JoinTiles(ref Image imageRow, Image imageNextTile, bool useMosaic, string direction, int dx, int dy)
        {            
            if (useMosaic)
                imageRow = imageRow.Mosaic(imageNextTile, direction, -dx, -dy, 0, 0);
            else
                imageRow = imageRow.Merge(imageNextTile, direction, dx, dy);
        }
        
        Image MergeTiles(FileInfo file, int[] tiles, string basePath, string basePathAlpha, string resultSuffix, List<FileInfo> tileFilesToDelete, bool imageHasAlpha)
        {
            bool useMosaic = false;
            bool alphaReadError = false, cancelRgbGlobalbalance = false, cancelAlphaGlobalbalance = false;
            Image imageResult = null, imageAlphaResult = null;
            Image imageRow = null;
            Image imageAlphaRow = null;
            int tileWidth = 0, tileHeight = 0;
            GetTileResolution(file, basePath, tiles, resultSuffix, ref tileWidth, ref tileHeight);

            for (int i = 0; i < tiles[1]; i++)
            {
                for (int j = 0; j < tiles[0]; j++)
                {
                    int tileIndex = i * tiles[0] + j;

                    Image imageNextTile = null, imageAlphaNextTile = null;                 
                    try
                    {
                        if (SplitRGB)                        
                            imageNextTile = JoinRGB(basePath, Path.GetFileNameWithoutExtension(file.Name), tileIndex, resultSuffix, tileFilesToDelete);                        
                        else
                        {
                            imageNextTile = Image.NewFromFile($"{ResultsPath + basePath}_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false, Enums.Access.Sequential);
                            tileFilesToDelete.Add(new FileInfo($"{ResultsPath + basePath}_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));
                        }
                    }
                    catch (VipsException ex)
                    {
                        WriteToLogOpenError(file, ex.Message);
                        return null;
                    }

                    if (imageHasAlpha && !IgnoreAlpha && !alphaReadError)
                    {
                        try
                        {
                            imageAlphaNextTile = Image.NewFromFile($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false, Enums.Access.Sequential);
                            tileFilesToDelete.Add(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));

                            if (j == 0)
                            {
                                imageAlphaRow = imageAlphaNextTile;//.CopyMemory();
                            }
                            else
                            {
                                JoinTiles(ref imageAlphaRow, imageAlphaNextTile, useMosaic, Enums.Direction.Horizontal, -tileWidth * (j), 0);
                                if (BalanceAlphas)
                                    UseGlobalbalance(ref imageAlphaRow, ref cancelAlphaGlobalbalance, $"{file.Name} alpha");
                            }
                        }
                        catch (VipsException ex)
                        {
                            alphaReadError = true;
                            WriteToLogOpenError(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"), ex.Message);
                        }
                    }

                    if (j == 0)
                    {
                        imageRow = imageNextTile;//.CopyMemory();
                        continue;
                    }
                    else                    
                        JoinTiles(ref imageRow, imageNextTile, useMosaic, Enums.Direction.Horizontal, -tileWidth * j, 0); 

                    UseGlobalbalance(ref imageRow, ref cancelRgbGlobalbalance, $"{file.Name}");                   
                    imageNextTile.Dispose();
                }

                if (i == 0)
                {
                    imageResult = imageRow;//.Copy();
                    if (imageHasAlpha && !IgnoreAlpha && !alphaReadError)
                        imageAlphaResult = imageAlphaRow.Copy();
                }
                else
                {
                    JoinTiles(ref imageResult, imageRow, useMosaic, Enums.Direction.Vertical, 0, -tileHeight * i);   
                    UseGlobalbalance(ref imageResult, ref cancelRgbGlobalbalance, file.Name);                  

                    if (imageHasAlpha && !IgnoreAlpha && !alphaReadError)
                    {
                        JoinTiles(ref imageAlphaResult, imageAlphaRow, useMosaic, Enums.Direction.Vertical, 0, -tileHeight * i);
                        if(BalanceAlphas)
                            UseGlobalbalance(ref imageAlphaResult, ref cancelAlphaGlobalbalance, $"{file.Name} alpha");                      
                    }
                }
            }

            if (tiles[1] > 1) // more than 1 row
            {
                imageRow.Dispose(); //dispose of previous
                imageAlphaRow?.Dispose();
            }

            if (imageHasAlpha && !IgnoreAlpha && !alphaReadError)
            {
                imageResult = imageResult.Bandjoin(imageAlphaResult);
                imageResult = imageResult.Copy(interpretation: "srgb").Cast("uchar");
                imageAlphaResult.Dispose();
            }
            return imageResult;
        }

        internal void MergeTask(FileInfo file, string basePath, int outputMode, string outputFilename = "")
        {           
            #region IMAGE READ
            
            string basePathAlpha = basePath;
            string resultSuffix = "";

            if (AdvancedUseResultSuffix)
                resultSuffix = AdvancedResultSuffix;

            if (outputMode == 1) // grab alpha tiles from different folder
            {
                string fileName = Path.GetFileNameWithoutExtension(file.Name);
                basePathAlpha = basePathAlpha.Replace(
                    $"{DirectorySeparator}images{DirectorySeparator}{fileName}",
                    $"{DirectorySeparator}images{DirectorySeparator}{fileName}_alpha");
            }

            bool imageHasAlpha = false;
            int imageWidth = 0, imageHeight = 0;
            MagickImage image;
            try
            {
                image = LoadImage(file);
                imageWidth = image.Width;
                imageHeight = image.Height;
                imageHasAlpha = image.HasAlpha;
            }
            catch
            {
                WriteToLog($"Failed to read file {file.Name}!", Color.Red);
                return;
            }

            int[] tiles;

            int expandSize = SeamlessExpandSize;
            if (image.Height <= 32 || image.Width <= 32)
                expandSize = SeamlessExpandSize/2;

            if (SeamlessTexture)
            {
                imageWidth += expandSize * 2;
                imageHeight += expandSize * 2;
            }

            if (imageHeight * imageWidth > MaxTileResolution)
            {
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolution);
                bool dimensionsAreOK = imageWidth % tiles[0] == 0 && imageHeight % tiles[1] == 0;
                if (!dimensionsAreOK && !SeamlessTexture)
                {
                    int[] newDimensions = Helper.GetGoodDimensions(image.Width, image.Height, tiles[0], tiles[1]);                   
                    tiles = Helper.GetTilesSize(newDimensions[0], newDimensions[1], MaxTileResolution);
                }
            }
            else
                tiles = new int[] { 1, 1 };
                       
            List<FileInfo> tileFilesToDelete = new List<FileInfo>();
            #endregion

            Image imageResult = MergeTiles(file, tiles, basePath, basePathAlpha, resultSuffix, tileFilesToDelete, imageHasAlpha);
            if (imageResult == null)
                return;

            #region SAVE IMAGE

            ImageFormatInfo outputFormat;
            if (UseOriginalImageFormat)
                outputFormat = FormatInfos.Where(x => x.Extension.Equals(file.Extension, StringComparison.InvariantCultureIgnoreCase)).First(); //hack, may be bad
            else
                outputFormat = selectedOutputFormat;
            if (outputFormat == null)
                outputFormat = new ImageFormatInfo(file.Extension);

            string destinationPath = OutputDirectoryPath + basePath + outputFormat;

            if(outputFilename != "")
                destinationPath = OutputDirectoryPath + basePath.Replace(Path.GetFileNameWithoutExtension(file.Name), outputFilename) + outputFormat;

            if (OutputDestinationMode == 3)            
                destinationPath = $"{OutputDirectoryPath}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}" +
                    $"{Path.GetFileNameWithoutExtension(file.Name)}{outputFormat}";

            if (SeamlessTexture)
            {
                int upscaleModificator = imageResult.Width / imageWidth;
                imageResult = ExtractTiledTexture(imageResult, upscaleModificator, expandSize);
            }
            else
            if (imageResult.Width % image.Width != 0 || imageResult.Height % image.Height != 0) // result image dimensions are wrong
            {
                int upscaleModificator = imageResult.Width / imageWidth;
                imageResult = imageResult.Crop(0, 0, image.Width * upscaleModificator, image.Height * upscaleModificator);
            }

            if (                
                outputFormat.VipsNative &&               
                ThresholdBlackValue == 0 &&
                ThresholdWhiteValue == 100 &&
                ResizeImageAfterScaleFactor == 1.0) //no need to convert to MagickImage, save fast with vips
            {                
                if (OverwriteMode == 2)
                {
                    file.Delete();
                    destinationPath = Path.GetDirectoryName(file.FullName) + Path.GetFileNameWithoutExtension(file.FullName) + outputFormat;
                }
                else
                {
                    string a = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(a))
                        Directory.CreateDirectory(a);
                }              

                if (!WriteToFileVipsNative(imageResult, outputFormat, destinationPath))
                    return;
                IncrementDoneCounter();
                ReportProgress();
                WriteToLog($"<{file.Name}> DONE", Color.LightGreen);

                if (DeleteResults)
                    tileFilesToDelete.ForEach(x => x.Delete());
                GC.Collect();
                return;
            }

            byte[] imageBuffer = imageResult.PngsaveBuffer(compression: 0);
        
            var readSettings = new MagickReadSettings()
            {
                Format = MagickFormat.Png00,
                Compression = CompressionMethod.NoCompression,
                Width = imageResult.Width,
                Height = imageResult.Height
            };
            MagickImage finalImage = new MagickImage(imageBuffer, readSettings);

            ImagePostrpocess(finalImage);
            
            if (OverwriteMode == 2)
            {
                file.Delete();
                destinationPath = $"{OutputDirectoryPath}{DirectorySeparator}" +
                    $"{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{file.Name}";
            }
            else
            {
                string a = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(a))
                    Directory.CreateDirectory(a);
            }

            if (outputFormat.Extension == ".dds")
                WriteToFileDds(finalImage, destinationPath);
            else
                finalImage.Write(destinationPath);
            
            finalImage.Dispose();
            ReportProgress();
            WriteToLog($"{file.Name} DONE", Color.LightGreen);
            if (DeleteResults)
                tileFilesToDelete.ForEach(x => x.Delete());
            GC.Collect();
            return;
            #endregion
        }
        async public Task Merge()
        {
            if (!IsSub)
                SaveSettings();
            WriteToLog("Merging tiles...");

            tasks = new List<Task>();
            DirectoryInfo di = new DirectoryInfo(InputDirectoryPath);

            ResetDoneCounter();
            FilesTotal = 0;           

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;         

            foreach (var file in di.GetFiles("*", searchOption))
            {
                if (!file.Exists)
                    continue;

                if (!ApplyFilters(file))
                {
                    //ReportProgressThreadSafe(false);
                    WriteToLog($"{file.Name} is filtered, skipping", Color.HotPink);
                    continue;
                }

                if (OutputDestinationMode == 1)
                {
                    DirectoryInfo imagesFolder;

                    if (SplitRGB)
                    {                       
                        imagesFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}images{DirectorySeparator}" + Path.GetFileNameWithoutExtension(file.Name) + "_R");

                        foreach (var image in imagesFolder.GetFiles("*", SearchOption.TopDirectoryOnly).
                            Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_R" + "_tile-00")))
                        {
                            string basePath = $"{DirectorySeparator}images{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}{DirectorySeparator}" +
                                $"{Path.GetFileNameWithoutExtension(image.Name).Replace("_R","")}";
                            basePath = basePath.Remove(basePath.Length - 8, 8);
                            FilesTotal++;
                            //MergeTask(file, s, 1);
                            tasks.Add(Task.Run(() => MergeTask(file, basePath, OutputDestinationMode)));
                        }                        
                    }
                    else
                    {
                        imagesFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}images{DirectorySeparator}" + Path.GetFileNameWithoutExtension(file.Name));
                        if (!imagesFolder.Exists || imagesFolder.GetFiles().Length == 0)
                        {
                            WriteToLogOpenError(file, "Can't find tiles in result folder for " + file.Name);
                            return;
                        }

                        foreach (var image in imagesFolder.GetFiles("*", SearchOption.TopDirectoryOnly).Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_tile-00")))
                        {
                            string basePath = $"{DirectorySeparator}images{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}{DirectorySeparator}{Path.GetFileNameWithoutExtension(image.Name)}";                            
                            basePath = basePath.Remove(basePath.Length - 8, 8); //remove "_tile-00"
                            FilesTotal++;
                            //MergeTask(file, s, 1);
                            tasks.Add(Task.Run(() => MergeTask(file, basePath, OutputDestinationMode)));
                        }
                    }
                    continue;
                }
                if (OutputDestinationMode == 2)
                {
                    DirectoryInfo modelsFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}models{DirectorySeparator}");
                    if (!modelsFolder.Exists)
                    {
                        WriteToLog(modelsFolder.FullName + " doesn't exist!", Color.Red);
                        return;
                    }

                    foreach (var modelFolder in modelsFolder.GetDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        foreach (var image in modelFolder.GetFiles("*", SearchOption.TopDirectoryOnly).Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_tile-")))
                        {
                            FilesTotal++; ;
                            //MergeTask(file, $"\\models\\{modelFolder.Name}\\{Path.GetFileNameWithoutExtension(file.Name)}", 2);                            
                            tasks.Add(Task.Run(() =>
                            MergeTask(
                                file,
                                $"{DirectorySeparator}models{DirectorySeparator}{modelFolder.Name}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}",
                                OutputDestinationMode)));
                            break;
                        }
                    }
                    continue;
                }
                if (OutputDestinationMode == 3)
                {
                    FilesTotal = di.GetFiles("*", searchOption).Length;
                    //MergeTask(file, "\\" + Path.GetFileNameWithoutExtension(file.Name), 3);                     
                    tasks.Add(Task.Run(() => MergeTask(
                        file, 
                        file.FullName.Replace(InputDirectoryPath, "").Replace(file.Name, Path.GetFileNameWithoutExtension(file.Name)),
                        OutputDestinationMode)));
                    continue;
                }
                FilesTotal = di.GetFiles().Length;
                //MergeTask(file, DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name), 0);
                tasks.Add(Task.Run(() => MergeTask(file, DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name), 0)));
            }
            await Task.WhenAll(tasks.ToArray());
            tasks.Clear();
            GC.Collect();
            WriteToLog("Finished!", Color.LightGreen);
            string pathToMergedFiles = OutputDirectoryPath;
            if (OutputDestinationMode == 1)
                pathToMergedFiles += $"{DirectorySeparator}images";
            if (OutputDestinationMode == 2)
                pathToMergedFiles += $"{DirectorySeparator}models";
        }

        async public Task SplitUpscaleMerge()
        {
            checkedModels = SelectedModelsItems;
            if (checkedModels.Count == 0)
            {
                WriteToLog("No models selected!");
                return;
            }
            await Split();
            bool upscaleSuccess = await Upscale();
            if (upscaleSuccess)
                await Merge();
        }
        
        bool ApplyFilters(FileInfo file)
        {
            bool alphaFilter = true, filenameFilter = true, sizeFilter, resultsFilter = true;
            string[] patternsContains = FilterFilenameContainsPattern.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            string[] patternsNotContains = FilterFilenameNotContainsPattern.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            string filename = file.Name;
            if (!FilterFilenameCaseSensitive)
            {
                filename = filename.ToUpper();
                for (int i = 0; i < patternsContains.Length; i++)
                    patternsContains[i] = patternsContains[i].ToUpper();
            }
            if (FilterFilenameContainsEnabled)
            {
                bool matchPattern = false;
                foreach (string pattern in patternsContains)
                    matchPattern = matchPattern || filename.Contains(pattern);

                filenameFilter = filenameFilter && matchPattern;
            }
            if (FilterFilenameNotContainsEnabled)
            {
                bool matchPattern = false;
                foreach (string pattern in patternsNotContains)
                    matchPattern = matchPattern || !filename.Contains(pattern);

                filenameFilter = filenameFilter && matchPattern;
            }
            if (!filenameFilter) return false;

            if (FilterSelectedExtensionsList?.Count() > 0 &&
                !FilterSelectedExtensionsList.Contains(file.Extension.ToUpper()))
                return false;

            if (FilterAlpha != 0 || FilterImageResolutionEnabled) //need to load Magick image
            {
                try
                {
                    using (MagickImage image = new MagickImage(file.FullName))
                    {
                        if (FilterAlpha != 0)
                        {
                            switch (FilterAlpha) // switch alpha filter type
                            {
                                case 1:
                                    alphaFilter = image.HasAlpha;
                                    break;
                                case 2:
                                    alphaFilter = !image.HasAlpha;
                                    break;
                            }

                            if (!alphaFilter) return false;
                        }

                        if (FilterImageResolutionEnabled)
                        {
                            if (!FilterImageResolutionOr) // OR
                                sizeFilter = image.Width <= FilterImageResolutionMaxWidth || image.Height <= FilterImageResolutionMaxHeight;
                            else // AND
                                sizeFilter = image.Width <= FilterImageResolutionMaxWidth && image.Height <= FilterImageResolutionMaxHeight;
                            if (!sizeFilter) return false;
                        }
                    }
                }
                catch
                {
                    if (file.Extension.ToLower() == ".dds")
                    {
                        using (Surface image = DdsFile.Load(file.FullName))
                        {
                            if (FilterAlpha != 0)
                            {
                                switch (FilterAlpha) // switch alpha filter type
                                {
                                    case 1:
                                        alphaFilter = DdsFile.HasTransparency(image);
                                        break;
                                    case 2:
                                        alphaFilter = !DdsFile.HasTransparency(image);
                                        break;
                                }
                                if (!alphaFilter) return false;
                            }

                            if (!FilterImageResolutionEnabled)
                            {
                                if (!FilterImageResolutionOr) // OR
                                    sizeFilter = image.Width <= FilterImageResolutionMaxWidth || image.Height <= FilterImageResolutionMaxHeight;
                                else // AND
                                    sizeFilter = image.Width <= FilterImageResolutionMaxWidth && image.Height <= FilterImageResolutionMaxHeight;
                                if (!sizeFilter) return false;
                            }
                        }
                    }
                }
            }
            return true;
        }
        
        MagickImage ResizeImage(MagickImage image, double resizeFactor, FilterType filterType)
        {
            MagickImage newImage = image.Clone() as MagickImage;
            //image.VirtualPixelMethod = VirtualPixelMethod.
            //image.Interpolate = interpolateMethod;
            newImage.FilterType = filterType;
            //image.Sharpen();
            //image.Scale(new Percentage(resizeFactor));        
            newImage.Resize((int)(resizeFactor * image.Width), (int)(resizeFactor * image.Height));
            return newImage;
        }

        MagickImage PadImage(MagickImage image, int x, int y)
        {
            MagickImage result = (MagickImage)image.Clone();
            int[] newDimensions = Helper.GetGoodDimensions(image.Width, image.Height, x, y);
            result.Extent(newDimensions[0], newDimensions[1]);          
            return result;
        }

        void WriteTestScriptToDisk()
        {
            string script = "";

            string archName = "ESRGAN";
            if (UseBasicSR) archName = "BasicSR";
                        
            if (OutputDestinationMode == 0)
                script = EmbeddedResource.GetFileText($"ImageEnhancingUtility.Core.Scripts.{archName}.upscaleDefault.py");            
            if (OutputDestinationMode == 1)
                script = EmbeddedResource.GetFileText($"ImageEnhancingUtility.Core.Scripts.{archName}.upscaleFolderForImage.py");
            if (OutputDestinationMode == 2)
                script = EmbeddedResource.GetFileText($"ImageEnhancingUtility.Core.Scripts.{archName}.upscaleFolderForModel.py");
            if (OutputDestinationMode == 3)
                script = EmbeddedResource.GetFileText($"ImageEnhancingUtility.Core.Scripts.{archName}.upscaleFolderStructure.py");
            if (GreyscaleModel)
                script = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.Scripts.ESRGAN.upscaleGrayscale.py");
            
            string scriptPath = EsrganPath + $"{DirectorySeparator}IEU_test.py";
            if(UseBasicSR) scriptPath = EsrganPath + $"{DirectorySeparator}codes{DirectorySeparator}IEU_test.py";
            File.WriteAllText(scriptPath, script);
        }
        
        #region PYTHON PROCESS STUFF

        async Task<bool> DetectModelUpscaleFactor(ModelInfo checkedModel)
        {
            int processExitCodePthReader = -666;
            WriteToLog($"Detecting {checkedModel.Name} upscale size...");

            using (Process pthReaderProcess = PthReader(checkedModel.FullName))
                processExitCodePthReader = await RunProcessAsync(pthReaderProcess);
            
            if (processExitCodePthReader != 0)
            {
                WriteToLog($"Failed to detect {checkedModel.Name} upscale size!", Color.Red);
                return false;
            }
            WriteToLog($"{checkedModel.Name} upscale size is {hotModelUpscaleSize}", Color.LightGreen);
            checkedModel.UpscaleFactor = hotModelUpscaleSize;
            Helper.RenameModelFile(checkedModel, checkedModel.UpscaleFactor);
            WriteToLog($"Changed model filename to {checkedModel.Name}", Color.LightBlue);
            CreateModelTree();
            return true;
        }

        async Task<Process> ESRGAN(bool NoWindow)
        {
            if (checkedModels.Count > 1 && UseDifferentModelForAlpha)
            {
                WriteToLog("Only single model must be selected when using different model for alpha");
                return null;
            }

            Process process = new Process();

            string upscaleSizePattern = "(?:_?[1|2|4|8|16]x_)|(?:_x[1|2|4|8|16]_?)|(?:_[1|2|4|8|16]x_?)|(?:_?x[1|2|4|8|16]_)";

            process.StartInfo.Arguments = $"{EsrganPath}";
            bool noValidModel = true;
            string torchDevice = UseCPU ? "cpu" : "cuda";
            int upscaleMultiplayer = 0;
            string resultsPath = ResultsPath;
            if (OverwriteMode == 1)
                resultsPath = LrPath;

            foreach (ModelInfo checkedModel in checkedModels)
            {
                var regResult = Regex.Match(checkedModel.Name.ToLower(), upscaleSizePattern);
                if (regResult.Success && regResult.Groups.Count == 1)
                {
                    upscaleMultiplayer = int.Parse(regResult.Value.Replace("x", "").Replace("_", ""));
                    noValidModel = false;
                }
                else
                {
                    if (!await DetectModelUpscaleFactor(checkedModel))
                        continue;
                    upscaleMultiplayer = checkedModel.UpscaleFactor;
                    noValidModel = false;
                }
                process.StartInfo.Arguments += $" & python IEU_test.py \"{checkedModel.FullName}\" {upscaleMultiplayer} {torchDevice} \"{LrPath + $"{DirectorySeparator}*"}\" \"{resultsPath}\"";
            }

            if (UseDifferentModelForAlpha)
            {   //detect upsacle factor for alpha model
                bool validModelAlpha = false;
                int upscaleMultiplayerAlpha = 0;
                var regResultAlpha = Regex.Match(ModelForAlpha.Name.ToLower(), upscaleSizePattern);
                if (regResultAlpha.Success && regResultAlpha.Groups.Count == 1)
                {
                    upscaleMultiplayerAlpha = int.Parse(regResultAlpha.Value.Replace("x", "").Replace("_", ""));
                    validModelAlpha = true;
                }
                else
                {
                    if (await DetectModelUpscaleFactor(ModelForAlpha))
                    {
                        upscaleMultiplayerAlpha = ModelForAlpha.UpscaleFactor;
                        validModelAlpha = true;
                    }
                }
                if (upscaleMultiplayer != upscaleMultiplayerAlpha)
                {
                    WriteToLog("Upscale size for rgb model and alpha model must be the same");
                    return null;
                }

                if (validModelAlpha)
                    process.StartInfo.Arguments += $" & python IEU_test.py \"{ModelForAlpha.FullName}\" {upscaleMultiplayerAlpha} {torchDevice} \"{LrPath + $"_alpha{DirectorySeparator}*"}\" \"{resultsPath}\"";
            }
            if (noValidModel)
            {
                WriteToLog("Can't start ESRGAN: no selected models with known upscale size");
                return null;
            }

            WriteTestScriptToDisk();

            process.ErrorDataReceived += SortOutputHandler;
            process.OutputDataReceived += SortOutputHandler;
            process.StartInfo.CreateNoWindow = NoWindow;

            if (!Directory.Exists(LrPath))
            {
                WriteToLog(LrPath + " doen't exist!");
                return null;
            }
            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if(OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;
            FilesTotal = Directory.GetFiles(LrPath, "*", searchOption).Count() * checkedModels.Count;
            if (UseDifferentModelForAlpha)
                FilesTotal += Directory.GetFiles(LrPath + "_alpha").Count();

            ResetDoneCounter();

            WriteToLog("Starting ESRGAN...");
            return process;
        }
        
        async Task<Process> BasicSR_Test(bool NoWindow)
        {
            if (checkedModels.Count > 1 && UseDifferentModelForAlpha)
            {
                WriteToLog("Only single model must be selected when using different model for alpha");
                return null;
            }

            Process process = new Process();
            string upscaleSizePattern = "(?:_?[1|2|4|8|16]x_)|(?:_x[1|2|4|8|16]_?)|(?:_[1|2|4|8|16]x_?)|(?:_?x[1|2|4|8|16]_)";

            process.StartInfo.Arguments = $"{EsrganPath}";
            bool noValidModel = true;            
            int upscaleMultiplayer = 0;

            List<TestConfig> configs = new List<TestConfig>();

            foreach (ModelInfo checkedModel in checkedModels)
            {
                TestConfig config = new TestConfig(checkedModel.FullName);

                var regResult = Regex.Match(checkedModel.Name.ToLower(), upscaleSizePattern);
                if (regResult.Success && regResult.Groups.Count == 1)
                {
                    upscaleMultiplayer = int.Parse(regResult.Value.Replace("x", "").Replace("_", ""));
                    noValidModel = false;
                }
                else
                {
                    if (!await DetectModelUpscaleFactor(checkedModel))
                        continue;
                    upscaleMultiplayer = checkedModel.UpscaleFactor;
                    noValidModel = false;
                }
                
                config.Scale = upscaleMultiplayer;
                if (UseCPU)
                    config.GpuIds = null;
                TestDataset dataset = new TestDataset() { DatarootLR = LrPath, DatarootHR = ResultsPath };
                config.Datasets.Test = dataset;
                config.Path.Root = EsrganPath;
                configs.Add(config);
            }

            if (UseDifferentModelForAlpha)
            {
                TestConfig configAlpha = new TestConfig(ModelForAlpha.FullName);

                //detect upsacle factor for alpha model                
                bool validModelAlpha = false;
                int upscaleMultiplayerAlpha = 0;               
                var regResultAlpha = Regex.Match(ModelForAlpha.Name.ToLower(), upscaleSizePattern);
                if (regResultAlpha.Success && regResultAlpha.Groups.Count == 1)
                {
                    upscaleMultiplayerAlpha = int.Parse(regResultAlpha.Value.Replace("x", "").Replace("_", ""));
                    validModelAlpha = true;
                }
                else
                {
                    if (await DetectModelUpscaleFactor(ModelForAlpha))
                    {
                        upscaleMultiplayerAlpha = ModelForAlpha.UpscaleFactor;
                        validModelAlpha = true;
                    }
                }
                if (upscaleMultiplayer != upscaleMultiplayerAlpha)
                {
                    WriteToLog("Upscale size for rgb model and alpha model must be the same");
                    return null;
                }
                configAlpha.Scale = upscaleMultiplayerAlpha;
                if (UseCPU)
                    configAlpha.GpuIds = null;
                TestDataset dataset = new TestDataset() { DatarootLR = LrPath + $"_alpha{DirectorySeparator}*", DatarootHR = ResultsPath };
                configAlpha.Datasets.Test = dataset;
                if (validModelAlpha)
                    configs.Add(configAlpha);
            }
            if (noValidModel)
            {
                WriteToLog("Can't start BasicSR: no selected models with known upscale size");
                return null;
            }
            for(int i = 0; i < configs.Count; i++)
            {
                configs[i].SaveConfig($"testConfig_{i}", $"{EsrganPath}{DirectorySeparator}IEU_TestConfigs");
                process.StartInfo.Arguments += $" & python codes{DirectorySeparator}IEU_test.py -opt IEU_TestConfigs{DirectorySeparator}testConfig_{i}.json";
            }                        

            process.ErrorDataReceived += SortOutputHandler;
            process.OutputDataReceived += SortOutputHandler;
            process.StartInfo.CreateNoWindow = NoWindow;

            if (!Directory.Exists(LrPath))
            {
                WriteToLog(LrPath + " doen't exist!");
                return null;
            }
            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;
            FilesTotal = Directory.GetFiles(LrPath, "*", searchOption).Count() * checkedModels.Count;
            if (UseDifferentModelForAlpha)
                FilesTotal += Directory.GetFiles(LrPath + "_alpha", "*", searchOption).Count();

            ResetDoneCounter();

            WriteTestScriptToDisk();

            WriteToLog("Starting BasicSR...");
            return process;
        }

        Process PthReader(string modelPath)
       {
            Process process = new Process();
            process.StartInfo.Arguments = $"{Helper.GetApplicationRoot()}";
            process.StartInfo.Arguments += $" & python pthReader.py -p \"{modelPath}\"";
            process.StartInfo.CreateNoWindow = true;

            process.ErrorDataReceived += SortOutputHandler;
            process.OutputDataReceived += SortOutputHandlerPthReader;

            return process;
        }

        public Task<int> RunProcessAsync(Process process)
        {
            var tcs = new TaskCompletionSource<int>();
            if (process == null) // something goes wrong
            {
                tcs.SetResult(-666);
                return tcs.Task;
            }

            process.StartInfo.RedirectStandardInput = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;           
            process.EnableRaisingEvents = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"cd {process.StartInfo.Arguments.Replace("\"", "\\\"").Replace("&", "&&")}\"";
                WriteToLog(process.StartInfo.Arguments);
            }
            else
            {
                process.StartInfo.FileName = "cmd";
                process.StartInfo.Arguments = "/C cd /d " + process.StartInfo.Arguments;
            }

            process.Exited += (sender, args) =>
            {
                tcs.SetResult(process.ExitCode);
                process.OutputDataReceived -= SortOutputHandler;
                process.OutputDataReceived -= SortOutputHandlerPthReader;
                process.ErrorDataReceived -= SortOutputHandler;
                process.Dispose();
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }

        public async Task<bool> CreateInterpolatedModel(string a, string b, double alpha, string outputName="")
        {
            if (alpha <= 0.0 || alpha >= 1.0)
            {
                WriteToLog("Alpha should be between 0.0 and 1.0");
                WriteToLog($"Current value is: {alpha}");
                return false;
            }

            string outputPath;
            if (outputName != "")
                outputPath = $"{ModelsPath}{DirectorySeparator}{outputName}";
            else
                outputPath = $"{ModelsPath}{DirectorySeparator}{Path.GetFileNameWithoutExtension(a)}_{Path.GetFileNameWithoutExtension(b)}_interp_{alpha.ToString().Replace(",", "")}.pth";

            string script = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.Scripts.interpModels.py");
            File.WriteAllText(EsrganPath + $"{DirectorySeparator}interpModels.py", script);

            using (Process process = new Process())
            {
                process.StartInfo.Arguments = $"{EsrganPath}";
                process.StartInfo.Arguments += $" & python interpModels.py \"{a}\" \"{b}\" {alpha.ToString().Replace(",", ".")} \"{outputPath}\"";
                process.ErrorDataReceived += SortOutputHandler;
                process.OutputDataReceived += SortOutputHandler;
                int code = await RunProcessAsync(process);
                if (code == 0)
                {
                    WriteToLog("Finished interpolating!");
                    CreateModelTree();
                }
            }
            return true;

        }

        private IEU previewIEU;

        async public Task<System.Drawing.Bitmap> CreatePreview(System.Drawing.Bitmap original, string modelPath)
        {            
            string previewDirPath = $"{EsrganPath}{DirectorySeparator}IEU_preview";                
            string previewResultsDirPath = previewDirPath + $"{DirectorySeparator}results";           
            string previewLrDirPath = previewDirPath + $"{DirectorySeparator}LR";          
            string previewInputDirPath = previewDirPath + $"{DirectorySeparator}input";

            List<DirectoryInfo> previewFolders = new List<DirectoryInfo>() { 
                new DirectoryInfo(previewDirPath),
                new DirectoryInfo(previewResultsDirPath),
                new DirectoryInfo(previewLrDirPath),
                new DirectoryInfo(previewInputDirPath) };

            foreach(var folder in previewFolders)
            {
                if (!folder.Exists)
                    folder.Create();
                else
                    folder.GetFiles("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
            }

            FileInfo previewOriginal = new FileInfo(previewInputDirPath + $"{DirectorySeparator}preview.png");
            FileInfo preview = new FileInfo(previewDirPath + $"{DirectorySeparator}preview.png");
            original.Save(previewOriginal.FullName, ImageFormat.Png);

            if (previewIEU == null)
                previewIEU = new IEU(true);       
            
            previewIEU.EsrganPath = EsrganPath;
            previewIEU.LrPath = previewLrDirPath;
            previewIEU.InputDirectoryPath = previewInputDirPath;
            previewIEU.ResultsPath = previewResultsDirPath;
            previewIEU.OutputDirectoryPath = previewDirPath;
            previewIEU.MaxTileResolution = MaxTileResolution;
            previewIEU.OverlapSize = OverlapSize;
            previewIEU.IgnoreAlpha = IgnoreAlpha;
            previewIEU.OverwriteMode = 0;
            previewIEU.OutputDestinationMode = 0;
            previewIEU.UseCPU = UseCPU;

            await previewIEU.Split(new FileInfo[] { previewOriginal });
            ModelInfo previewModelInfo = new ModelInfo(Path.GetFileNameWithoutExtension(modelPath), modelPath);
            previewIEU.SelectedModelsItems = new List<ModelInfo>() { previewModelInfo };
            bool success = await previewIEU.Upscale(true);
            if (!success)
            {
                File.WriteAllText(previewDirPath + $"{DirectorySeparator}log.txt", previewIEU.Logs);
                return null;
            }
            await previewIEU.Merge();            

            System.Drawing.Bitmap result;
            using (var fs = new FileStream(preview.FullName, FileMode.Open))
            {
                using (System.Drawing.Bitmap test = new System.Drawing.Bitmap(fs))
                {
                    result = test.Clone() as System.Drawing.Bitmap;
                }
            }
            return result;
        }

        private void SortOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.

            if (!string.IsNullOrEmpty(outLine.Data)
                && outLine.Data != $"{EsrganPath}>"
                && outLine.Data != "^C"
                && !outLine.Data.Contains("UserWarning")
                && !outLine.Data.Contains("nn."))
            {
                if (Regex.IsMatch(outLine.Data, "^[0-9]+ .*$"))
                {
                    IncrementDoneCounter();
                    ReportProgress();
                    WriteToLog(outLine.Data, Color.LightGreen);
                }
                else
                    WriteToLog(outLine.Data);
            }
        }

        private void SortOutputHandlerPthReader(object sendingProcess, DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.
            if (outLine.Data == "2" || outLine.Data == "4" || outLine.Data == "1" || outLine.Data == "8" || outLine.Data == "16")
            {
                hotModelUpscaleSize = int.Parse(outLine.Data);
            }
        }

        #endregion
    }    
}



