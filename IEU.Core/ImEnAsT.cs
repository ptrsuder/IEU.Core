using DdsFileTypePlus;
using ImageMagick;
using NetVips;
using PaintDotNet;
using ProtoBuf;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Image = NetVips.Image;
using ReactiveCommand = ReactiveUI.ReactiveCommand;


//TODO:
//filter: (not)has result
//write log file
//Image Enhancing Helper - IEH, ImEnHel
//Image Enhancing Mate - IEM, ImEnMate
//Image Enhancing Utility - IEU, IMENU

namespace ImageEnhancingUtility.Core
{    
    public enum OverwriteMode
    {
        None,
        OverwriteTiles,
        OverwriteOriginal
    }
    public enum OutputMode
    {
        Default,
        FolderPerImage,
        FolderPerModel,
        FolderStructure
    }

    [DataContract]
    public class ImEnAsT : ReactiveObject
    {
        public readonly string AppVersion = "0.9.0";
        public readonly string GitHubRepoName = "ImageEnhancingUtility";

        #region PROPERTIES

        public double WindowMinWidth = 800, WindowMinHeight = 650;
        int overlapSize = 16;
        int upscaleMultiplayer = 4;

        List<Task> tasks;
        int hotModelUpscaleSize = 0;

        #region DDS SETTINGS
        private Dictionary<string, int> _ddsTextureType;
        public Dictionary<string, int> ddsTextureType
        {
            get => _ddsTextureType;
            set
            {
                this.RaiseAndSetIfChanged(ref _ddsTextureType, value);
            }
        }
        private List<DdsFileFormatSetting> _ddsFileFormatCurrent = new List<DdsFileFormatSetting>() { new DdsFileFormatSetting("Fsfsf", DdsFileFormat.B4G4R4A4) };
        public List<DdsFileFormatSetting> ddsFileFormatCurrent
        {
            get => _ddsFileFormatCurrent;
            set
            {
                ddsFileFormat = value[0];
                this.RaiseAndSetIfChanged(ref _ddsFileFormatCurrent, value);
            }
        }

        List<DdsFileFormatSetting> ddsFileFormatsColor;
        List<DdsFileFormatSetting> ddsFileFormatsColorAlpha;
        List<DdsFileFormatSetting> ddsFileFormatsNormalMap;
        List<DdsFileFormatSetting>[] ddsFileFormats;

        private List<BC7CompressionMode> _ddsBC7CompressionModes;
        public List<BC7CompressionMode> ddsBC7CompressionModes
        {
            get => _ddsBC7CompressionModes;
            set => this.RaiseAndSetIfChanged(ref _ddsBC7CompressionModes, value);
        }

        private DdsFileFormatSetting _ddsFileFormat;
        public DdsFileFormatSetting ddsFileFormat
        {
            get => _ddsFileFormat;
            set => this.RaiseAndSetIfChanged(ref _ddsFileFormat, value);
        }
        BC7CompressionMode _ddsBC7CompressionMode;
        BC7CompressionMode ddsBC7CompressionMode
        {
            get => _ddsBC7CompressionMode;
            set => this.RaiseAndSetIfChanged(ref _ddsBC7CompressionMode, value);
        }

        bool ddsGenerateMipmaps = true;
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
                this.RaiseAndSetIfChanged(ref _selectedModelsItems, value);
            }
        }
        public List<ModelInfo> checkedModels;

        private double _windowWidth = 800;
        [DataMember(Order = 1)]
        public double WindowWidth
        {
            get => _windowWidth;
            set => this.RaiseAndSetIfChanged(ref _windowWidth, value);
        }

        private double _windowHeight = 650;
        [DataMember(Order = 2)]
        public double WindowHeight
        {
            get => _windowHeight;
            set => this.RaiseAndSetIfChanged(ref _windowHeight, value);
        }

        private double _logPanelWidth = 200;
        [DataMember(Order = 24)]
        public double LogPanelWidth
        {
            get => _logPanelWidth;
            set => this.RaiseAndSetIfChanged(ref _logPanelWidth, value);
        }

        #region FOLDER_PATHS
        private string _esrganPath;
        [DataMember(Order = 3)]
        public string esrganPath
        {
            get => _esrganPath;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (string.IsNullOrEmpty(modelsPath))
                        modelsPath = $"{value}{DirectorySeparator}models";
                    if (string.IsNullOrEmpty(lrPath))
                        lrPath = $"{value}{DirectorySeparator}LR";
                    if (string.IsNullOrEmpty(resultsPath))
                        resultsPath = $"{value}{DirectorySeparator}results";
                    if (string.IsNullOrEmpty(imgPath))
                        imgPath = $"{value}{DirectorySeparator}img";
                    if (string.IsNullOrEmpty(resultsMergedPath))
                        resultsMergedPath = $"{value}{DirectorySeparator}merged_results";
                    this.RaiseAndSetIfChanged(ref _esrganPath, value);
                }
            }
        }
        private string _modelsPath;
        [DataMember(Order = 4)]
        public string modelsPath
        {
            get => _modelsPath;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    this.RaiseAndSetIfChanged(ref _modelsPath, value);
                    CreateModelTree();
                }
            }
        }
        private string _imgPath;
        [DataMember(Order = 5)]
        public string imgPath
        {
            get => _imgPath;
            set => this.RaiseAndSetIfChanged(ref _imgPath, value);
        }
        private string _resultsMergedPath;
        [DataMember(Order = 6)]
        public string resultsMergedPath
        {
            get => _resultsMergedPath;
            set => this.RaiseAndSetIfChanged(ref _resultsMergedPath, value);
        }
        private string _lrPath;
        [DataMember(Order = 7)]
        public string lrPath
        {
            get => _lrPath;
            set => this.RaiseAndSetIfChanged(ref _lrPath, value);
        }
        private string _resultsPath;
        [DataMember(Order = 8)]
        public string resultsPath
        {
            get => _resultsPath;
            set => this.RaiseAndSetIfChanged(ref _resultsPath, value);
        }
        #endregion

        #region SETTINGS

        private bool _createMemoryImage;
        [DataMember(Order = 9)]
        public bool createMemoryImage
        {
            get => _createMemoryImage;
            set => this.RaiseAndSetIfChanged(ref _createMemoryImage, value);
        }

        private bool _ignoreAlpha = false;
        [DataMember(Order = 10)]
        public bool ignoreAlpha
        {
            get => _ignoreAlpha;
            set => this.RaiseAndSetIfChanged(ref _ignoreAlpha, value);
        }

        private bool _preserveImageFormat;
        [DataMember(Order = 11)]
        public bool preserveImageFormat
        {
            get => _preserveImageFormat;
            set => this.RaiseAndSetIfChanged(ref _preserveImageFormat, value);
        } 

        private bool _deleteResults;
        [DataMember(Order = 12)]
        public bool deleteResults
        {
            get => _deleteResults;
            set => this.RaiseAndSetIfChanged(ref _deleteResults, value);
        }

        private int _maxTileResolution = 512 * 380;
        [DataMember(Order = 13)]
        public int maxTileResolution
        {
            get => _maxTileResolution;
            set
            {
                if (value > _maxTileResolution)
                    _maxTileResolutionWidth = value / _maxTileResolutionHeight;
                else
                    _maxTileResolutionHeight = value / _maxTileResolutionWidth;
                this.RaiseAndSetIfChanged(ref _maxTileResolution, value);
            }
        }

        private int _maxTileResolutionWidth = 512;
        [DataMember(Order = 14)]
        public int maxTileResolutionWidth
        {
            get => _maxTileResolutionWidth;
            set
            {
                _maxTileResolutionHeight = _maxTileResolution / value;
                this.RaiseAndSetIfChanged(ref _maxTileResolutionWidth, value);
            }
        }

        private int _maxTileResolutionHeight = 380;
        [DataMember(Order = 15)]
        public int maxTileResolutionHeight
        {
            get => _maxTileResolutionHeight;
            set
            {
                _maxTileResolutionWidth = _maxTileResolution / value;
                this.RaiseAndSetIfChanged(ref _maxTileResolutionHeight, value);
            }
        }

        private int _pngCompression = 0;
        [DataMember(Order = 16)]
        public int pngCompression
        {
            get => _pngCompression;
            set => this.RaiseAndSetIfChanged(ref _pngCompression, value);
        }

        private int _outputDestinationMode = 0;
        [DataMember(Order = 17)]
        public int outputDestinationMode
        {
            get => _outputDestinationMode;
            set => this.RaiseAndSetIfChanged(ref _outputDestinationMode, value);
        }
        private int _overwriteMode = 0;
        [DataMember(Order = 18)]
        public int overwriteMode
        {
            get => _overwriteMode;
            set => this.RaiseAndSetIfChanged(ref _overwriteMode, value);
        }

        private bool _checkForUpdates = true;
        [DataMember(Order = 19)]
        public bool checkForUpdates
        {
            get => _checkForUpdates;
            set => this.RaiseAndSetIfChanged(ref _checkForUpdates, value);
        }

        private int _ddsTextureTypeSelected = 0;
        [DataMember(Order = 20)]
        public int ddsTextureTypeSelected
        {
            get => _ddsTextureTypeSelected;
            set
            {
                ddsFileFormatCurrent = ddsFileFormats[value];
                ddsFileFormat = ddsFileFormatCurrent[0];
                this.RaiseAndSetIfChanged(ref _ddsTextureTypeSelected, value);
            }
        }

        private int _ddsFileFormatSelected = 0;
        [DataMember(Order = 21)]
        public int ddsFileFormatSelected
        {
            get => _ddsFileFormatSelected;
            set
            {
                this.RaiseAndSetIfChanged(ref _ddsFileFormatSelected, value);
            }
        }

        private int _ddsBC7CompressionSelected = 0;
        [DataMember(Order = 22)]
        public int ddsBC7CompressionSelected
        {
            get => _ddsBC7CompressionSelected;
            set => this.RaiseAndSetIfChanged(ref _ddsBC7CompressionSelected, value);
        }
        
        #endregion

        #region MAINTAB_PROGRESS
        private string logs;
        public string Logs
        {
            get => logs;
            set => this.RaiseAndSetIfChanged(ref logs, value);
        }

        private string logMessages;
        public string LogMessages
        {
            get => logMessages;
            set => this.RaiseAndSetIfChanged(ref logMessages, value);
        }


        private int _progressBarValue = 0;
        public int progressBarValue
        {
            get => _progressBarValue;
            set => this.RaiseAndSetIfChanged(ref _progressBarValue, value);
        }

        private int _filesDone = 0;
        public int filesDone
        {
            get => _filesDone;
            set => this.RaiseAndSetIfChanged(ref _filesDone, value);
        }

        private int _filesDoneSuccesfully = 0;
        public int filesDoneSuccesfully
        {
            get => _filesDoneSuccesfully;
            set => this.RaiseAndSetIfChanged(ref _filesDoneSuccesfully, value);
        }

        private int _filesNumber = 0;
        public int filesNumber
        {
            get => _filesNumber;
            set => this.RaiseAndSetIfChanged(ref _filesNumber, value);
        }

        private string _progressLabel = "0/0";
        public string progressLabel
        {
            get => _progressLabel;
            set => this.RaiseAndSetIfChanged(ref _progressLabel, value);
        }
        #endregion

        #region ADVANCED

        public bool _advanceUseResultSuffix = false;
        public bool advanceUseResultSuffix
        {
            get => _advanceUseResultSuffix;
            set => _advanceUseResultSuffix = value;
        }
        public string _advancedResultSuffix = "";
        public string advancedResultSuffix
        {
            get => _advancedResultSuffix;
            set => _advancedResultSuffix = value;
        }

        private bool _filterFilenameContainsEnabled = false;
        public bool filterFilenameContainsEnabled
        {
            get => _filterFilenameContainsEnabled;
            set => _filterFilenameContainsEnabled = value;
        }
        private bool _filterFilenameNotContainsEnabled = false;
        public bool filterFilenameNotContainsEnabled
        {
            get => _filterFilenameNotContainsEnabled;
            set => _filterFilenameNotContainsEnabled = value;
        }
        private bool _filterFilenameCaseSensitive = false;
        public bool filterFilenameCaseSensitive
        {
            get => _filterFilenameCaseSensitive;
            set => _filterFilenameCaseSensitive = value;
        }
        private string _filterFilenameContainsPattern = "";
        public string filterFilenameContainsPattern
        {
            get => _filterFilenameContainsPattern;
            set => _filterFilenameContainsPattern = value;
        }
        private string _filterFilenameNotContainsPattern = "";
        public string filterFilenameNotContainsPattern
        {
            get => _filterFilenameNotContainsPattern;
            set => _filterFilenameNotContainsPattern = value;
        }

        private int _filterAlpha = 0;
        public int filterAlpha
        {
            get => _filterAlpha;
            set => _filterAlpha = value;
        }

        private bool _filterImageResolutionEnabled = false;
        public bool filterImageResolutionEnabled
        {
            get => _filterImageResolutionEnabled;
            set => _filterImageResolutionEnabled = value;
        }
        private bool _filterImageResolutionOr = true;
        public bool filterImageResolutionOr
        {
            get => _filterImageResolutionOr;
            set => _filterImageResolutionOr = value;
        }
        private int _filterImageResolutionMaxWidth = 4096;
        public int filterImageResolutionMaxWidth
        {
            get => _filterImageResolutionMaxWidth;
            set => _filterImageResolutionMaxWidth = value;
        }
        private int _filterImageResolutionMaxHeight = 4096;
        public int filterImageResolutionMaxHeight
        {
            get => _filterImageResolutionMaxHeight;
            set => _filterImageResolutionMaxHeight = value;
        }

        public readonly List<string> filterExtensionsList = new List<string>() {
            ".PNG", ".TGA", ".DDS", ".JPG",
            ".JPEG", ".BMP", ".TIFF"};

        public List<string> filterSelectedExtensionsList { get; set; }

        public readonly string[] postprocessNoiseFilter = new string[] {
             "None", "Enhance", "Despeckle", "Adaptive blur"};

        private int _noiseReductionType = 0;
        public int noiseReductionType
        {
            get => _noiseReductionType;
            set => _noiseReductionType = value;
        }

        public bool thresholdEnabled { get; set; } = false;

        private int _thresholdBlackValue = 0;
        public int thresholdBlackValue
        {
            get => _thresholdBlackValue;
            set => _thresholdBlackValue = value;
        }
        private int _thresholdWhiteValue = 100;
        public int thresholdWhiteValue
        {
            get => _thresholdWhiteValue;
            set => _thresholdWhiteValue = value;
        }

        public static List<double> ResizeImageScaleFactors = new List<double>() { 0.25, 0.5, 1.0, 2.0, 4.0 };
        private double _resizeImageBeforeScaleFactor = 1.0;
        public double resizeImageBeforeScaleFactor
        {
            get => _resizeImageBeforeScaleFactor;
            set => _resizeImageBeforeScaleFactor = value;
        }       
        private double _resizeImageAfterScaleFactor = 1.0;
        public double resizeImageAfterScaleFactor
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
        public int resizeImageBeforeFilterType
        {
            get => _resizeImageBeforeFilterType;
            set => _resizeImageBeforeFilterType = value;
        }
        private int _resizeImageAfterFilterType = 2;
        public int resizeImageAfterFilterType
        {
            get => _resizeImageAfterFilterType;
            set => _resizeImageAfterFilterType = value;
        }
        #endregion

        public bool splitRGB = false;
        public bool useCPU = false;

        public bool seamlessTexture = false;
        int expandSize = 16;
                
        static List<string> losslessFormats = new List<string>() { ".png", ".tiff", ".webp" };
        static List<string> convertFormats = new List<string>() { ".tga", ".dds", ".jpg", ".bmp" };
        public List<string> outputFormats = new List<string>() { ".png", ".tiff", ".webp", ".tga", ".dds", ".jpg", ".bmp" };
        string losslessFormat = ".png"; //change
        string convertFormat = convertFormats[0]; //change
        ImageFormatInfo selectedOutputFormat;
        bool convertImageFormat = false;

        private bool _useDifferentModelForAlpha = false;
        public bool UseDifferentModelForAlpha
        {
            get => _useDifferentModelForAlpha;
            set => _useDifferentModelForAlpha = value;
        }
        private ModelInfo _modelForAlpha;
        public ModelInfo ModelForAlpha
        {
            get => _modelForAlpha;
            set => _modelForAlpha = value;
        }

        
        #endregion

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> CropCommand { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> UpscaleCommand { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> MergeCommand { get; }       

        private bool _darkThemeEnabled = false;
        [DataMember(Order = 23)]
        public bool DarkThemeEnabled
        {
            get => _darkThemeEnabled;
            set => this.RaiseAndSetIfChanged(ref _darkThemeEnabled, value);
        }

        string DirectorySeparator = @"\"; //Path.DirectorySeparatorChar

        public ImageFormatInfo pngFormat = new ImageFormatInfo(".png");
        public ImageFormatInfo tiffFormat = new ImageFormatInfo(".tiff");
        public ImageFormatInfo webpFormat = new ImageFormatInfo(".webp");
        public ImageFormatInfo tgaFormat = new ImageFormatInfo(".tga");
        public ImageFormatInfo ddsFormat = new ImageFormatInfo(".dds");
        public ImageFormatInfo jpgFormat = new ImageFormatInfo(".jpg");
        public ImageFormatInfo bmpFormat = new ImageFormatInfo(".bmp");
        public List<ImageFormatInfo> formatInfos = new List<ImageFormatInfo>();


        public ImEnAsT()
        {
            CropCommand = ReactiveCommand.CreateFromTask(Split);
            UpscaleCommand = ReactiveCommand.CreateFromTask(Upscale);
            MergeCommand = ReactiveCommand.CreateFromTask(Merge);

            formatInfos = new List<ImageFormatInfo>() { pngFormat, tiffFormat, webpFormat, tgaFormat, jpgFormat, bmpFormat };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                DirectorySeparator = @"\";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                DirectorySeparator = @"/";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                DirectorySeparator = @":";

            WriteToLogsThreadSafe(RuntimeInformation.OSDescription);
            WriteToLogsThreadSafe(RuntimeInformation.FrameworkDescription);
            WriteToLogsThreadSafe("Linux: " + RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
            WriteToLogsThreadSafe("Windows: " + RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            WriteToLogsThreadSafe("OSx: " + RuntimeInformation.IsOSPlatform(OSPlatform.OSX));

            ReadSettings();

            #region DDS OUTPUT SETTINGS         

            ddsFileFormatsColor = new List<DdsFileFormatSetting>
            {
                new DdsFileFormatSetting("BC1 (Linear) [DXT1]", DdsFileFormat.BC1),
                new DdsFileFormatSetting("BC1 (sRGB) [DXT1]", DdsFileFormat.BC1Srgb),
                new DdsFileFormatSetting("BC7 (Linear)", DdsFileFormat.BC7),
                new DdsFileFormatSetting("BC7 (sRGB)", DdsFileFormat.BC7Srgb),
                new DdsFileFormatSetting("BC4 (Grayscale)", DdsFileFormat.BC4),
                new DdsFileFormatSetting("Loseless", DdsFileFormat.R8G8B8A8)
            };

            ddsFileFormatsColorAlpha = new List<DdsFileFormatSetting>
            {
                new DdsFileFormatSetting("BC3 (Linear) [DXT5]", DdsFileFormat.BC3),
                new DdsFileFormatSetting("BC3 (sRGB) [DXT5]", DdsFileFormat.BC3Srgb),
                new DdsFileFormatSetting("BC2 (Linear)", DdsFileFormat.BC2),
                new DdsFileFormatSetting("BC7 (Linear)", DdsFileFormat.BC7),
                new DdsFileFormatSetting("BC7 (sRGB)", DdsFileFormat.BC7Srgb),
                new DdsFileFormatSetting("Loseless", DdsFileFormat.R8G8B8A8)
            };

            ddsFileFormatsNormalMap = new List<DdsFileFormatSetting>
            {
                new DdsFileFormatSetting("BC5 (Two channels)", DdsFileFormat.BC5),
                new DdsFileFormatSetting("Loseless", DdsFileFormat.R8G8B8A8)
            };

            ddsFileFormatCurrent = ddsFileFormatsColor;

            ddsFileFormats = new List<DdsFileFormatSetting>[] {
                ddsFileFormatsColor, ddsFileFormatsColorAlpha, ddsFileFormatsNormalMap };

            ddsTextureType = new Dictionary<string, int>
            {
                { "Color", 0 },
                { "Color + Alpha", 1 },
                { "Normal Map", 2 }
            };

            ddsBC7CompressionModes = new List<BC7CompressionMode>
            {
                BC7CompressionMode.Fast,
                BC7CompressionMode.Normal,
                BC7CompressionMode.Slow
            };

            #endregion

            //noiseReductionType_comboBox.Items.AddRange(new string[] { "None", "Enhance", "Despeckle", "Adaptive blur" });             
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
            DirectoryInfo di = new DirectoryInfo(modelsPath);
            if (!di.Exists)
            {
                WriteToLogsThreadSafe($"{di.FullName} doesn't exist!");
                return;
            }

            foreach (DirectoryInfo d in di.GetDirectories("*", SearchOption.TopDirectoryOnly))
                foreach (FileInfo fi in d.GetFiles("*.pth", SearchOption.TopDirectoryOnly))
                    ModelsItemsTemp.Add(new ModelInfo(fi.Name, fi.FullName, d.Name));

            foreach (FileInfo fi in di.GetFiles("*.pth", SearchOption.TopDirectoryOnly))
                ModelsItemsTemp.Add(new ModelInfo(fi.Name, fi.FullName));
            ModelsItems = ModelsItemsTemp;
        }

        public void WriteToLogsThreadSafe(string text)
        {
            Logs += $"\n[{DateTime.Now}] {text}";
        }
        public void WriteToLogsThreadSafe(string text, System.Drawing.Color color)
        {
            //FormattedText formattedText = new FormattedText() {Text = text, Fo Spans = new FormattedTextStyleSpan[] { new FormattedTextStyleSpan(0, text.Length}  };
            //Logs.Text.
            //    .Spans.Append(new FormattedTextStyleSpan();
            WriteToLogsThreadSafe(text);
        }
        public void WriteLogOpenError(FileInfo file, string exMessage)
        {
            WriteToLogsThreadSafe("ERROR opening tile!", System.Drawing.Color.Red);
            WriteToLogsThreadSafe($"{exMessage}", System.Drawing.Color.Red);
            WriteToLogsThreadSafe($"Skipping <{file.Name}>...", System.Drawing.Color.Red);
        }

        private void ReportProgressThreadSafe(bool filtered = true)
        {
            if (filtered)
            {
                filesDoneSuccesfully++;
            }
            filesDone++;
            progressBarValue = filesDone / filesNumber * 100;
            progressLabel = $@"{filesDone}/{filesNumber}";
        }
                

        void SplitTask(FileInfo file)
        {
            MagickImage image = null, inputImage = null, inputImageAlpha = null, inputImageRed = null, inputImageGreen = null, inputImageBlue = null;
            int imageWidth, imageHeight;
            bool imageHasAlpha = false;
            string lrPathAlpha = lrPath + "_alpha";
            try
            {
                if (file.Extension.ToLower() == ".dds" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Surface surface = DdsFile.Load(file.FullName);
                    imageWidth = surface.Width;
                    imageHeight = surface.Height;
                    imageHasAlpha = DdsFile.HasTransparency(surface);
                    image = Helper.ConvertToMagickImage(surface);
                }
                else
                {
                    image = new MagickImage(file.FullName);
                    imageWidth = image.Width;
                    imageHeight = image.Height;
                    imageHasAlpha = image.HasAlpha;
                }
            }
            catch (Exception ex)
            {
                WriteToLogsThreadSafe($"Failed to read file {file.Name}!", System.Drawing.Color.Red);
                return;
            }
            int[] tiles;
            FileInfo fileAlpha = new FileInfo(file.DirectoryName + DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name) + "_alpha.png");

            if (resizeImageBeforeScaleFactor != 1.0)
            {
                image = ResizeImage(image, resizeImageBeforeScaleFactor, (FilterType) resizeImageBeforeFilterType);
                imageWidth = Convert.ToInt32(imageWidth * resizeImageBeforeScaleFactor);
                imageHeight = Convert.ToInt32(imageHeight * resizeImageBeforeScaleFactor);
            }

            if (seamlessTexture)
            {
                MagickImage expandedImage = (MagickImage) image.Clone();
                MagickImage bottomEdge = (MagickImage)image.Clone();
                bottomEdge.Crop(new MagickGeometry(0, imageHeight - 16, imageWidth, 16));
                expandedImage.Page = new MagickGeometry($"+0+{expandSize}");
                bottomEdge.Page = new MagickGeometry("+0+0");
                MagickImageCollection edges = new MagickImageCollection() { bottomEdge, expandedImage };
                expandedImage = (MagickImage)edges.Mosaic();
      
                MagickImage topEdge = (MagickImage)image.Clone();
                topEdge.Crop(new MagickGeometry(0, 0, imageWidth, 16));
                topEdge.Page = new MagickGeometry($"+0+{expandedImage.Height}");
                expandedImage.Page = new MagickGeometry("+0+0");
                edges.Add(topEdge);
                edges = new MagickImageCollection() { expandedImage, topEdge };
                expandedImage = (MagickImage)edges.Mosaic();

                image = (MagickImage)expandedImage.Clone();         

                MagickImage rightEdge = (MagickImage)image.Clone();
                edges = new MagickImageCollection() { rightEdge, expandedImage };
                rightEdge.Crop(new MagickGeometry(image.Width - 16, 0, 16, image.Height));
                expandedImage.Page = new MagickGeometry($"+{expandSize}+0");
                rightEdge.Page = new MagickGeometry("+0+0");
                expandedImage = (MagickImage) edges.Mosaic();

                MagickImage leftEdge = (MagickImage) image.Clone();
                edges = new MagickImageCollection() { expandedImage, leftEdge };
                leftEdge.Crop(new MagickGeometry(0, 0, 16, image.Height));
                leftEdge.Page = new MagickGeometry($"+{expandedImage.Width}+0");
                expandedImage.Page = new MagickGeometry($"+0+0");
                expandedImage = (MagickImage) edges.Mosaic();

                image = (MagickImage)expandedImage.Clone();
                imageWidth = image.Width;
                imageHeight = image.Height;
            }

            inputImage = (MagickImage)image.Clone();
            inputImage.HasAlpha = false;
            
            switch (noiseReductionType)
            {
                case 0:
                    break;
                case 1:
                    inputImage.Enhance();
                    break;
                case 2:
                    inputImage.Despeckle();
                    break;
                case 3:
                    inputImage.AdaptiveBlur();
                    break;
            }            

            if (imageHasAlpha && !ignoreAlpha)
            {
                imageHasAlpha = true;
                inputImageAlpha = (MagickImage)image.Separate(Channels.Alpha).First();
            }

            if (splitRGB)
            {
                if (image.ColorSpace == ColorSpace.RGB || image.ColorSpace == ColorSpace.sRGB)
                {
                    inputImageRed = (MagickImage)image.Separate(Channels.Red).First();
                    inputImageGreen = (MagickImage)image.Separate(Channels.Green).First();
                    inputImageBlue = (MagickImage)image.Separate(Channels.Blue).First();
                }
                else
                {
                    WriteToLogsThreadSafe("Image is not RGB");
                    return;
                }
            }

            if (imageHeight * imageWidth > maxTileResolution)
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, maxTileResolution);
            else
                tiles = new int[] { 1, 1 };

            int tileWidth = imageWidth / tiles[0];
            int tileHeight = imageHeight / tiles[1];
            int rightOverlap = 1;
            int bottomOverlap = 1;

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
                    int xOffset = rightOverlap * overlapSize;
                    int yOffset = bottomOverlap * overlapSize;
                    int tile_X1 = j * tileWidth;
                    int tile_Y1 = i * tileHeight;
                    
                    Directory.CreateDirectory($"{lrPath}\\{Path.GetDirectoryName(file.FullName).Replace(imgPath, "")}");                    

                    if (imageHasAlpha && !ignoreAlpha) //Crop Alpha
                    {
                        MagickImage outputImageAlpha = (MagickImage)inputImageAlpha.Clone();
                        outputImageAlpha.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        if(UseDifferentModelForAlpha)
                            outputImageAlpha.Write($"{lrPathAlpha}\\{Path.GetDirectoryName(fileAlpha.FullName).Replace(imgPath, "")}\\{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex.ToString("D2")}.png");
                        else
                            outputImageAlpha.Write($"{lrPath}\\{Path.GetDirectoryName(fileAlpha.FullName).Replace(imgPath, "")}\\{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex.ToString("D2")}.png");
                    }
                    if(splitRGB)
                    {
                        MagickImage outputImageRed = (MagickImage)inputImageRed.Clone();
                        outputImageRed.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        outputImageRed.Write($"{lrPath}\\{Path.GetDirectoryName(file.FullName).Replace(imgPath, "")}\\{Path.GetFileNameWithoutExtension(file.Name)}_R_tile-{tileIndex.ToString("D2")}.png");

                        MagickImage outputImageGreen = (MagickImage)inputImageGreen.Clone();
                        outputImageGreen.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        outputImageGreen.Write($"{lrPath}\\{Path.GetDirectoryName(file.FullName).Replace(imgPath, "")}\\{Path.GetFileNameWithoutExtension(file.Name)}_G_tile-{tileIndex.ToString("D2")}.png");

                        MagickImage outputImageBlue = (MagickImage)inputImageBlue.Clone();
                        outputImageBlue.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        outputImageBlue.Write($"{lrPath}\\{Path.GetDirectoryName(file.FullName).Replace(imgPath, "")}\\{Path.GetFileNameWithoutExtension(file.Name)}_B_tile-{tileIndex.ToString("D2")}.png");
                    }
                    else
                    {
                        MagickImage outputImage = (MagickImage)inputImage.Clone();                        
                        outputImage.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        outputImage.Write($"{lrPath}\\{Path.GetDirectoryName(file.FullName).Replace(imgPath, "")}\\{Path.GetFileNameWithoutExtension(file.Name)}_tile-{tileIndex.ToString("D2")}.png");
                    }
                }
            }
            WriteToLogsThreadSafe($"{file.Name} DONE", System.Drawing.Color.LightGreen);
            ReportProgressThreadSafe();
        }
        async public Task Split()
        {
            DirectoryInfo directory = new DirectoryInfo(lrPath);
            DirectoryInfo directoryAlpha = new DirectoryInfo(lrPath + "_alpha");
            if (UseDifferentModelForAlpha && !directoryAlpha.Exists)
                directoryAlpha.Create();
            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (outputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;
            directory.GetFiles("*", searchOption).ToList().ForEach(x => x.Delete());
            WriteToLogsThreadSafe($"'{lrPath}' is cleared", System.Drawing.Color.LightBlue);

            if (UseDifferentModelForAlpha)
            {
                directoryAlpha.GetFiles("*", searchOption).ToList().ForEach(x => x.Delete());
                WriteToLogsThreadSafe($"'{lrPath + "_alpha"}' is cleared", System.Drawing.Color.LightBlue);
            }           

            DirectoryInfo di = new DirectoryInfo(imgPath);
            WriteToLogsThreadSafe("Creating tiles...");

            tasks = new List<Task>();
            filesDone = 0;
            filesNumber = di.GetFiles("*", searchOption).Length;

            if (createMemoryImage)
            {
                Image image = Image.Black(maxTileResolutionWidth, maxTileResolutionHeight);
                image.WriteToFile($"{lrPath}{DirectorySeparator}(000)000.png");
            }

            

            foreach (var file in di.GetFiles("*", searchOption))
            {
                if (!file.Exists)
                    continue;

                if (!ApplyFilter(file))
                {
                    ReportProgressThreadSafe(false);
                    WriteToLogsThreadSafe($"{file.Name} is filtered, skipping", System.Drawing.Color.HotPink);
                    continue;
                }
                //SplitTask(file);
                tasks.Add(Task.Factory.StartNew(() => SplitTask(file)));
            }
            await Task.WhenAll(tasks.ToArray());
            tasks.Clear();
            WriteToLogsThreadSafe("Finished!", System.Drawing.Color.LightGreen);
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


        void MergeTask(FileInfo file, string basePath, int outputMode)
        {
            #region IMAGE READ
            bool useMosaic = false;
            bool alphaReadError = false, cancelRgbGlobalbalance = true, cancelAlphaGlobalbalance = true;
            string basePathAlpha = basePath;
            string resultSuffix = "";
            if (advanceUseResultSuffix)
                resultSuffix = advancedResultSuffix;

            if (outputMode == 1) // tell to grab alpha tiles in another folder
            {
                string fileName = Path.GetFileNameWithoutExtension(file.Name);
                basePathAlpha = Regex.Replace(basePathAlpha, $@"{DirectorySeparator}images{DirectorySeparator}{fileName}", $@"{DirectorySeparator}images{DirectorySeparator}{fileName}_alpha");
            }
            DirectoryInfo resultsFolder = new DirectoryInfo(resultsPath);

            bool imageHasAlpha = false;
            int imageWidth = 0, imageHeight = 0;
            MagickImage image;

            try
            {
                if (file.Extension.ToLower() == ".dds" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Surface surface = DdsFile.Load(file.FullName);
                    imageWidth = surface.Width;
                    imageHeight = surface.Height;
                    imageHasAlpha = DdsFile.HasTransparency(surface);
                    image = Helper.ConvertToMagickImage(surface);
                }               
                else
                {
                    image = new MagickImage(file.FullName);
                    imageWidth = image.Width;
                    imageHeight = image.Height;
                    imageHasAlpha = image.HasAlpha;
                }
            }
            catch
            {
                WriteToLogsThreadSafe($"Failed to read file {file.Name}!", System.Drawing.Color.Red);
                return;
            }

            int[] tiles;
            if (imageHeight * imageWidth > maxTileResolution)
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, maxTileResolution);
            else
                tiles = new int[] { 1, 1 };

            Image imageRow = null, imageResult = null;
            Image imageAlphaRow = null, imageAlphaResult = null;
            Image imageRowR = null, imageRowG = null, imageRowB = null;            

            List<FileInfo> tileFilesToDelete = new List<FileInfo>();

            MagickImageInfo lastTile;
            try
            {
                string pathToLastTile = $"{resultsPath + basePath}_tile-{((tiles[1] - 1) * tiles[0] + tiles[0] - 1).ToString("D2")}{resultSuffix}.png";
                if (splitRGB)                
                    pathToLastTile = $"{resultsPath + basePath}_R_tile-{((tiles[1] - 1) * tiles[0] + tiles[0] - 1).ToString("D2")}{resultSuffix}.png";                   
                
                lastTile = new MagickImageInfo(pathToLastTile);
            }
            catch (Exception ex)
            {
                WriteLogOpenError(file, ex.Message);
                return;
            }
            #endregion
            
            #region TILES MERGE
            int tileWidth = lastTile.Width, tileHeight = lastTile.Height;

            for (int i = 0; i < tiles[1]; i++)
            {
                for (int j = 0; j < tiles[0]; j++)
                {
                    int tileIndex = i * tiles[0] + j;

                    if (j == 0)
                    {
                        try
                        {
                            if(splitRGB)
                            {
                                imageRowR = Image.NewFromFile($"{resultsPath + basePath}_R_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false)[0];
                                tileFilesToDelete.Add(new FileInfo($"{resultsPath + basePath}_R_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));
                                imageRowG = Image.NewFromFile($"{resultsPath + basePath}_G_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false)[0];
                                tileFilesToDelete.Add(new FileInfo($"{resultsPath + basePath}_G_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));
                                imageRowB = Image.NewFromFile($"{resultsPath + basePath}_B_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false)[0];
                                tileFilesToDelete.Add(new FileInfo($"{resultsPath + basePath}_B_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));               
                                imageRow = imageRowR.Bandjoin(imageRowG).Bandjoin(imageRowB);
                            }
                            else
                            {
                                imageRow = Image.NewFromFile($"{resultsPath + basePath}_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false);
                                tileFilesToDelete.Add(new FileInfo($"{resultsPath + basePath}_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));
                            }                           
                        }
                        catch (VipsException ex)
                        {
                            WriteLogOpenError(file, ex.Message);
                            return;
                        }

                        if (imageHasAlpha && !ignoreAlpha && !alphaReadError)
                        {
                            try
                            {
                                imageAlphaRow = Image.NewFromFile($"{resultsPath + basePathAlpha}_alpha_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false);
                                tileFilesToDelete.Add(new FileInfo($"{resultsPath + basePathAlpha}_alpha_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));
                            }
                            catch (VipsException ex)
                            {
                                alphaReadError = true; // failed to merge alpha, alpha will be skipped
                                WriteLogOpenError(new FileInfo($"{resultsPath + basePathAlpha}_alpha_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"), ex.Message);
                            }
                        }
                        continue;
                    }

                    Image imageNextTile = null, imageAlphaNextTile = null;
                    Image imageNextTileR = null, imageNextTileG = null, imageNextTileB = null;
                    try
                    {
                        if (splitRGB)
                        {
                            imageNextTileR = Image.NewFromFile($"{resultsPath + basePath}_R_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false)[0];
                            tileFilesToDelete.Add(new FileInfo($"{resultsPath + basePath}_R_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));
                            imageNextTileG = Image.NewFromFile($"{resultsPath + basePath}_G_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false)[0];
                            tileFilesToDelete.Add(new FileInfo($"{resultsPath + basePath}_G_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));
                            imageNextTileB = Image.NewFromFile($"{resultsPath + basePath}_B_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false)[0];
                            tileFilesToDelete.Add(new FileInfo($"{resultsPath + basePath}_B_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));      
                            imageNextTile = imageNextTileR.Bandjoin(imageNextTileG).Bandjoin(imageNextTileB);
                        }
                        else
                        {
                            imageNextTile = Image.NewFromFile($"{resultsPath + basePath}_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false);
                            tileFilesToDelete.Add(new FileInfo($"{resultsPath + basePath}_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));
                        }
                    }
                    catch (VipsException ex)
                    {
                        WriteLogOpenError(file, ex.Message);
                        return;
                    }

                    if (imageHasAlpha && !ignoreAlpha && !alphaReadError)
                    {
                        try
                        {
                            imageAlphaNextTile = Image.NewFromFile($"{resultsPath + basePathAlpha}_alpha_tile-{tileIndex.ToString("D2")}{resultSuffix}.png", false);
                            tileFilesToDelete.Add(new FileInfo($"{resultsPath + basePathAlpha}_alpha_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"));
                            if(useMosaic)
                                imageAlphaRow = imageAlphaRow.Mosaic(imageAlphaNextTile, Enums.Direction.Horizontal, tileWidth * (j), 0, 0, 0);
                            else
                                imageAlphaRow = imageAlphaRow.Merge(imageAlphaNextTile, Enums.Direction.Horizontal, -tileWidth * (j), 0);
                            try
                            {
                                if (!cancelAlphaGlobalbalance)
                                {
                                    Image tempImage = imageAlphaRow.CopyMemory();
                                    tempImage = tempImage.Globalbalance();
                                    imageAlphaRow = tempImage.CopyMemory();
                                    tempImage.Dispose();
                                }
                            }
                            catch
                            {
                                cancelAlphaGlobalbalance = true;
                                WriteToLogsThreadSafe($"Failed to use globalbalance on {file.Name} alpha", System.Drawing.Color.Red);
                            }
                        }
                        catch (VipsException ex)
                        {
                            alphaReadError = true;
                            WriteLogOpenError(new FileInfo($"{resultsPath + basePathAlpha}_alpha_tile-{tileIndex.ToString("D2")}{resultSuffix}.png"), ex.Message);
                        }
                    }

                    if (useMosaic)
                        imageRow = imageRow.Mosaic(imageNextTile, Enums.Direction.Horizontal, tileWidth * j, 0, 0, 0);
                    else
                        imageRow = imageRow.Merge(imageNextTile, Enums.Direction.Horizontal, -tileWidth * j, 0);

                    try
                    {
                        if (!cancelRgbGlobalbalance)
                        {
                            Image tempImage = imageRow.CopyMemory();
                            tempImage = tempImage.Globalbalance();
                            imageRow = tempImage.CopyMemory();
                            tempImage.Dispose();
                        }
                    }
                    catch
                    {
                        cancelRgbGlobalbalance = true;
                        WriteToLogsThreadSafe($"Failed to use globalbalance on {file.Name}", System.Drawing.Color.Red);
                    }
                    imageNextTile.Dispose();
                }
                
                if (i == 0)
                {
                    imageResult = imageRow;
                    if (imageHasAlpha && !ignoreAlpha && !alphaReadError)
                        imageAlphaResult = imageAlphaRow.Copy();
                }
                else
                {
                    if (useMosaic)
                        imageResult = imageResult.Mosaic(imageRow, Enums.Direction.Vertical, 0, tileHeight * i, 0, 0);
                    else
                        imageResult = imageResult.Merge(imageRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                    
                    try
                    {
                        if (!cancelRgbGlobalbalance)
                        {
                            Image tempImage = imageResult.CopyMemory();
                            tempImage = tempImage.Globalbalance();
                            imageResult = tempImage.CopyMemory();
                            tempImage.Dispose();
                        }
                    }
                    catch
                    {
                        cancelRgbGlobalbalance = true;
                        WriteToLogsThreadSafe($"Failed to use globalbalance on {file.Name}", System.Drawing.Color.Red);
                    }

                    if (imageHasAlpha && !ignoreAlpha && !alphaReadError)
                    {
                        if (useMosaic)
                            imageAlphaResult = imageAlphaResult.Mosaic(imageAlphaRow, Enums.Direction.Vertical, 0, tileHeight * i, 0, 0);
                        else
                            imageAlphaResult = imageAlphaResult.Merge(imageAlphaRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                        try
                        {
                            if (!cancelAlphaGlobalbalance)
                            {
                                Image tempImage = imageAlphaResult.Copy();
                                tempImage = tempImage.Globalbalance(intOutput: true);
                                imageAlphaResult = tempImage.Copy();
                                tempImage.Dispose();
                            }
                        }
                        catch
                        {
                            cancelAlphaGlobalbalance = true;
                            WriteToLogsThreadSafe($"Failed to use globalbalance on {file.Name} alpha", System.Drawing.Color.Red);
                        }

                    }
                }
            }

            if (tiles[1] > 1) // more than 1 row
            {
                imageRow.Dispose(); //dispose of previous
                imageAlphaRow?.Dispose();
            }
            
            if (imageHasAlpha && !ignoreAlpha && !alphaReadError)
            {
                imageResult = imageResult.Bandjoin(imageAlphaResult);
                imageResult = imageResult.Copy(interpretation: "srgb").Cast("uchar");
                imageAlphaResult.Dispose();
            }
            #endregion

            #region SAVE IMAGE

            ImageFormatInfo outputFormat;
            if (preserveImageFormat)
                outputFormat = formatInfos.Where(x => x.Extension.Equals(file.Extension, StringComparison.InvariantCultureIgnoreCase)).First();
            else
                outputFormat = selectedOutputFormat;

            if (outputFormat == null)
                outputFormat = new ImageFormatInfo(file.Extension);

            string destinationPath = resultsMergedPath + basePath + outputFormat;

            if (outputDestinationMode == 3)
            {
                destinationPath = $"{resultsMergedPath}{Path.GetDirectoryName(file.FullName).Replace(imgPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}{outputFormat}";
            }

            if (imageResult.Width % image.Width != 0) //seamlessTexture
            {
                int upscaleModificator = imageResult.Width / image.Width;
                int edgeSize = upscaleModificator * expandSize;
                imageResult = imageResult.Crop(edgeSize, edgeSize, imageResult.Width - 2*edgeSize, imageResult.Height - 2*edgeSize).CopyMemory();
            }

            if (                
                outputFormat.VipsNative &&
                !convertImageFormat &&
                thresholdBlackValue == 0 &&
                thresholdWhiteValue == 100 &&
                resizeImageAfterScaleFactor == 1.0) //no need to convert to MagickImage, save fast with vips
            {                
                if (overwriteMode == 2)
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

                try
                {                                   
                    if (outputFormat.Extension == ".png")
                        imageResult.Pngsave(destinationPath, compression: outputFormat.CompressionFactor);
                    //VIPS_FOREIGN_TIFF_COMPRESSION_NONE, VIPS_FOREIGN_TIFF_COMPRESSION_JPEG, VIPS_FOREIGN_TIFF_COMPRESSION_DEFLATE, 
                    if (outputFormat.Extension == ".tiff")
                        imageResult.Tiffsave(destinationPath, outputFormat.CompressionMethod);
                    if (outputFormat.Extension == ".webp")
                        imageResult.Webpsave(destinationPath, lossless: true, nearLossless: false, q: 100);
                }
                catch(Exception ex)
                {
                    WriteToLogsThreadSafe($"Failed to save {destinationPath}\n{ex.Message}");
                    return;
                }
                imageResult.Dispose();
                ReportProgressThreadSafe();
                WriteToLogsThreadSafe($"<{file.Name}> DONE", System.Drawing.Color.LightGreen);

                if (deleteResults)
                    tileFilesToDelete.ForEach(x => x.Delete());
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

            if (thresholdBlackValue != 0)
                finalImage.BlackThreshold(new Percentage((double)thresholdBlackValue));
            if (thresholdWhiteValue != 100)
                finalImage.WhiteThreshold(new Percentage((double)thresholdWhiteValue));

            if(resizeImageAfterScaleFactor != 1.0)
            {
                finalImage = ResizeImage(finalImage, resizeImageAfterScaleFactor, (FilterType) resizeImageAfterFilterType);
            }

            destinationPath = resultsMergedPath + basePath + outputFormat;

            if (outputDestinationMode == 3)
            {
                destinationPath = $"{resultsMergedPath}{Path.GetDirectoryName(file.FullName).Replace(imgPath, "")}\\{Path.GetFileNameWithoutExtension(file.Name)}{outputFormat}";
            }
            if (overwriteMode == 2)
            {
                file.Delete();
                destinationPath = $"{resultsMergedPath}{DirectorySeparator}{Path.GetDirectoryName(file.FullName).Replace(imgPath, "")}\\{file.Name}";
            }
            else
            {
                string a = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(a))
                    Directory.CreateDirectory(a);
            }

            if (outputFormat.Extension == ".dds")
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Surface processedSurface = Helper.ConvertToSurface(finalImage);
                    FileStream fileStream = new FileStream(destinationPath, FileMode.Create);
                    DdsFile.Save(
                    fileStream,
                    ddsFileFormat.DdsFileFormat,
                    DdsErrorMetric.Perceptual,
                    ddsBC7CompressionMode,
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
            else
                finalImage.Write(destinationPath);
            
            finalImage.Dispose();
            ReportProgressThreadSafe();
            WriteToLogsThreadSafe($"{file.Name} DONE", System.Drawing.Color.LightGreen);
            if (deleteResults)
                tileFilesToDelete.ForEach(x => x.Delete());
            return;

            #endregion
        }
        async public Task Merge()
        {
            WriteToLogsThreadSafe("Merging tiles...");

            tasks = new List<Task>();
            DirectoryInfo di = new DirectoryInfo(imgPath);

            filesDone = 0;
            filesNumber = 0;
            filesDoneSuccesfully = 0;

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (outputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;         

            foreach (var file in di.GetFiles("*", searchOption))
            {
                if (!file.Exists)
                    continue;

                if (!ApplyFilter(file))
                {
                    //ReportProgressThreadSafe(false);
                    WriteToLogsThreadSafe($"{file.Name} is filtered, skipping", System.Drawing.Color.HotPink);
                    continue;
                }

                if (outputDestinationMode == 1)
                {
                    DirectoryInfo imagesFolder = new DirectoryInfo(resultsPath + $"{DirectorySeparator}images{DirectorySeparator}" + Path.GetFileNameWithoutExtension(file.Name));

                    if (!imagesFolder.Exists || imagesFolder.GetFiles().Length == 0)
                    {
                        WriteLogOpenError(file, "Can't find tiles in result folder");
                        return;
                    }

                    foreach (var image in imagesFolder.GetFiles("*", SearchOption.TopDirectoryOnly).Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_tile-00")))
                    {
                        string s = $"{DirectorySeparator}images{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}{DirectorySeparator}{Path.GetFileNameWithoutExtension(image.Name)}";
                        s = s.Remove(s.Length - 8, 8);
                        filesNumber++;
                        //MergeTask(file, s, 1);
                        tasks.Add(Task.Run(() => MergeTask(file, s, outputDestinationMode)));
                    }
                    continue;
                }

                if (outputDestinationMode == 2)
                {
                    DirectoryInfo modelsFolder = new DirectoryInfo(resultsPath + $"{DirectorySeparator}models{DirectorySeparator}");

                    foreach (var modelFolder in modelsFolder.GetDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        foreach (var image in modelFolder.GetFiles("*", SearchOption.TopDirectoryOnly).Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_tile-")))
                        {
                            filesNumber++; ;
                            //MergeTask(file, $"\\models\\{modelFolder.Name}\\{Path.GetFileNameWithoutExtension(file.Name)}", 2);                            
                            tasks.Add(Task.Run(() =>
                            MergeTask(
                                file,
                                $"{DirectorySeparator}models{DirectorySeparator}{modelFolder.Name}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}",
                                outputDestinationMode)));
                            break;
                        }
                    }
                    continue;
                }
                if (outputDestinationMode == 3)
                {
                    filesNumber = di.GetFiles("*", searchOption).Length;
                    //MergeTask(file, "\\" + Path.GetFileNameWithoutExtension(file.Name), 3);                     
                    tasks.Add(Task.Run(() => MergeTask(file, DirectorySeparator
                        + Path.GetFileNameWithoutExtension(file.Name), outputDestinationMode)));
                    continue;
                }
                filesNumber = di.GetFiles().Length;
                //MergeTask(file, DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name), 0);
                tasks.Add(Task.Run(() => MergeTask(file, DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name), 0)));
            }
            await Task.WhenAll(tasks.ToArray());
            tasks.Clear();
            GC.Collect();
            WriteToLogsThreadSafe("Finished!", System.Drawing.Color.LightGreen);
            string pathToMergedFiles = resultsMergedPath;
            if (outputDestinationMode == 1)
                pathToMergedFiles += $"{DirectorySeparator}images";
            if (outputDestinationMode == 2)
                pathToMergedFiles += $"{DirectorySeparator}models";
        }
        

        bool ApplyFilter(FileInfo file)
        {
            bool alphaFilter = true, filenameFilter = true, sizeFilter = true, resultsFilter = true;
            string[] patternsContains = filterFilenameContainsPattern.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            string[] patternsNotContains = filterFilenameNotContainsPattern.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            string filename = file.Name;
            if (!filterFilenameCaseSensitive)
            {
                filename = filename.ToUpper();
                for (int i = 0; i < patternsContains.Length; i++)
                    patternsContains[i] = patternsContains[i].ToUpper();
            }
            if (filterFilenameContainsEnabled)
            {
                bool matchPattern = false;
                foreach (string pattern in patternsContains)
                    matchPattern = matchPattern || filename.Contains(pattern);

                filenameFilter = filenameFilter && matchPattern;
            }
            if (filterFilenameNotContainsEnabled)
            {
                bool matchPattern = false;
                foreach (string pattern in patternsNotContains)
                    matchPattern = matchPattern || !filename.Contains(pattern);

                filenameFilter = filenameFilter && matchPattern;
            }
            if (!filenameFilter) return false;

            //if (filterExtensionsList.Where(x => x.Value == true).Count() > 0 &&
            //    !filterExtensionsList.Keys.Contains(file.Extension.ToUpper()))
            //    return false;

            if (filterSelectedExtensionsList?.Count() > 0 &&
                !filterSelectedExtensionsList.Contains(file.Extension.ToUpper()))
                return false;

            if (filterAlpha != 0 || filterImageResolutionEnabled) //need to load Magick image
            {
                try
                {
                    using (MagickImage image = new MagickImage(file.FullName))
                    {
                        if (filterAlpha != 0)
                        {
                            switch (filterAlpha) // switch alpha filter type
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

                        if (filterImageResolutionEnabled)
                        {
                            if (!filterImageResolutionOr) // OR
                                sizeFilter = image.Width <= filterImageResolutionMaxWidth || image.Height <= filterImageResolutionMaxHeight;
                            else // AND
                                sizeFilter = image.Width <= filterImageResolutionMaxWidth && image.Height <= filterImageResolutionMaxHeight;
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
                            if (filterAlpha != 0)
                            {
                                switch (filterAlpha) // switch alpha filter type
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

                            if (!filterImageResolutionEnabled)
                            {
                                if (!filterImageResolutionOr) // OR
                                    sizeFilter = image.Width <= filterImageResolutionMaxWidth || image.Height <= filterImageResolutionMaxHeight;
                                else // AND
                                    sizeFilter = image.Width <= filterImageResolutionMaxWidth && image.Height <= filterImageResolutionMaxHeight;
                                if (!sizeFilter) return false;
                            }
                        }
                    }
                }
            }
            return true;
        }
        
        async public Task Upscale()
        {
            checkedModels = SelectedModelsItems;

            if (checkedModels.Count == 0)
            {
                WriteToLogsThreadSafe("No models selected!");
                return;
            }

            DirectoryInfo directory = new DirectoryInfo(resultsPath);
            if (!directory.Exists)
                directory.Create();

            Process process = await ESRGAN();
            int processExitCode = -666;

            processExitCode = await RunProcessAsync(process);
            if (processExitCode == -666)
                return;
            if (processExitCode != 0)
            {
                WriteToLogsThreadSafe("Error ocured during ESRGAN work!", System.Drawing.Color.Red);
                return;
            }
            WriteToLogsThreadSafe("ESRGAN finished!", System.Drawing.Color.LightGreen);
        }

        void CreateUpscaleScript()
        {
            string script = "";

            if (outputDestinationMode == 0)
                script = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.upscaleDefault.py");
            if (outputDestinationMode == 1)
                script = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.upscaleFolderForImage.py");
            if (outputDestinationMode == 2)
                script = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.upscaleFolderForModel.py");
            if (outputDestinationMode == 3)
                script = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.upscaleFolderStructure.py");

            if (overwriteMode == 2)
                script = Regex.Replace(script, "results", "LR");

            File.WriteAllText(esrganPath + $"{DirectorySeparator}hackallimages.py", script);
        }
        
        #region PYTHON PROCESS STUFF

        async Task<Process> ESRGAN()
        {
            if (checkedModels.Count > 1 && UseDifferentModelForAlpha)
            {
                WriteToLogsThreadSafe("Only single model must be selected when using different model for alpha");
                return null;
            }

            Process process = new Process();

            process.StartInfo.Arguments = $"{esrganPath}";
            bool noValidModel = true;
            string torchDevice = useCPU ? "cpu" : "cuda";

            foreach (ModelInfo checkedModel in checkedModels)
            {
                var regResult = Regex.Match(checkedModel.Name.ToLower(), "(?:_?[1-4]x_)|(?:_x[1-4]_?)|(?:_[1-4]x_?)|(?:_?x[1-4]_)");
                if (regResult.Success && regResult.Groups.Count == 1)
                {
                    upscaleMultiplayer = int.Parse(regResult.Value.Replace("x", "").Replace("_", ""));                    
                    noValidModel = false;
                }
                else
                {
                    int processExitCodePthReader = -666;
                    Process pthReaderProcess = PthReader(checkedModel.FullName);
                    WriteToLogsThreadSafe($"Detecting {checkedModel.Name} upscale size...");
                    processExitCodePthReader = await RunProcessAsync(pthReaderProcess);
                    if (processExitCodePthReader != 0)
                    {
                        WriteToLogsThreadSafe($"Error trying detect {checkedModel.Name} upscale size!", System.Drawing.Color.Red);
                        continue;
                    }
                    WriteToLogsThreadSafe($"{checkedModel.Name} upscale size is {hotModelUpscaleSize}", System.Drawing.Color.LightGreen);                   
                    upscaleMultiplayer = hotModelUpscaleSize;
                    noValidModel = false;
                }                
                process.StartInfo.Arguments += $" & python hackallimages.py \"{checkedModel.FullName}\" {upscaleMultiplayer} {torchDevice} \"{lrPath+"\\*"}\" \"{resultsPath}\"";                
            }

            if (UseDifferentModelForAlpha)
            {
                //detect upsacle factor for alpha model
                bool validModelAlpha = false;
                int upscaleSizeAlpha = 0;
                var regResultAlpha = Regex.Match(ModelForAlpha.Name.ToLower(), "(?:_?[1-4]x_)|(?:_x[1-4]_?)|(?:_[1-4]x_?)|(?:_?x[1-4]_)");
                if (regResultAlpha.Success && regResultAlpha.Groups.Count == 1)
                {
                    upscaleSizeAlpha = int.Parse(regResultAlpha.Value.Replace("x", "").Replace("_", ""));
                    validModelAlpha = true;
                }
                else
                {
                    int processExitCodePthReader = -666;
                    Process pthReaderProcess = PthReader(ModelForAlpha.FullName);
                    WriteToLogsThreadSafe($"Detecting {ModelForAlpha.Name} upscale size...");
                    processExitCodePthReader = await RunProcessAsync(pthReaderProcess);
                    if (processExitCodePthReader != 0)
                        WriteToLogsThreadSafe($"Error trying detect {ModelForAlpha.Name} upscale size!", System.Drawing.Color.Red);
                    else
                    {
                        upscaleSizeAlpha = hotModelUpscaleSize;
                        WriteToLogsThreadSafe($"{ModelForAlpha.Name} upscale size is {upscaleSizeAlpha}", System.Drawing.Color.LightGreen);
                        validModelAlpha = true;
                    }
                }
                if (upscaleMultiplayer != upscaleSizeAlpha)
                {
                    WriteToLogsThreadSafe("Upscale size for rgb model and alpha model must be the same");
                    return null;
                }

                if (validModelAlpha)
                    process.StartInfo.Arguments += $" & python hackallimages.py \"{ModelForAlpha.FullName}\" {upscaleSizeAlpha} {torchDevice} \"{lrPath + "_alpha\\*"}\" \"{resultsPath}\"";
            }
            if (noValidModel)
            {
                WriteToLogsThreadSafe("Can't start ESRGAN: no selected models with known upscale size");
                return null;
            }

            CreateUpscaleScript();

            process.ErrorDataReceived += SortOutputHandler;
            process.OutputDataReceived += SortOutputHandler;

            if (!Directory.Exists(lrPath))
            {
                WriteToLogsThreadSafe(lrPath + " doen't exist!");
                return null;
            }
            filesNumber = Directory.GetFiles(lrPath).Count() * checkedModels.Count;
            if(UseDifferentModelForAlpha)
                filesNumber += Directory.GetFiles(lrPath + "_alpha").Count();
            filesDone = 0;
            filesDoneSuccesfully = 0;

            WriteToLogsThreadSafe("Starting ESRGAN...");

            return process;
        }

        Process PthReader(string modelPath)
        {
            Process process = new Process();
            process.StartInfo.Arguments = $"{Helper.GetApplicationRoot()}";
            process.StartInfo.Arguments += $" & python pthReader.py -p \"{modelPath}\"";

            process.ErrorDataReceived += SortOutputHandler;
            process.OutputDataReceived += SortOutputHandlerPthReader;

            return process;
        }

        Task<int> RunProcessAsync(Process process)
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
            process.StartInfo.CreateNoWindow = false;
            process.EnableRaisingEvents = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"cd {process.StartInfo.Arguments.Replace("\"", "\\\"").Replace("&", "&&")}\"";
                WriteToLogsThreadSafe(process.StartInfo.Arguments);
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
            if (alpha < 0.0 || alpha > 1.0)
            {
                WriteToLogsThreadSafe("Alpha should be between 0.0 and 1.0");
                return false;
            }

            string outputPath;
            if (outputName != "")
                outputPath = $"{modelsPath}{DirectorySeparator}{outputName}";
            else
                outputPath = $"{modelsPath}{DirectorySeparator}{Path.GetFileNameWithoutExtension(a)}_{Path.GetFileNameWithoutExtension(b)}_interp_{alpha.ToString().Replace(",", "")}.pth";

            string script = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.interpModels.py");
            File.WriteAllText(esrganPath + $"{DirectorySeparator}interpModels.py", script);

            Process process = new Process();
            process.StartInfo.Arguments = $"{esrganPath}";
            process.StartInfo.Arguments += $" & python interpModels.py \"{a}\" \"{b}\" {alpha.ToString().Replace(",",".")} \"{outputPath}\"";
            process.ErrorDataReceived += SortOutputHandler;
            process.OutputDataReceived += SortOutputHandler;
            int code = await RunProcessAsync(process);
            if(code == 0)
            {
                WriteToLogsThreadSafe("Finished interpolating!");
                CreateModelTree();
                
            }
            return true;

        }

        private void SortOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.

            if (!string.IsNullOrEmpty(outLine.Data)
                && outLine.Data != $"{esrganPath}>"
                && outLine.Data != "^C"
                && !outLine.Data.Contains("UserWarning")
                && !outLine.Data.Contains("nn."))
            {
                if (Regex.IsMatch(outLine.Data, "^[0-9]+ .*$"))
                {
                    ReportProgressThreadSafe();
                    WriteToLogsThreadSafe(outLine.Data, System.Drawing.Color.LightGreen);
                }
                else
                    WriteToLogsThreadSafe(outLine.Data);
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

        //async Task CheckNewReleases()
        //{
        //    var checker = new UpdateChecker("ptrsuder", GitHubRepoName, AppVersion, GitHubBranch);
        //    //ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        //    UpdateType update = await checker.CheckUpdate();

        //    switch (update)
        //    {
        //        case UpdateType.None:
        //            WriteToLogsThreadSafe("NO NEW RELEASES");
        //            break;
        //        case UpdateType.Fail:
        //            WriteToLogsThreadSafe(checker.ErrorMessage);
        //            break;
        //        default:
        //            WriteToLogsThreadSafe("NEW RELEASE IS AVAILABLE ON GITHUB");
        //            WriteToLogsThreadSafe(@"https://github.com/ptrsuder/crop-upscale-merge/releases");
        //            //if (await Application.Current.MainWindow.ShowDialog<bool>(Application.Current.MainWindow))
        //            //    Helper.OpenBrowser(@"https://github.com/ptrsuder/crop-upscale-merge/releases");
        //            break;
        //    }
        //}

    }    

    public class ImageFormatInfo
    {
        private string _extension;
        public string Extension
        {
            get => _extension;
            private set
            {
                _extension = value;
                DisplayName = _extension.ToUpper().Remove(0,1);
                VipsNative = losslessFormats.Contains(_extension);                
            }
        }
        public string DisplayName { get; set; }
        public bool VipsNative { get; private set; }
        public bool Lossless;
        public int CompressionFactor;
        public string CompressionMethod;

        public ImageFormatInfo(string extension)
        {
            Extension = extension;
        }

        public override string ToString()
        {
            return Extension;
        }

        static public bool IsVipsNative(string extension)
        {
            return losslessFormats.Contains(extension);
        }

        static List<string> losslessFormats = new List<string>() { ".png", ".tiff", ".webp" };
        static List<string> convertFormats = new List<string>() { ".tga", ".dds", ".jpg", ".bmp" };
    }
}
