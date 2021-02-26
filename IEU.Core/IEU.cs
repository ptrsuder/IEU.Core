using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using DdsFileTypePlus;
using DynamicData;
using ImageEnhancingUtility.BasicSR;
using ImageEnhancingUtility.Core.Utility;
using ImageMagick;
using NetVips;
using Newtonsoft.Json;
using ProtoBuf;
using PaintDotNet;
using ReactiveUI;
using Color = System.Drawing.Color;
using Image = NetVips.Image;
using Path = System.IO.Path;
using ReactiveCommand = ReactiveUI.ReactiveCommand;
using Unit = System.Reactive.Unit;
using NvAPIWrapper;
using NvAPIWrapper.GPU;

//TODO:
//new filter: (doesn't)have result
//write log file
//identical filenames with different extension
//transfer gui settings to gui project?
[assembly: InternalsVisibleTo("ImageEnhancingUtility.Tests")]
namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    public partial class IEU : ReactiveObject
    {
        public readonly string AppVersion = "0.13.00";
        public readonly string GitHubRepoName = "IEU.Core";

        private int _overwriteMode = 0;       
        public int OverwriteMode
        {
            get => _overwriteMode;
            set => this.RaiseAndSetIfChanged(ref _overwriteMode, value);
        }

        private bool _preciseTileResolution = false;      
        public bool PreciseTileResolution
        {
            get => _preciseTileResolution;
            set
            {
                OverlapSize = 0;
                this.RaiseAndSetIfChanged(ref _preciseTileResolution, value);
            }
        }

        public string Name = "";      

        Dictionary<string, Dictionary<string, string>> lrDict = new Dictionary<string, Dictionary<string, string>>();
        [Category("Exposed")]
        public Dictionary<string, Dictionary<string, string>> LRDict
        {
            get => lrDict;
        }
        Dictionary<string, Dictionary<string, MagickImage>> hrDict = new Dictionary<string, Dictionary<string, MagickImage>>();
        [Category("Exposed")]
        public Dictionary<string, Dictionary<string, MagickImage>> HRDict
        {
            get => hrDict;
        }

        bool _noNvidia = false;
        public bool NoNvidia
        {
            get => _noNvidia;
            set
            {
                AutoSetTileSizeEnable = false;
                VramMonitorEnable = false;
                this.RaiseAndSetIfChanged(ref _noNvidia, value);
            }
        }

        public bool UseJoey = false;

        #region FIELDS/PROPERTIES       

        [Category("Exposed")]
        public int SeamlessExpandSize { get; set; } = 16;

        [Category("Exposed")]
        public bool HidePythonProcess { get; set; } = true;

        readonly bool IsSub = false;

        public static readonly string[] NoiseReductionTypes = new string[] {
             "None", "Enhance", "Despeckle", "Adaptive blur"};

        int hotModelUpscaleSize = 0;

        public SourceList<ModelInfo> ModelsItems = new SourceList<ModelInfo>();

        private List<ModelInfo> _selectedModelsItems = new List<ModelInfo>();
        public List<ModelInfo> SelectedModelsItems
        {
            get => _selectedModelsItems;
            set
            {
                if (value.Count > 1 && _selectedModelsItems.Count <= 1) // from single model to mult
                    OutputDestinationModes = Dictionaries.OutputDestinationModesMultModels;
                if (value.Count <= 1 && _selectedModelsItems.Count > 1) // from mult models to single
                {
                    //int temp = OutputDestinationMode;
                    OutputDestinationModes = Dictionaries.OutputDestinationModesSingleModel;
                    //OutputDestinationMode = temp;
                }
                this.RaiseAndSetIfChanged(ref _selectedModelsItems, value);
            }
        }
        public List<ModelInfo> checkedModels;


        #region FOLDER_PATHS
        private string _esrganPath = "";
        [ProtoMember(4)]
        public string EsrganPath
        {
            get => _esrganPath;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (string.IsNullOrEmpty(ModelsPath))
                        ModelsPath = $"{value}{DirSeparator}models";
                    if (string.IsNullOrEmpty(LrPath))
                        LrPath = $"{value}{DirSeparator}LR";
                    if (string.IsNullOrEmpty(ResultsPath))
                        ResultsPath = $"{value}{DirSeparator}results";
                    if (string.IsNullOrEmpty(InputDirectoryPath))
                        InputDirectoryPath = $"{value}{DirSeparator}IEU_input";
                    if (!Directory.Exists(InputDirectoryPath))
                        Directory.CreateDirectory(InputDirectoryPath);
                    if (string.IsNullOrEmpty(OutputDirectoryPath))
                        OutputDirectoryPath = $"{value}{DirSeparator}IEU_output";
                    if (!Directory.Exists(OutputDirectoryPath))
                        Directory.CreateDirectory(OutputDirectoryPath);
                    this.RaiseAndSetIfChanged(ref _esrganPath, value);
                }
            }
        }

        private string _modelsPath = "";
        [ProtoMember(5)]
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
                        LastModelForAlphaPath = ModelsItems.Items.ToArray()[0].FullName;
                }
            }
        }
        private string _imgPath = "";
        [ProtoMember(6)]
        public string InputDirectoryPath
        {
            get => _imgPath;
            set => this.RaiseAndSetIfChanged(ref _imgPath, value);
        }
        private string _resultsMergedPath = "";
        [ProtoMember(7)]
        public string OutputDirectoryPath
        {
            get => _resultsMergedPath;
            set => this.RaiseAndSetIfChanged(ref _resultsMergedPath, value);
        }
        private string _lrPath = "";
        [ProtoMember(8)]
        public string LrPath
        {
            get => _lrPath;
            set => this.RaiseAndSetIfChanged(ref _lrPath, value);
        }
        private string _resultsPath = "";
        [ProtoMember(9)]
        public string ResultsPath
        {
            get => _resultsPath;
            set => this.RaiseAndSetIfChanged(ref _resultsPath, value);
        }
        #endregion

        #region SETTINGS

        private int _maxTileResolution = 512 * 380;
        public int MaxTileResolution
        {
            get => _maxTileResolution;
            set => this.RaiseAndSetIfChanged(ref _maxTileResolution, value);
        }

        private int _maxTileResolutionWidth = 512;
        [ProtoMember(10)]
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
        [ProtoMember(11)]
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

        string _lastModelForAlphaPath = "";
        [ProtoMember(18)]
        public string LastModelForAlphaPath
        {
            get => _lastModelForAlphaPath;
            set
            {
                if (value != "" && !File.Exists(value))
                {
                    Logger.Write($"{value} is saved as model for alphas, but it is missing", Color.LightYellow);
                    if (ModelsItems.Count > 0)
                        value = ModelsItems.Items.ToArray()[0].FullName;
                    else
                        value = "";
                }
                _lastModelForAlphaPath = value;
            }
        }

        bool _darkThemeEnabled = false;
        [ProtoMember(19)]
        public bool DarkThemeEnabled
        {
            get => _darkThemeEnabled;
            set => this.RaiseAndSetIfChanged(ref _darkThemeEnabled, value);
        }

        bool _disableRuleSystem = true;
        [ProtoMember(23, IsRequired = true)]
        public bool DisableRuleSystem
        {
            get => _disableRuleSystem;
            set => this.RaiseAndSetIfChanged(ref _disableRuleSystem, value);
        }

        bool _useCondaEnv = false;
        [ProtoMember(25)]
        public bool UseCondaEnv
        {
            get => _useCondaEnv;
            set => this.RaiseAndSetIfChanged(ref _useCondaEnv, value);
        }
        string _condaEnv = "";
        [ProtoMember(26)]
        public string CondaEnv
        {
            get => _condaEnv;
            set => this.RaiseAndSetIfChanged(ref _condaEnv, value);
        }

        private bool _enableBlend = false;
        [ProtoMember(28)]
        public bool EnableBlend
        {
            get => _enableBlend;
            set => this.RaiseAndSetIfChanged(ref _enableBlend, value);
        }

        bool _inMemoryMode = false;
        [ProtoMember(29)]
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

        private bool _vramMonitorEnable = false;
        [ProtoMember(44, IsRequired = true)]
        public bool VramMonitorEnable
        {
            get => _vramMonitorEnable;
            set => this.RaiseAndSetIfChanged(ref _vramMonitorEnable, value);
        }
        private int _vramMonitorFrequency = 500;
        [ProtoMember(45)]
        public int VramMonitorFrequency
        {
            get => _vramMonitorFrequency;
            set => this.RaiseAndSetIfChanged(ref _vramMonitorFrequency, value);
        }
        private bool _autoSetTileSizeEnable = true;
        [ProtoMember(46, IsRequired = true)]
        public bool AutoSetTileSizeEnable
        {
            get => _autoSetTileSizeEnable;
            set => this.RaiseAndSetIfChanged(ref _autoSetTileSizeEnable, value);
        }

        bool _useOldVipsMerge = true;
        [ProtoMember(47, IsRequired = true)]
        public bool UseOldVipsMerge
        {
            get => _useOldVipsMerge;
            set => this.RaiseAndSetIfChanged(ref _useOldVipsMerge, value);
        }

        private int _paddingSize = 0;
        [ProtoMember(53)]
        public int PaddingSize
        {
            get => _paddingSize;
            set => this.RaiseAndSetIfChanged(ref _paddingSize, value);
        }

        #endregion

        #region MAINTAB_PROGRESS       

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
            return new int[] { Interlocked.Increment(ref _filesDone), success ? Interlocked.Increment(ref _filesDoneSuccesfully) : _filesDoneSuccesfully };
        }
        public void ResetDoneCounter() { _filesDone = 0; _filesDoneSuccesfully = 0; }

        private int _filesDoneSuccesfully = 0;
        public int FilesDoneSuccesfully { get { return _filesDoneSuccesfully; } }

        private int _filesTotal = 0;
        public int FilesTotal { get { return _filesTotal; } }
        public int[] IncrementTotalCounter()
        {
            return new int[] { Interlocked.Increment(ref _filesTotal) };
        }
        public void SetTotalCounter(int value) { _filesTotal = value; }
        public void ResetTotalCounter() { _filesTotal = 0; }

        private string _progressLabel = "0/0";
        public string ProgressLabel
        {
            get => _progressLabel;
            set => this.RaiseAndSetIfChanged(ref _progressLabel, value);
        }
        #endregion

        public static List<double> ResizeImageScaleFactors = new List<double>() { 0.25, 0.5, 1.0, 2.0, 4.0 };

        readonly string DirSeparator = Path.DirectorySeparatorChar.ToString();

        public bool GreyscaleModel = false;

        #region RULESET
        public Profile GlobalProfile;
        Profile _currentProfile;
        public Profile CurrentProfile
        {
            get => _currentProfile;
            set => this.RaiseAndSetIfChanged(ref _currentProfile, value);
        }

        public Filter GlobalFilter;
        Filter _currentFilter;
        public Filter CurrentFilter
        {
            get => _currentFilter;
            set => this.RaiseAndSetIfChanged(ref _currentFilter, value);
        }

        [ProtoMember(20)]
        readonly List<Profile> _profiles = new List<Profile>();
        public SourceList<Profile> Profiles = new SourceList<Profile>();

        [ProtoMember(21)]
        readonly List<Filter> _filters = new List<Filter>();
        public SourceList<Filter> Filters = new SourceList<Filter>();

        [ProtoMember(22)]
        public SortedDictionary<int, Rule> Ruleset = new SortedDictionary<int, Rule>(new RulePriority());
        public Rule GlobalRule;

        public string SaveProfileName = "NewProfile";
        #endregion

        #endregion

        public ReactiveCommand<Tuple<FileInfo[], Profile>, Unit> SplitCommand { get; }
        public ReactiveCommand<Tuple<bool, Profile>, bool> UpscaleCommand { get; }
        public ReactiveCommand<Unit, Unit> MergeCommand { get; }
        public ReactiveCommand<Unit, Unit> SplitUpscaleMergeCommand { get; }

        #region CONSTRUCTOR       
       
        public IEU(bool isSub = false)
        {
            IsSub = isSub;
            Task splitFunc(Tuple<FileInfo[], Profile> x) => Split();
            SplitCommand = ReactiveCommand.CreateFromTask((Func<Tuple<FileInfo[], Profile>, Task>)splitFunc);

            Task<bool> upscaleFunc(Tuple<bool, Profile> x) => Upscale(x != null && x.Item1, x?.Item2);
            UpscaleCommand = ReactiveCommand.CreateFromTask((Func<Tuple<bool, Profile>, Task<bool>>)upscaleFunc);

            MergeCommand = ReactiveCommand.CreateFromTask(Merge);

            SplitUpscaleMergeCommand = ReactiveCommand.CreateFromTask(SplitUpscaleMerge);

            Logger.Write(RuntimeInformation.OSDescription);
            Logger.Write(RuntimeInformation.FrameworkDescription);

            if (!IsSub)
                ReadSettings();

            if (_profiles.Count == 0)
                AddProfile(new Profile("Global"));
            else
                Profiles.AddRange(_profiles);

            if (_filters.Count == 0)
                AddFilter(new Filter("Global"));
            else
                Filters.AddRange(_filters);

            GlobalProfile = Profiles.Items.FirstOrDefault();
            GlobalFilter = Filters.Items.FirstOrDefault();
            CurrentProfile = GlobalProfile.Clone() as Profile;
            CurrentFilter = GlobalFilter.Clone() as Filter;
            GlobalRule = new Rule("Global", GlobalProfile, GlobalFilter) { Priority = 0 };
            if (Ruleset.Count == 0)
                Ruleset.Add(0, GlobalRule);
            else
                Ruleset[0] = GlobalRule;
            Ruleset = new SortedDictionary<int, Rule>(Ruleset, new RulePriority());

            try
            {
                gpuMonitor = new GpuMonitor(Logger);
                gpuMonitor.GetVRAM();
            }
            catch
            {
                Logger.Write("Failed to get Nvidia GPU info.");
                NoNvidia = true;
            }
        }
        
        #endregion

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

        public void CreateModelTree()
        {
            List<ModelInfo> newList = new List<ModelInfo>();
            if (IsSub)
                return;
            DirectoryInfo di = new DirectoryInfo(ModelsPath);
            if (!di.Exists)
            {
                Logger.Write($"{di.FullName} doesn't exist!");
                return;
            }

            foreach (DirectoryInfo d in di.GetDirectories("*", SearchOption.TopDirectoryOnly))
                foreach (FileInfo fi in d.GetFiles("*.pth", SearchOption.TopDirectoryOnly))
                    newList.Add(new ModelInfo(fi.Name, fi.FullName, d.Name));

            foreach (FileInfo fi in di.GetFiles("*.pth", SearchOption.TopDirectoryOnly))
                newList.Add(new ModelInfo(fi.Name, fi.FullName));
            ModelsItems.Clear();
            ModelsItems.AddRange(newList);
        }
        public void GetCheckedModels()
        {
            checkedModels = SelectedModelsItems;
            for (int i = 0; i < checkedModels.Count; i++)
            {
                checkedModels[i].Priority = i;
            }
            if (checkedModels.Count == 0)
                Logger.Write("No models selected!");
        }

        public void ChangeModelPriority(ModelInfo model, int newPriority)
        {
            ModelInfo temp = checkedModels[newPriority];
            checkedModels[newPriority] = model;
            checkedModels[model.Priority] = temp;
            temp.Priority = model.Priority;
            model.Priority = newPriority;
        }

        #region RULESET

        public void AddProfile(string name)
        {
            Profile oldProfile = Profiles.Items.Where(x => x.Name == name).FirstOrDefault() as Profile;
            _profiles.Remove(oldProfile);
            Profiles.Remove(oldProfile);

            Profile newProfile = CurrentProfile.Clone();
            newProfile.Name = name;

            if (name == "Global")
            {
                Profiles.Insert(0, newProfile);
                _profiles.Insert(0, newProfile);
            }
            else
                AddProfile(newProfile);
        }

        private void AddProfile(Profile newProfile)
        {
            Profiles.Add(newProfile);
            _profiles.Add(newProfile);
        }

        public void LoadProfile(Profile profile)
        {
            CurrentProfile = profile.Clone();
        }

        public void DeleteProfile(Profile profile)
        {
            if (Profiles.Items.Contains(profile))
            {
                Profiles.Remove(profile);
                _profiles.Remove(profile);
            }
        }

        public void AddFilter(string name)
        {
            Filter oldProfile = Filters.Items.Where(x => x.Name == name).FirstOrDefault() as Filter;
            _filters.Remove(oldProfile);
            Filters.Remove(oldProfile);

            Filter newProfile = CurrentFilter.Clone() as Filter;
            newProfile.Name = name;

            if (name == "Global")
            {
                Filters.Insert(0, newProfile);
                _filters.Insert(0, newProfile);
            }
            else
                AddFilter(newProfile);
        }

        private void AddFilter(Filter newFilter)
        {
            Filters.Add(newFilter);
            _filters.Add(newFilter);
        }

        public void LoadFilter(Filter filter)
        {
            CurrentFilter = filter.Clone() as Filter;
        }

        public void DeleteFilter(Filter profile)
        {
            if (Filters.Items.Contains(profile))
            {
                Filters.Remove(profile);
                _filters.Remove(profile);
            }
        }

        public void AddRule(string name, Profile profile, Filter filter)
        {
            KeyValuePair<int, Rule> oldProfile = (KeyValuePair<int, Rule>)Ruleset.Where(x => x.Value.Name == name).FirstOrDefault();
            Ruleset.Remove(Ruleset.IndexOf(oldProfile));
            Ruleset.Add(Ruleset.Count, new Rule(name, profile, filter) { Priority = Ruleset.Count });
            //Ruleset = new SortedDictionary<int, Rule>(Ruleset);
        }

        public void DeleteRule(Rule profile)
        {
            if (Ruleset.Values.Contains(profile))
            {
                KeyValuePair<int, Rule> oldProfile = (KeyValuePair<int, Rule>)Ruleset.Where(x => x.Value == profile).FirstOrDefault();
                Ruleset.Remove(oldProfile.Key);
                //Ruleset = new SortedDictionary<int, Rule>(Ruleset);
            }
        }

        public void ChangeRulePriority(Rule rule, int newPriority)
        {
            Rule temp = Ruleset[newPriority];
            Ruleset[newPriority] = rule;
            Ruleset[rule.Priority] = temp;
            temp.Priority = rule.Priority;
            rule.Priority = newPriority;
        }

        #endregion

        #region PROGRESS/LOG
        public Logger Logger = new Logger();
        private void ReportProgress()
        {
            double fdd = (double)FilesDone;
            if (FilesDone == 0 && FilesTotal != 0)
                fdd = 0.001;
            ProgressBarValue = (fdd / (double)FilesTotal) * 100.00;
            ProgressLabel = $@"{FilesDone}/{FilesTotal}";
        }
        #endregion
               
        [Category("Exposed")]
        [ProtoMember(52)]
        public int MaxConcurrency { get; set; } = 99;

        #region SPLIT    

        void CreateTiles(FileInfo file, MagickImage inputImage, bool imageHasAlpha, Profile HotProfile)
        {
            var values = new ImageValues();
            values.Path = file.FullName;
            values.Dimensions = new int[] { inputImage.Width, inputImage.Height };
            values.UseAlpha = imageHasAlpha && !HotProfile.IgnoreAlpha;

            FileInfo fileAlpha = new FileInfo(file.DirectoryName + DirSeparator + Path.GetFileNameWithoutExtension(file.Name) + "_alpha.png");
            string lrPathAlpha = LrPath + "_alpha";
            int imageWidth = inputImage.Width, imageHeight = inputImage.Height;
            MagickImage inputImageRed = null, inputImageGreen = null, inputImageBlue = null, inputImageAlpha = null;

            int[] tiles;
            int[] paddedDimensions = new int[] { imageWidth, imageHeight };

            if (PreciseTileResolution)
            {
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolutionWidth, MaxTileResolutionHeight);
                if (tiles[0] == 0 || tiles[1] == 0)
                {
                    Logger.Write(file.Name + " resolution is smaller than specified tile size");
                    return;
                }
            }
            else
            {
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolution);
                values.Columns = tiles[0];
                values.Rows = tiles[1];
                values.CropToDimensions = new int[] { imageWidth, imageHeight };
                paddedDimensions = GetPaddedDimensions(imageWidth, imageHeight, tiles, ref values, HotProfile);
                inputImage = ImageOperations.PadImage(inputImage, paddedDimensions[0], paddedDimensions[1]);                
            }

            ImageOperations.ImagePreprocess(ref inputImage, ref values, HotProfile, Logger);
            //if (inputImageAlpha != null && imageHasAlpha == true)
            //    ImagePreprocess(ref inputImageAlpha, paddedDimensions, HotProfile);

            imageWidth = inputImage.Width;
            imageHeight = inputImage.Height;
            tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolution);

            values.FinalDimensions = new int[] { inputImage.Width, inputImage.Height };
            values.Columns = tiles[0];
            values.Rows = tiles[1];

            if (PaddingSize > 0)
            {
                Image im = ImageOperations.ConvertToVips(inputImage); //TODO: open from file in the beginning              
                //im = Image.NewFromFile(file.FullName);      
                im = im.Embed(PaddingSize, PaddingSize, im.Width + 2 * PaddingSize, im.Height + 2 * PaddingSize, "VIPS_EXTEND_COPY");
                inputImage = ImageOperations.ConvertToMagickImage(im);
                values.FinalDimensions = new int[] { inputImage.Width, inputImage.Height };
                values.PaddingSize = PaddingSize;
            }

            if (values.UseAlpha)
                inputImageAlpha = (MagickImage)inputImage.Separate(Channels.Alpha).First();

            inputImage.HasAlpha = false;

            int seamlessPadding = 0;
            if (HotProfile.SeamlessTexture)
            {
                inputImage = ImageOperations.ExpandTiledTexture(inputImage, ref seamlessPadding);
                values.FinalDimensions = new int[] { inputImage.Width + 2 * seamlessPadding, inputImage.Height + 2 * seamlessPadding };
            }

            if (values.UseAlpha)
            {
                if (HotProfile.UseFilterForAlpha)
                    imageHasAlpha = false;
                else
                {
                    bool isSolidColor = inputImageAlpha.TotalColors == 1;
                    //if (isSolidColor)
                    //{
                    //    var hist = inputImageAlpha.Histogram();
                    //    isSolidColor = hist.ContainsKey(new MagickColor("#FFFFFF")) || hist.ContainsKey(new MagickColor("#000000"));
                    //}
                    values.AlphaSolidColor = isSolidColor;

                    if (HotProfile.IgnoreSingleColorAlphas && isSolidColor)
                    {
                        inputImageAlpha.Dispose();
                        inputImageAlpha = null;
                        imageHasAlpha = false;
                        values.UseAlpha = false;
                    }
                    else
                    {
                        if (HotProfile.SeamlessTexture)
                            inputImageAlpha = ImageOperations.ExpandTiledTexture(inputImageAlpha, ref seamlessPadding);
                    }
                }
            }

            if (HotProfile.SplitRGB)
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
                    Logger.Write($"{file.Name}: not RGB");
                    return;
                }
            }

            #region CREATE TILES

            int tileWidth = imageWidth / tiles[0];
            int tileHeight = imageHeight / tiles[1];

            values.TileW = tileWidth;
            values.TileH = tileHeight;

            bool addColumn = false, addRow = false;
            int rows = tiles[1], columns = tiles[0];
            if (PreciseTileResolution)
            {
                tileWidth = MaxTileResolutionWidth;
                tileHeight = MaxTileResolutionHeight;
                if (imageWidth % tileWidth >= 16)
                {
                    addColumn = true;
                    columns++;
                }
                if (imageHeight % tileHeight >= 16)
                {
                    addRow = true;
                    rows++;
                }
            }
            int rightOverlap, bottomOverlap;

            Directory.CreateDirectory($"{LrPath}{DirSeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}");
            Dictionary<string, string> lrImages = new Dictionary<string, string>();
            if (InMemoryMode)
                lrDict.Add(file.FullName, lrImages);

            int lastIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (row < rows - 1)
                        bottomOverlap = 1;
                    else
                        bottomOverlap = 0;
                    if (col < columns - 1)
                        rightOverlap = 1;
                    else
                        rightOverlap = 0;

                    int tileIndex = row * columns + col;
                    int xOffset = rightOverlap * OverlapSize;
                    int yOffset = bottomOverlap * OverlapSize;
                    int tile_X1 = col * tileWidth;
                    int tile_Y1 = row * tileHeight;

                    if (addColumn && col == columns - 1)
                        tile_X1 = imageWidth - tileWidth;
                    if (addRow && row == rows - 1)
                        tile_Y1 = imageHeight - tileHeight;

                    var cropRectangle = new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset + (PaddingSize + seamlessPadding) * 2, tileHeight + yOffset + (PaddingSize + seamlessPadding) * 2);

                    if (imageHasAlpha && !HotProfile.IgnoreAlpha) //crop alpha
                    {
                        MagickImage outputImageAlpha = (MagickImage)inputImageAlpha.Clone();
                        outputImageAlpha.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset + PaddingSize * 2, tileHeight + yOffset + PaddingSize * 2));
                        string lrAlphaFolderPath = $"{lrPathAlpha}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}";
                        if (InMemoryMode)
                        {
                            if (HotProfile.UseDifferentModelForAlpha)
                            {
                                var outPath = $"{lrAlphaFolderPath}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}{file.Extension}";
                                lrImages.Add(outPath, outputImageAlpha.ToBase64());
                            }
                            else
                            {
                                var outPath = $"{LrPath}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}{file.Extension}";
                                lrImages.Add(outPath, outputImageAlpha.ToBase64());
                            }
                        }
                        else
                        {
                            if (HotProfile.UseDifferentModelForAlpha)
                            {
                                Directory.CreateDirectory(lrAlphaFolderPath);
                                outputImageAlpha.Write($"{lrAlphaFolderPath}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}.png");
                            }
                            else
                                outputImageAlpha.Write($"{LrPath}{DirSeparator}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}.png");
                        }
                    }
                    if (HotProfile.SplitRGB)
                    {
                        var pathBase = $"{LrPath}{DirSeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{Path.GetFileNameWithoutExtension(file.Name)}";
                        if(OutputDestinationMode == 3)
                            pathBase = $"{LrPath}" + file.FullName.Replace(InputDirectoryPath, "").Replace(file.Name, Path.GetFileNameWithoutExtension(file.Name));
                        
                        var pathR = $"{pathBase}_R_tile-{tileIndex:D2}.png";
                        var pathG = $"{pathBase}_G_tile-{tileIndex:D2}.png";
                        var pathB = $"{pathBase}_B_tile-{tileIndex:D2}.png";

                        MagickImage outputImageRed = (MagickImage)inputImageRed.Clone();
                        outputImageRed.Crop(cropRectangle);
                        MagickImage outputImageGreen = (MagickImage)inputImageGreen.Clone();
                        outputImageGreen.Crop(cropRectangle);
                        MagickImage outputImageBlue = (MagickImage)inputImageBlue.Clone();
                        outputImageBlue.Crop(cropRectangle);

                        if (InMemoryMode)
                        {                           
                            lrImages.Add(Path.ChangeExtension(pathR, file.Extension), outputImageRed.ToBase64());
                            lrImages.Add(Path.ChangeExtension(pathG, file.Extension), outputImageGreen.ToBase64());
                            lrImages.Add(Path.ChangeExtension(pathB, file.Extension), outputImageBlue.ToBase64());
                        }
                        else
                        {
                            outputImageRed.Write(pathR);
                            outputImageGreen.Write(pathG);
                            outputImageBlue.Write(pathB);
                        }
                    }
                    else
                    {
                        MagickImage outputImage = (MagickImage)inputImage.Clone();

                        outputImage.Crop(cropRectangle);
                        MagickFormat format = MagickFormat.Png24;
                        var dirpath = Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "");
                        string outPath = $"{LrPath}{dirpath}{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}_tile-{tileIndex:D2}.png";
                        if (!InMemoryMode)
                            outputImage.Write(outPath, format);
                        else
                        {
                            outPath = $"{LrPath}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}_tile-{tileIndex:D2}{file.Extension}";
                            lrImages.Add(outPath, outputImage.ToBase64());
                        }
                        lastIndex = tileIndex;
                    }
                }
            }
            #endregion

            var files = Directory.GetFiles(ResultsPath, "*", SearchOption.AllDirectories);
            var basename = Path.GetFileNameWithoutExtension(file.Name);

            foreach (var f in files)
            {
                //remove leftover old tiles from results
                var match = Regex.Match(Path.GetFileNameWithoutExtension(f), $"({basename}_tile-)([0-9]*)");
                string t = match.Groups[2].Value;
                if (t == "") continue;
                if (Int32.Parse(t) > lastIndex)
                    File.Delete(f);
            }

            string basePath = "";  

            if(UseModelChain)
            {
                basePath = DirSeparator + Path.GetFileNameWithoutExtension(file.Name);   
                values.results.Add(new UpscaleResult(basePath, checkedModels.Last()));
            }
            else
                foreach (var model in checkedModels)
            {
                if (OutputDestinationMode == 0)
                    basePath = DirSeparator + Path.GetFileNameWithoutExtension(file.Name);

                if (OutputDestinationMode == 3)
                    basePath = file.FullName.Replace(InputDirectoryPath, "").Replace(file.Name, Path.GetFileNameWithoutExtension(file.Name));

                if (OutputDestinationMode == 1)
                {
                    if (HotProfile.SplitRGB) //search for initial tiles in _R folder                    
                        basePath = $"{DirSeparator}Images{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}_ChannelChar{DirSeparator}" +
                                  $"{DirSeparator}[{Path.GetFileNameWithoutExtension(model.Name)}]_{Path.GetFileNameWithoutExtension(file.Name)}";
                    else                    
                        basePath = $"{DirSeparator}Images{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}" +
                                $"{DirSeparator}[{Path.GetFileNameWithoutExtension(model.Name)}]_{Path.GetFileNameWithoutExtension(file.Name)}";                    
                }
                if (OutputDestinationMode == 2)
                {
                    if (HotProfile.SplitRGB) //search for initial tiles in _R folder                    
                        basePath = $"{DirSeparator}Models{DirSeparator}{Path.GetFileNameWithoutExtension(model.Name)}" +
                                  $"{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}";
                    else
                        basePath = $"{DirSeparator}Models{DirSeparator}{Path.GetFileNameWithoutExtension(model.Name)}" +
                            $"{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}";                      
                }
                values.results.Add(new UpscaleResult(basePath, model));
            }
          
            values.profile1 = HotProfile;
            if(!batchValues.images.ContainsKey(values.Path))
                batchValues.images.Add(values.Path, values);

            inputImage.Dispose();

            Logger.Write($"{file.Name} SPLIT DONE", Color.LightGreen);
        }
        void SplitTask(FileInfo file, Profile HotProfile)
        {
            MagickImage image;
            bool imageHasAlpha = false;

            try
            {
                image = ImageOperations.LoadImage(file);
                imageHasAlpha = image.HasAlpha;
            }
            catch (Exception ex)
            {
                Logger.Write($"Failed to read file {file.Name}!", Color.Red);
                Logger.Write(ex.Message);
                return;
            }

            CreateTiles(file, image, imageHasAlpha, HotProfile);
            if (!InMemoryMode)
            {
                IncrementDoneCounter();
                ReportProgress();
            }
            GC.Collect();
        }

        async public Task Split(FileInfo[] inputFiles = null)
        {
            if (AutoSetTileSizeEnable)
                await AutoSetTileSize();

            if (!IsSub)            
                SaveSettings();                           

            checkedModels = SelectedModelsItems;
            foreach (var model in checkedModels)
                model.UpscaleFactor = await DetectModelUpscaleFactor(model);

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;

            DirectoryInfo inputDirectory = new DirectoryInfo(InputDirectoryPath);
            DirectoryInfo lrDirectory = new DirectoryInfo(LrPath);
            FileInfo[] inputDirectoryFiles = inputDirectory.GetFiles("*", searchOption)
               .Where(x => ImageFormatInfo.ImageExtensions.Contains(x.Extension.ToUpperInvariant())).ToArray();
            if (inputDirectoryFiles.Count() == 0)
            {
                Logger.Write("No files in input folder!", Color.Red);
                return;
            }

            DirectoryInfo lrAlphaDirectory = new DirectoryInfo(LrPath + "_alpha");
            if (lrDirectory.Exists)
            {
                lrDirectory.Delete(true);
                Logger.Write($"'{LrPath}' is cleared", Color.LightBlue);
            }
            else
                lrDirectory.Create();


            if (!lrAlphaDirectory.Exists)
                lrAlphaDirectory.Create();
            else
            {
                lrAlphaDirectory.Delete(true);
                Logger.Write($"'{LrPath + "_alpha"}' is cleared", Color.LightBlue);
            }

            Logger.Write("Creating tiles...");

            if (inputFiles == null)
                inputFiles = inputDirectoryFiles;

            if (!InMemoryMode)
            {
                ResetDoneCounter();
                SetTotalCounter(inputFiles.Length);
                ReportProgress();
            }

            batchValues = new BatchValues()
            {
                MaxTileResolution = MaxTileResolution,
                MaxTileH = MaxTileResolutionHeight,
                MaxTileW = MaxTileResolutionWidth,
                OutputMode = OutputDestinationMode,
                OverwriteMode = OverwriteMode,
                OverlapSize = OverlapSize,
                Padding = PaddingSize,
                UseModelChain = UseModelChain,
                ModelChain = checkedModels
                //Seamless = 
            };            

            await Task.Run(() => Parallel.ForEach(inputFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = MaxConcurrency }, file =>
            {
                if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper()))
                    return;
                bool fileSkipped = true;
                List<Rule> rules = new List<Rule>(Ruleset.Values);
                if (DisableRuleSystem)
                    rules = new List<Rule> { new Rule("Simple rule", CurrentProfile, CurrentFilter) };

                foreach (var rule in rules)
                {
                    if (rule.Filter.ApplyFilter(file))
                    {
                        SplitTask(file, rule.Profile);
                        fileSkipped = false;
                        break;
                    }
                }
                if (fileSkipped)
                {
                    IncrementDoneCounter(false);
                    Logger.Write($"{file.Name} is filtered, skipping", Color.HotPink);
                    return;
                }
            }));

            WriteBatchValues(batchValues);
            if (!InMemoryMode)
                Logger.Write("Finished!", Color.LightGreen);
        }

        public async Task Split(FileInfo file)
        {
            Logger.Write($"{file.Name} SPLIT START");

            if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper()))
                return;
            bool fileSkipped = true;
            List<Rule> rules = new List<Rule>(Ruleset.Values);
            if (DisableRuleSystem)
                rules = new List<Rule> { new Rule("Simple rule", CurrentProfile, CurrentFilter) };

            checkedModels = SelectedModelsItems;
            foreach (var model in checkedModels)
            {
                model.UpscaleFactor = await DetectModelUpscaleFactor(model);
            }

            foreach (var rule in rules)
            {
                if (rule.Filter.ApplyFilter(file))
                {
                    await Task.Run(() => SplitTask(file, rule.Profile));
                    fileSkipped = false;
                    break;
                }
            }
            if (fileSkipped)
            {
                IncrementDoneCounter(false);
                Logger.Write($"{file.Name} is filtered, skipping", Color.HotPink);
                return;
            }
        }

        void WriteBatchValues(BatchValues batchValues)
        {
            File.WriteAllText(@"CurrentSession.json", JsonConvert.SerializeObject(batchValues));
        }

        BatchValues ReadBatchValues()
        {           
            var batch = JsonConvert.DeserializeObject<BatchValues>(File.ReadAllText(@"CurrentSession.json"));
            OutputDestinationMode = batch.OutputMode;
            OverwriteMode = batch.OverwriteMode;
            MaxTileResolutionWidth = batch.MaxTileW;
            MaxTileResolutionHeight = batch.MaxTileH;
            MaxTileResolution = batch.MaxTileResolution;
            OverlapSize = batch.OverlapSize;
            PaddingSize = batch.Padding;
            ResultSuffix = batch.ResultSuffix;

            return batch;
        }

        #endregion

        #region MERGE
        
        int[] GetPaddedDimensions(int imageWidth, int imageHeight, int[] tiles, ref ImageValues values, Profile HotProfile)
        {
            int[] paddedDimensions = new int[] { imageWidth, imageHeight };

            if (HotProfile.ResizeImageBeforeScaleFactor != 1.0)
            {
                //make sure that after downscale no pixels will be lost
                double scale = HotProfile.ResizeImageBeforeScaleFactor;
                int divider = scale < 1 ? (int)(1 / scale) : 1;
                bool dimensionsAreOK = imageWidth % divider == 0 && imageHeight % divider == 0;
                if (!dimensionsAreOK && !HotProfile.SeamlessTexture)                
                    paddedDimensions = Helper.GetGoodDimensions(imageWidth, imageHeight, divider, divider);
                
                imageWidth = (int)(paddedDimensions[0] * HotProfile.ResizeImageBeforeScaleFactor);
                imageHeight = (int)(paddedDimensions[1] * HotProfile.ResizeImageBeforeScaleFactor);
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolution);
                values.CropToDimensions = new int[] { paddedDimensions[0], paddedDimensions[1] };
            }

            if (imageWidth * imageHeight > MaxTileResolution)
            {
                //make sure that image can be tiled without leftover pixels
                bool dimensionsAreOK = imageWidth % tiles[0] == 0 && imageHeight % tiles[1] == 0;

                if (!dimensionsAreOK && !HotProfile.SeamlessTexture)
                {
                    paddedDimensions = Helper.GetGoodDimensions(paddedDimensions[0], paddedDimensions[1], tiles[0], tiles[1]);
                }
            }
            return paddedDimensions;
        }

        void ExtractTiledTexture(ref Image imageResult, int upscaleModificator, int expandSize)
        {
            int edgeSize = upscaleModificator * expandSize;
            Image temp = imageResult.Copy();
            imageResult = temp.ExtractArea(edgeSize, edgeSize, imageResult.Width - edgeSize * 2, imageResult.Height - edgeSize * 2);
            temp.Dispose();
        }

        MagickImage ExtractTiledTexture(MagickImage imageResult, int upscaleModificator, int expandSize)
        {
            int edgeSize = upscaleModificator * expandSize;
            MagickImage tempImage = new MagickImage(imageResult);
            tempImage.Crop(imageResult.Width - edgeSize * 2, imageResult.Height - edgeSize * 2, Gravity.Center);
            //(edgeSize, edgeSize, imageResult.Width - edgeSize * 2, imageResult.Height - edgeSize * 2);
            tempImage.RePage();
            return tempImage;
        }

        Image JoinRGB(Tuple<string, MagickImage> pathImage, UpscaleResult result, int tileIndex, string resultSuffix, ref List<FileInfo> tileFilesToDelete)
        {
            Image imageNextTileR, imageNextTileG, imageNextTileB;
            FileInfo tileR, tileG, tileB;
            if (OutputDestinationMode == 1)
            {
                tileR = new FileInfo($"{ResultsPath + result.BasePath.Replace("_ChannelChar", "_R")}_R_tile-{tileIndex:D2}{resultSuffix}.png");
                tileG = new FileInfo($"{ResultsPath + result.BasePath.Replace("_ChannelChar", "_G")}_G_tile-{tileIndex:D2}{resultSuffix}.png");
                tileB = new FileInfo($"{ResultsPath + result.BasePath.Replace("_ChannelChar", "_B")}_B_tile-{tileIndex:D2}{resultSuffix}.png");
            }
            else
            {
                tileR = new FileInfo($"{ResultsPath + result.BasePath}_R_tile-{tileIndex:D2}{resultSuffix}.png");
                tileG = new FileInfo($"{ResultsPath + result.BasePath}_G_tile-{tileIndex:D2}{resultSuffix}.png");
                tileB = new FileInfo($"{ResultsPath + result.BasePath}_B_tile-{tileIndex:D2}{resultSuffix}.png");
            }
            if (InMemoryMode)
            {
                var hrTiles = hrDict[pathImage.Item1];
                imageNextTileR = ImageOperations.ConvertToVips(hrTiles[tileR.FullName])[0];
                imageNextTileG = ImageOperations.ConvertToVips(hrTiles[tileG.FullName])[0];
                imageNextTileB = ImageOperations.ConvertToVips(hrTiles[tileB.FullName])[0];
            }
            else
            {
                imageNextTileR = Image.NewFromFile(tileR.FullName, false, Enums.Access.Sequential)[0];
                tileFilesToDelete.Add(tileR);
                imageNextTileG = Image.NewFromFile(tileG.FullName, false, Enums.Access.Sequential)[0];
                tileFilesToDelete.Add(tileG);
                imageNextTileB = Image.NewFromFile(tileB.FullName, false, Enums.Access.Sequential)[0];
                tileFilesToDelete.Add(tileB);
            }
            return imageNextTileR.Bandjoin(new Image[] { imageNextTileG, imageNextTileB }).Copy(interpretation: "srgb").Cast("uchar"); ;
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
            catch (Exception ex)
            {
                cancelGlobalbalance = true;
                if (ex.HResult == -2146233088)
                    Logger.Write($"{filename}: globabalance is canceled", Color.LightYellow);
                else
                    Logger.Write($"{filename}: {ex.Message}", Color.Red);
            }
        }

        void JoinTiles(ref Image imageRow, Image imageNextTile, string direction, int dx, int dy)
        {
            Logger.WriteDebug("Merging with old vips method");
            int mblendSize = EnableBlend ? OverlapSize : 0;
            Logger.WriteDebug($"mblend: {EnableBlend}");
            imageRow = imageRow.Merge(imageNextTile, direction, dx, dy, mblendSize);
        }

        void JoinTilesNew(ref Image imageRow, Image imageNextTile, bool Copy, string direction, int dx, int dy)
        {
            Logger.WriteDebug("Merging with new vips method");
            int overlap, resultW = imageRow.Width, resultH = imageRow.Height;

            if (direction == Enums.Direction.Horizontal)
            {
                overlap = imageRow.Width + dx;
                resultW += imageNextTile.Width - overlap;
            }
            else
            {
                overlap = imageRow.Height + dy;
                resultH += imageNextTile.Height - overlap;
            }

            Image result = imageRow.Merge(imageNextTile, direction, dx, dy);
            result = result.Resize(4);

            Image maskVips; //= CreateMask(imageRow.Width, imageRow.Height, overlap / 2, direction);           

            int offset = 1;
            Bitmap mask;
            Rectangle brushSize, gradientRectangle;
            LinearGradientMode brushDirection;
            if (direction == Enums.Direction.Horizontal)
            {
                brushSize = new Rectangle(-dx, -dy, overlap, imageRow.Height);
                brushDirection = LinearGradientMode.Horizontal;
                gradientRectangle = new Rectangle(-dx + offset, -dy, overlap - offset, imageNextTile.Height);
            }
            else
            {
                brushSize = new Rectangle(-dx, -dy + offset, imageRow.Width, overlap);
                brushDirection = LinearGradientMode.Vertical;
                gradientRectangle = new Rectangle(-dx, -dy + offset, imageRow.Width, overlap);
            }
            mask = new Bitmap(imageRow.Width, imageRow.Height);

            using (Graphics graphics = Graphics.FromImage(mask))
            using (LinearGradientBrush brush = new LinearGradientBrush(brushSize, Color.White, Color.Black, brushDirection))
            {
                graphics.FillRectangle(Brushes.White, 0, 0, imageRow.Width, imageRow.Height);
                graphics.FillRectangle(brush, gradientRectangle);
                //graphics.FillRectangle(Brushes.Black, 0, 0, imageRow.Width, imageRow.Height);
                //graphics.FillRectangle(Brushes.Black, imageRow.Width - 5, 0, 5, imageRow.Height);
                //graphics.FillRectangle(Brushes.Black, -dx + offset + overlap / 2, 0, overlap / 2, imageRow.Height);
            }
            mask.Save(@"S:\ESRGAN-master\IEU_preview\mask1.png");
            var buffer = ImageOperations.ImageToByte(mask);
            maskVips = Image.NewFromBuffer(buffer).ExtractBand(0);
            //maskVips.WriteToFile(@"S:\ESRGAN-master\IEU_preview\mask1.png");

            //mask2
            Bitmap mask2;
            Rectangle brushSize2, gradientRectangle2;
            LinearGradientMode brushDirection2;
            if (direction == Enums.Direction.Horizontal)
            {
                brushSize2 = new Rectangle(0, 0, overlap / 2, imageNextTile.Height);
                brushDirection2 = LinearGradientMode.Horizontal;
                gradientRectangle2 = new Rectangle(0, 0, overlap / 2, imageNextTile.Height);
            }
            else
            {
                brushSize2 = new Rectangle(-dx, -dy + offset, imageNextTile.Width, overlap / 2);
                brushDirection2 = LinearGradientMode.Vertical;
                gradientRectangle2 = new Rectangle(-dx, -dy + offset, imageNextTile.Width, overlap / 2);
            }
            mask2 = new Bitmap(imageNextTile.Width, imageNextTile.Height);

            using (Graphics graphics = Graphics.FromImage(mask2))
            using (LinearGradientBrush brush = new LinearGradientBrush(brushSize2, Color.Black, Color.White, brushDirection2))
            {
                graphics.FillRectangle(Brushes.White, 0, 0, imageNextTile.Width, imageNextTile.Height);
                graphics.FillRectangle(brush, gradientRectangle2);
            }
            mask2.Save(@"S:\ESRGAN-master\IEU_preview\mask2.png");
            var buffer2 = ImageOperations.ImageToByte(mask2);
            Image maskVips2 = Image.NewFromBuffer(buffer2).ExtractBand(0);
            //imageNextTile = imageNextTile.Bandjoin(new Image[] { maskVips2 });
            //imageNextTile.WriteToFile(@"S:\ESRGAN-master\IEU_preview\nextTile.png");
            Image expandedImage;

            expandedImage = imageRow.Bandjoin(new Image[] { maskVips });

            //if (Copy)
            //    expandedImage = imageRow.Bandjoin(new Image[] { maskVips }).Copy();            
            //else
            //    expandedImage = imageRow.Bandjoin(new Image[] { maskVips }).CopyMemory();

            //expandedImage.WriteToFile(@"S:\ESRGAN-master\IEU_preview\expandedImage.png");
            //imageNextTile.WriteToFile(@"S:\ESRGAN-master\IEU_preview\nextTile.png");

            //result = Image.Black(resultW, resultH).Invert();
            //result = result.Insert(imageNextTile, -dx, -dy);

            result = result.Composite2(imageNextTile, "atop", -dx, -dy).Flatten();
            result = result.Composite2(expandedImage, "atop", 0, 0);
            //result.WriteToFile(@"S:\ESRGAN-master\IEU_preview\result1.png");

            imageRow = result.Flatten();
        }

        Image CreateMask(int w, int h, int overlap, string direction)
        {
            var black = Image.Black(w, h, 1);
            var white = black.Invert();
            Image mask;
            if (direction == Enums.Direction.Horizontal)
                mask = white.Insert(black, w - overlap, 0);
            else
                mask = white.Insert(black, 0, h - overlap);
            mask = mask.Gaussblur(4.6, precision: "float");
            return mask;
        }

        #region IM MERGE
        void JoinTilesNew(ref MagickImage imageRow, MagickImage imageNextTile, string direction, int dx, int dy)
        {
            Logger.WriteDebug("Merging with IM method");
            MagickImage expandedImage = new MagickImage(imageRow);
            int overlap = expandedImage.Width + dx;
            Bitmap mask;
            Rectangle brushSize, gradientRectangle;
            LinearGradientMode brushDirection;
            Gravity tileG = Gravity.East;
            Gravity rowG = Gravity.West;
            int resultW = imageRow.Width, resultH = imageRow.Height;
            int offset = 1;
            if (direction == Enums.Direction.Horizontal)
            {
                overlap = expandedImage.Width + dx;
                brushSize = new Rectangle(-dx, -dy, overlap, imageRow.Height);
                brushDirection = LinearGradientMode.Horizontal;
                gradientRectangle = new Rectangle(-dx + offset, -dy, overlap - offset, imageNextTile.Height);
                resultW += imageNextTile.Width - overlap;
            }
            else
            {
                overlap = expandedImage.Height + dy;
                brushSize = new Rectangle(-dx, -dy, imageRow.Width, overlap);
                brushDirection = LinearGradientMode.Vertical;
                gradientRectangle = new Rectangle(-dx, -dy, imageRow.Width, overlap);
                resultH += imageNextTile.Height - overlap;
                tileG = Gravity.South;
                rowG = Gravity.North;
            }
            mask = new Bitmap(imageRow.Width, imageRow.Height);
            using (Graphics graphics = Graphics.FromImage(mask))
            using (LinearGradientBrush brush = new LinearGradientBrush(brushSize, Color.White, Color.Black, brushDirection))
            {
                graphics.FillRectangle(Brushes.White, 0, 0, imageRow.Width, imageRow.Height);
                graphics.FillRectangle(brush, gradientRectangle);
            }
            mask.Save(@"S:\ESRGAN-master\IEU_preview\mask1.png");

            var buffer = ImageOperations.ImageToByte(mask);
            MagickImage alphaMask = new MagickImage(buffer);
            //alphaMask.Write(@"S:\ESRGAN-master\IEU_preview\alpha_test.png");

            //var readSettings = new MagickReadSettings()
            //{
            //    Width = imageRow.Width,
            //    Height = imageRow.Height
            //};
            //readSettings.SetDefine("gradient:direction", "east");
            //readSettings.SetDefine("gradient:vector", $"{-dx},{0}, {-dx + 16},{0}");
            //var image = new MagickImage("gradient:black-white", readSettings);           

            using (MagickImageCollection images = new MagickImageCollection())
            {
                images.Add(new MagickImage(alphaMask));
                images.Add(new MagickImage(alphaMask));
                images.Add(new MagickImage(alphaMask));
                images.Add(new MagickImage(alphaMask));
                alphaMask = (MagickImage)images.Combine();
            };
            expandedImage.HasAlpha = true;
            expandedImage.Composite(alphaMask, CompositeOperator.CopyAlpha);

            //buffer = ImageOperations.ImageToByte2(new Bitmap(resultW, resultH));
            MagickImage result = new MagickImage(MagickColor.FromRgb(0, 0, 0), resultW, resultH);

            result.Composite(imageNextTile, tileG);
            result.Composite(expandedImage, rowG, CompositeOperator.Over);
            imageRow = new MagickImage(result);
        }
        MagickImage MergeTilesNew(Tuple<string, MagickImage> pathImage, int[] tiles, int[] tileSize, string basePath, string basePathAlpha, string resultSuffix,
                                                                                              List<FileInfo> tileFilesToDelete, bool imageHasAlpha, Profile HotProfile)
        {
            bool alphaReadError = false;
            MagickImage imageResult = null, imageAlphaResult = null;
            FileInfo file = new FileInfo(pathImage.Item1);
            MagickImage imageAlphaRow = null;
            int tileWidth = tileSize[0], tileHeight = tileSize[1];

            Dictionary<string, MagickImage> hrTiles = null;
            if (InMemoryMode)
            {
                hrTiles = hrDict[pathImage.Item1];
            }

            for (int i = 0; i < tiles[1]; i++)
            {
                MagickImage imageRow = null;

                for (int j = 0; j < tiles[0]; j++)
                {
                    int tileIndex = i * tiles[0] + j;

                    MagickImage imageNextTile = null, imageAlphaNextTile;
                    try
                    {
                        if (HotProfile.SplitRGB)
                            Logger.Write("RGB split is unsupported with IM merge, sorry!");
                        //imageNextTile = JoinRGB(pathImage, basePath, Path.GetFileNameWithoutExtension(file.Name), tileIndex, resultSuffix, tileFilesToDelete);
                        else
                        {
                            string newTilePath = $"{ResultsPath + basePath}_tile-{tileIndex:D2}{resultSuffix}.png";
                            if (InMemoryMode)
                                imageNextTile = hrTiles[newTilePath];
                            else
                            {
                                imageNextTile = new MagickImage(newTilePath);
                                tileFilesToDelete.Add(new FileInfo($"{ResultsPath + basePath}_tile-{tileIndex:D2}{resultSuffix}.png"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteOpenError(file, ex.Message);
                        return null;
                    }

                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
                    {
                        try
                        {
                            if (HotProfile.UseFilterForAlpha)
                            {
                                MagickImage image = new MagickImage(pathImage.Item2);
                                MagickImage inputImageAlpha = (MagickImage)image.Separate(Channels.Alpha).First();

                                int inputTileWidth = image.Width / tiles[0];
                                int upscaleMod = tileWidth / inputTileWidth;
                                Logger.Write($"Upscaling alpha x{upscaleMod} with {HotProfile.AlphaFilterType} filter", Color.LightBlue);
                                if (upscaleMod != 1)
                                    imageAlphaResult = ImageOperations.ResizeImage(inputImageAlpha, upscaleMod, (FilterType)HotProfile.AlphaFilterType);
                                else
                                    imageAlphaResult = inputImageAlpha;
                                alphaReadError = true;
                            }
                            else
                            {
                                var newAlphaTilePath = $"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{resultSuffix}.png";

                                if (InMemoryMode)
                                    imageAlphaNextTile = hrTiles[newAlphaTilePath];
                                else
                                {
                                    imageAlphaNextTile = new MagickImage(newAlphaTilePath);
                                    tileFilesToDelete.Add(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{resultSuffix}.png"));
                                }

                                if (j == 0)
                                {
                                    imageAlphaRow = imageAlphaNextTile;
                                }
                                else
                                {
                                    JoinTilesNew(ref imageAlphaRow, imageAlphaNextTile, Enums.Direction.Horizontal, -tileWidth * j, 0);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            alphaReadError = true;
                            if (!HotProfile.IgnoreSingleColorAlphas)
                                Logger.WriteOpenError(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{resultSuffix}.png"), ex.Message);
                        }
                    }

                    if (j == 0)
                    {
                        imageRow = imageNextTile;
                        continue;
                    }
                    else
                        JoinTilesNew(ref imageRow, imageNextTile, Enums.Direction.Horizontal, -tileWidth * j, 0);

                    imageNextTile.Dispose();
                }

                if (i == 0)
                {
                    imageResult = imageRow;
                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
                        imageAlphaResult = imageAlphaRow;
                }
                else
                {
                    JoinTilesNew(ref imageResult, imageRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                    imageRow.Dispose();

                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
                    {
                        JoinTilesNew(ref imageAlphaResult, imageAlphaRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                    }
                }
                GC.Collect();
            }
            bool alphaIsUpscaledWithFilter = imageAlphaResult != null && imageAlphaResult.Width == imageResult.Width && imageAlphaResult.Height == imageResult.Height;
            if ((imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError) || alphaIsUpscaledWithFilter)
            {
                imageResult.Composite(imageAlphaResult, CompositeOperator.CopyAlpha);
                imageAlphaResult.Dispose();
            }
            return imageResult;
        }

        #endregion

        Image MergeTiles(Tuple<string, MagickImage> pathImage, ImageValues values, UpscaleResult result, List<FileInfo> tileFilesToDelete)
        {
            Profile HotProfile = values.profile1;
            int upMod = result.Model.UpscaleFactor;

            bool alphaReadError = false, cancelRgbGlobalbalance = false, cancelAlphaGlobalbalance = false;
            Image imageResult = null, imageAlphaResult = null;
            FileInfo file = new FileInfo(pathImage.Item1);
            Image imageAlphaRow = null;
            int tileWidth = values.TileW * upMod, tileHeight = values.TileH * upMod;

            string basePathAlpha = result.BasePath;
            if (OutputDestinationMode == 1) // grab alpha tiles from different folder
            {
                string fileName = Path.GetFileNameWithoutExtension(file.Name);
                basePathAlpha = basePathAlpha.Replace(
                    $"{DirSeparator}Images{DirSeparator}{fileName}",
                    $"{DirSeparator}Images{DirSeparator}{fileName}_alpha");
            }

            Dictionary<string, MagickImage> hrTiles = null;
            if (InMemoryMode)
            {
                hrTiles = hrDict[pathImage.Item1];
            }

            for (int i = 0; i < values.Rows; i++)
            {
                Image imageRow = null;

                for (int j = 0; j < values.Columns; j++)
                {
                    int tileIndex = i * values.Columns + j;

                    Image imageNextTile, imageAlphaNextTile;
                    try
                    {
                        if (values.profile1.SplitRGB)
                            imageNextTile = JoinRGB(pathImage, result, tileIndex, ResultSuffix, ref tileFilesToDelete);
                        else
                        {
                            string newTilePath = $"{ResultsPath + result.BasePath}_tile-{tileIndex:D2}{ResultSuffix}.png";
                            if (InMemoryMode)
                                imageNextTile = ImageOperations.ConvertToVips(hrTiles[newTilePath]);
                            else
                            {
                                imageNextTile = Image.NewFromFile(newTilePath, false, Enums.Access.Sequential);
                                tileFilesToDelete.Add(new FileInfo($"{ResultsPath + result.BasePath}_tile-{tileIndex:D2}{ResultSuffix}.png"));
                            }
                        }
                    }
                    catch (VipsException ex)
                    {
                        Logger.WriteOpenError(file, ex.Message);
                        return null;
                    }

                    if (values.UseAlpha && !alphaReadError && !HotProfile.UseFilterForAlpha)
                    {
                        try
                        {
                            var newAlphaTilePath = $"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{ResultSuffix}.png";
                            if (InMemoryMode)
                                imageAlphaNextTile = ImageOperations.ConvertToVips(hrTiles[newAlphaTilePath]);
                            else
                            {
                                imageAlphaNextTile = Image.NewFromFile(newAlphaTilePath, false, Enums.Access.Sequential);
                                tileFilesToDelete.Add(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{ResultSuffix}.png"));
                            }

                            //remove padding
                            if (PaddingSize > 0)
                                imageAlphaNextTile = imageAlphaNextTile.Crop(PaddingSize * upMod, PaddingSize * upMod, imageAlphaNextTile.Width - PaddingSize * upMod * 2, imageAlphaNextTile.Height - PaddingSize * upMod * 2);

                            if (j == 0)
                            {
                                imageAlphaRow = imageAlphaNextTile;
                            }
                            else
                            {
                                if (UseOldVipsMerge)
                                    JoinTiles(ref imageAlphaRow, imageAlphaNextTile, Enums.Direction.Horizontal, -tileWidth * j, 0);
                                else
                                    JoinTilesNew(ref imageAlphaRow, imageAlphaNextTile, false, Enums.Direction.Horizontal, -tileWidth * j, 0);
                                if (HotProfile.BalanceAlphas)
                                    UseGlobalbalance(ref imageAlphaRow, ref cancelAlphaGlobalbalance, $"{file.Name} alpha");
                            }

                        }
                        catch (VipsException ex)
                        {
                            alphaReadError = true;
                            if (!HotProfile.IgnoreSingleColorAlphas)
                                Logger.WriteOpenError(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{ResultSuffix}.png"), ex.Message);
                        }
                    }

                    //remove padding
                    if (PaddingSize > 0)
                        imageNextTile = imageNextTile.Crop(PaddingSize * upMod, PaddingSize * upMod, imageNextTile.Width - PaddingSize * upMod * 2, imageNextTile.Height - PaddingSize * upMod * 2);

                    if (j == 0)
                    {
                        imageRow = imageNextTile;
                        continue;
                    }
                    else
                    {                       
                        if (UseOldVipsMerge)
                            JoinTiles(ref imageRow, imageNextTile, Enums.Direction.Horizontal, -tileWidth * j, 0);
                        else
                            JoinTilesNew(ref imageRow, imageNextTile, false, Enums.Direction.Horizontal, -tileWidth * j, 0);
                    }

                    if (HotProfile.BalanceRgb)
                        UseGlobalbalance(ref imageRow, ref cancelRgbGlobalbalance, $"{file.Name}");
                    imageNextTile.Dispose();
                }

                if (i == 0)
                {
                    imageResult = imageRow;
                    if (values.UseAlpha && !alphaReadError && !HotProfile.UseFilterForAlpha)
                        imageAlphaResult = imageAlphaRow;
                }
                else
                {
                    if (UseOldVipsMerge)
                        JoinTiles(ref imageResult, imageRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                    else
                        JoinTilesNew(ref imageResult, imageRow, true, Enums.Direction.Vertical, 0, -tileHeight * i);
                    imageRow.Dispose();

                    if (HotProfile.BalanceRgb)
                        UseGlobalbalance(ref imageResult, ref cancelRgbGlobalbalance, file.Name);

                    if (values.UseAlpha && !alphaReadError && !HotProfile.UseFilterForAlpha)
                    {
                        if (UseOldVipsMerge)
                            JoinTiles(ref imageAlphaResult, imageAlphaRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                        else
                            JoinTilesNew(ref imageAlphaResult, imageAlphaRow, true, Enums.Direction.Vertical, 0, -tileHeight * i);

                        if (HotProfile.BalanceAlphas)
                            UseGlobalbalance(ref imageAlphaResult, ref cancelAlphaGlobalbalance, $"{file.Name} alpha");
                    }
                }
                GC.Collect();
            }

            if (values.UseAlpha && HotProfile.UseFilterForAlpha)
            {
                MagickImage image = new MagickImage(pathImage.Item2);
                MagickImage inputImageAlpha = (MagickImage)image.Separate(Channels.Alpha).First();
                MagickImage upscaledAlpha = null;

                if (upMod != 1)
                    upscaledAlpha = ImageOperations.ResizeImage(inputImageAlpha, upMod, (FilterType)HotProfile.AlphaFilterType);
                else
                    upscaledAlpha = inputImageAlpha;
                byte[] buffer = upscaledAlpha.ToByteArray(MagickFormat.Png00);
                imageAlphaResult = Image.NewFromBuffer(buffer);
                alphaReadError = true;
            }

            //bool alphaIsUpscaledWithFilter = imageAlphaResult != null && imageAlphaResult.Width == imageResult.Width && imageAlphaResult.Height == imageResult.Height;
            if ((values.UseAlpha && !alphaReadError) || HotProfile.UseFilterForAlpha)
            {
                imageResult = imageResult.Bandjoin(imageAlphaResult.ExtractBand(0));
                imageResult = imageResult.Copy(interpretation: "srgb").Cast("uchar");
                imageAlphaResult.Dispose();
            }
            return imageResult;
        }

        internal void MergeTask(Tuple<string, MagickImage> pathImage, ImageValues values, UpscaleResult result, Profile HotProfile, string outputFilename = "")
        {
            FileInfo file = new FileInfo(pathImage.Item1);
            Logger.WriteDebug($"Image path: {pathImage.Item1}");
            Logger.WriteDebug($"Base path: {result.BasePath}");

            #region IMAGE READ
           
            string resultSuffix = "";

            if (UseResultSuffix)
                resultSuffix = ResultSuffix;
            
            int imageWidth = values.Dimensions[0], imageHeight = values.Dimensions[1];

            int[] tiles = new int[] { values.Columns, values.Rows };
            int tileWidth = values.TileW, tileHeight = values.TileH;
            double resizeMod = values.ResizeMod;
            int upscaleMod = result.Model.UpscaleFactor;           

            MagickImage inputImage = pathImage.Item2;
            if (inputImage.HasAlpha && !HotProfile.IgnoreAlpha && HotProfile.IgnoreSingleColorAlphas)            
                if (values.AlphaSolidColor)                
                    inputImage.HasAlpha = false; 

            int expandSize = SeamlessExpandSize;
            if (imageHeight <= 32 || imageWidth <= 32)
                expandSize = 8;

            //if (HotProfile.SeamlessTexture)
            //{
            //    WriteToLogDebug($"Seamless texture, expand size: {expandSize}");
            //    imageWidth += expandSize * 2;
            //    imageHeight += expandSize * 2;
            //}

            List<FileInfo> tileFilesToDelete = new List<FileInfo>();
            #endregion          

            MagickImage finalImage = null;
            Image imageResult = null;

            ImageFormatInfo outputFormat;
            if (HotProfile.UseOriginalImageFormat)
                outputFormat = HotProfile.FormatInfos
                    .Where(x => x.Extension.Equals(file.Extension, StringComparison.InvariantCultureIgnoreCase)).First();
            else
                outputFormat = HotProfile.selectedOutputFormat;
            if (outputFormat == null)
                outputFormat = new ImageFormatInfo(file.Extension);

            Logger.WriteDebug($"Output format: {outputFormat.Extension}");

            string destinationPath = OutputDirectoryPath + result.BasePath.Replace("_ChannelChar", "") + outputFormat;

            if (outputFilename != "")
                destinationPath =
                    OutputDirectoryPath + result.BasePath.Replace(Path.GetFileNameWithoutExtension(file.Name), outputFilename) + outputFormat;

            if (OutputDestinationMode == 3)
                destinationPath = $"{OutputDirectoryPath}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}" +
                    $"{Path.GetFileNameWithoutExtension(file.Name)}{outputFormat}";

            Logger.WriteDebug($"Destination path: {destinationPath}");

            int mergedWidth = 0, mergedHeight = 0;
            if (UseImageMagickMerge)
            {
                //finalImage = MergeTilesNew(pathImage, tiles, new int[] { tileWidth, tileHeight }, result.BasePath, basePathAlpha, resultSuffix, tileFilesToDelete, imageHasAlpha, HotProfile);
                //mergedWidth = finalImage.Width;
                //mergedHeight = finalImage.Height;
            }
            else
            {
                imageResult = MergeTiles(pathImage, values, result, tileFilesToDelete);
                mergedWidth = imageResult.Width;
                mergedHeight = imageResult.Height;
            }

            if (HotProfile.SeamlessTexture)
            {
                Logger.WriteDebug($"Extrating seamless texture. Upscale modificator: {upscaleMod}");
                if (UseImageMagickMerge)
                    finalImage = ExtractTiledTexture(finalImage, upscaleMod, expandSize);
                else
                    ExtractTiledTexture(ref imageResult, upscaleMod, expandSize);
            }
            else
            {
                if (UseImageMagickMerge)
                    finalImage.Crop(values.CropToDimensions[0] * upscaleMod, values.CropToDimensions[1] * upscaleMod, Gravity.Northwest);
                else
                    imageResult = imageResult.Crop(0, 0, values.CropToDimensions[0] * upscaleMod, values.CropToDimensions[1] * upscaleMod);
            }

            #region SAVE IMAGE

            if (!UseImageMagickMerge)
            {
                if (imageResult == null)
                    return;
                if (outputFormat.VipsNative &&
                    (!HotProfile.ThresholdEnabled || (HotProfile.ThresholdBlackValue == 0 && HotProfile.ThresholdWhiteValue == 100)) &&
                    (!HotProfile.ThresholdAlphaEnabled || (HotProfile.ThresholdBlackValue == 0 && HotProfile.ThresholdWhiteValue == 100)) &&
                      HotProfile.ResizeImageAfterScaleFactor == 1.0) //no need to convert to MagickImage, save faster with vips
                {
                    Logger.WriteDebug($"Saving with vips");

                    if (OverwriteMode == 2)
                    {
                        Logger.WriteDebug($"Overwriting file");
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
                    imageResult.Dispose();
                    //pathImage.Item2.Dispose();
                    IncrementDoneCounter();
                    //ReportProgress();
                    Logger.Write($"<{file.Name}> MERGE DONE", Color.LightGreen);

                    if (HotProfile.DeleteResults)
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
                finalImage = new MagickImage(imageBuffer, readSettings);
            }

            ImageOperations.ImagePostrpocess(ref finalImage, HotProfile, Logger);

            if (OverwriteMode == 2)
            {
                file.Delete();
                destinationPath = $"{OutputDirectoryPath}{DirSeparator}" +
                    $"{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}{file.Name}";
            }
            else
            {
                string a = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(a))
                    Directory.CreateDirectory(a);
            }

            if (outputFormat.Extension == ".dds")
                WriteToFileDds(finalImage, destinationPath, HotProfile);
            else
                finalImage.Write(destinationPath);
            imageResult?.Dispose();
            finalImage.Dispose();
            //pathImage.Item2.Dispose();
            IncrementDoneCounter();
            ReportProgress();
            Logger.Write($"{file.Name} DONE", Color.LightGreen);
            if (HotProfile.DeleteResults)
                tileFilesToDelete.ForEach(x => x.Delete());
            GC.Collect();
            #endregion
        }

        async public Task Merge()
        {
            if (!IsSub)
                SaveSettings();

            if(batchValues == null)            
                batchValues = ReadBatchValues();            

            //histMatchTest.MatchHist();
            //return;
            ////opencvTest.Stitch();
            ////return;

            DirectoryInfo di = new DirectoryInfo(InputDirectoryPath);

            ResetDoneCounter();
            ResetTotalCounter();

            int tempOutMode = OutputDestinationMode;
            if (UseModelChain) tempOutMode = 0;

            FileInfo[] inputFiles = batchValues.images.Keys.Select(x => new FileInfo(x)).ToArray();

            int totalFiles = 0;
            foreach(var image in batchValues.images.Values)
                totalFiles += image.results.Count;            

            SetTotalCounter(totalFiles);

            Logger.Write("Merging tiles...");
            await Task.Run(() => Parallel.ForEach(inputFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = MaxConcurrency }, file =>
            //foreach(var file in inputFiles)
            {
                var values = batchValues.images[file.FullName];

                if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper()))
                    return;

                MagickImage inputImage = ImageOperations.LoadImage(file);

                Tuple<string, MagickImage> pathImage = new Tuple<string, MagickImage>(file.FullName, inputImage);

                Profile profile = new Profile();

                List<Rule> rules = new List<Rule>(Ruleset.Values);
                if (DisableRuleSystem)
                    rules = new List<Rule> { new Rule("Simple rule", CurrentProfile, CurrentFilter) };

                bool fileSkipped = true;
                foreach (var rule in rules)
                {
                    if (rule.Filter.ApplyFilter(file))
                    {
                        profile = rule.Profile;
                        fileSkipped = false;
                        break;
                    }
                }

                if (fileSkipped)
                {
                    IncrementDoneCounter(false);
                    Logger.Write($"{file.Name} is filtered, skipping", Color.HotPink);
                    return;
                }

                foreach(var result in values.results)
                    MergeTask(
                        pathImage,
                        values,
                        result,
                        profile);
            }
            ));

            GC.Collect();
            Logger.Write("Finished!", Color.LightGreen);

            string pathToMergedFiles = OutputDirectoryPath;
            if (tempOutMode == 1)
                pathToMergedFiles += $"{DirSeparator}Images";
            if (tempOutMode == 2)
                pathToMergedFiles += $"{DirSeparator}Models";
        }
        async Task Merge(string path)
        {
            if (!IsSub)
                SaveSettings();

            if (batchValues == null)           
                batchValues = ReadBatchValues();

            var values = batchValues.images[path];

            DirectoryInfo di = new DirectoryInfo(InputDirectoryPath);

            //ResetDoneCounter();
            //ResetTotalCounter();     

            FileInfo file = new FileInfo(path);

            Logger.Write($"{file.Name} MERGE START");

            if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper()))
                return;

            MagickImage inputImage = ImageOperations.LoadImage(file);

            Tuple<string, MagickImage> pathImage = new Tuple<string, MagickImage>(file.FullName, inputImage);

            Profile profile = new Profile();

            List<Rule> rules = new List<Rule>(Ruleset.Values);
            if (DisableRuleSystem)
                rules = new List<Rule> { new Rule("Simple rule", CurrentProfile, CurrentFilter) };

            bool fileSkipped = true;
            foreach (var rule in rules)
            {
                if (rule.Filter.ApplyFilter(file))
                {
                    profile = rule.Profile;
                    fileSkipped = false;
                    break;
                }
            }

            if (fileSkipped)
            {
                IncrementDoneCounter(false);
                Logger.Write($"{file.Name} is filtered, skipping", Color.HotPink);
                return;
            }

            await Task.Run(() =>
            {
                if (InMemoryMode)
                {
                    var result = values.results[0];

                    MergeTask(pathImage, values, result, profile);

                    //if (OutputDestinationMode == 1)
                    //{
                        
                    //    var tilePath = hrDict[file.FullName].Keys.ElementAt(0);

                    //    var index = tilePath.LastIndexOf(DirSeparator);
                    //    var indexPrev = tilePath.LastIndexOf(DirSeparator, index - 1);
                    //    var modelName = tilePath.Substring(index + 1);

                    //    string basePath = $"{DirSeparator}Images{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}{DirSeparator}{Path.GetFileNameWithoutExtension(modelName)}";
                    //    basePath = basePath.Remove(basePath.Length - 8, 8); //remove "_tile-00"  
                    //    result.BasePath = basePath;
                    //    MergeTask(pathImage, values, result, profile);
                      

                    //}
                    //if (OutputDestinationMode == 2)
                    //{
                    //    DirectoryInfo modelsFolder = new DirectoryInfo(ResultsPath + $"{DirSeparator}Models{DirSeparator}");

                    //    //for (int i = 0; i < hrDict[file.FullName].Keys.Count; i += lrDict[file.FullName].Keys.Count)
                    //    //{
                    //    var tilePath = hrDict[file.FullName].Keys.ElementAt(0);

                    //    var index = tilePath.LastIndexOf(DirSeparator);
                    //    var indexPrev = tilePath.LastIndexOf(DirSeparator, index - 1);
                    //    var modelName = tilePath.Substring(indexPrev + 1, index - indexPrev - 1);

                    //    string basePath = $"{DirSeparator}Models{DirSeparator}{modelName}{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}";
                    //    result.BasePath = basePath;
                    //    MergeTask(pathImage, values, result, profile);
                    //    //}

                    //}
                    //if (OutputDestinationMode == 3)
                    //{
                    //    MergeTask(
                    //        pathImage,
                    //        values,
                    //        file.FullName.Replace(InputDirectoryPath, "").Replace(file.Name, Path.GetFileNameWithoutExtension(file.Name)),
                    //        profile);
                    //}
                }
                else
                    foreach (var result in values.results)
                        MergeTask(
                            pathImage,
                            values,
                            result,
                            profile);
            });

            if (InMemoryMode)
            {
                lrDict.Remove(path);
                hrDict.Remove(path);
            }
            GC.Collect();

            string pathToMergedFiles = OutputDirectoryPath;
            if (OutputDestinationMode == 1)
                pathToMergedFiles += $"{DirSeparator}images";
            if (OutputDestinationMode == 2)
                pathToMergedFiles += $"{DirSeparator}models";
        }

        #region SAVE FILE
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
                Logger.Write($"{ex.Message}");
                return false;
            }
            return true;
        }

        void WriteToFileDds(MagickImage finalImage, string destinationPath, Profile HotProfile)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Surface processedSurface = ImageOperations.ConvertToSurface(finalImage);
                FileStream fileStream = new FileStream(destinationPath, FileMode.Create);
                DdsFile.Save(
                    fileStream,
                    HotProfile.DdsFileFormat.DdsFileFormat,
                    DdsErrorMetric.Perceptual,
                    HotProfile.DdsBC7CompressionMode,
                    HotProfile.DdsIsCubemap,
                    HotProfile.DdsGenerateMipmaps,
                    ResamplingAlgorithm.Bilinear,
                    processedSurface,
                    null);
                fileStream.Close();
            }
            else
                finalImage.Write(destinationPath, MagickFormat.Dds);
        }
        #endregion

        void UpdateQueue()
        {
            FileInfo newFile = null;
            lock (fileQueue)
            {
                if (fileQueue.Count > 0)
                {
                    newFile = fileQueue.Dequeue();
                    hrDict.Add(newFile.FullName, new Dictionary<string, MagickImage>());
                }
            }
            if (newFile != null)
                SplitImage.Post(newFile);
        }

        #endregion

        #region UPSCALE               

        async public Task<bool> Upscale(bool NoWindow = true, Profile HotProfile = null, bool async = true)
        {
            if (HotProfile == null)
                HotProfile = GlobalProfile;
            if (DisableRuleSystem)
                HotProfile = CurrentProfile;
            if (!IsSub)
                SaveSettings();

            if (HotProfile.UseModel && HotProfile.Model != null)
                checkedModels = new List<ModelInfo>() { HotProfile.Model };
            else
                checkedModels = SelectedModelsItems;

            if (checkedModels.Count == 0)
            {
                Logger.Write("No models selected!");
                return false;
            }

            if (UseModelChain && checkedModels.Count > 1)
            {
                string upscaleSizePattern = "(?:_?[1|2|4|8|16]x_)|(?:_x[1|2|4|8|16]_?)|(?:_[1|2|4|8|16]x_?)|(?:_?x[1|2|4|8|16]_)";
                int latestSize = 1;
                foreach (var model in checkedModels)
                {
                    var regResult = Regex.Match(model.Name.ToLower(), upscaleSizePattern);
                    int size = int.Parse(regResult.Value.Replace("x", "").Replace("_", ""));
                    if (size > 1 && latestSize > 1)
                    {
                        Logger.Write($"Can't use {model.Name} after another {latestSize}x model.");
                        return false;
                    }
                    latestSize = size;
                }
            }

            DirectoryInfo directory = new DirectoryInfo(ResultsPath);
            if (!directory.Exists)
                directory.Create();

            if (CreateMemoryImage)
            {
                Image image = Image.Black(MaxTileResolutionWidth, MaxTileResolutionHeight);
                image.WriteToFile($"{LrPath}{DirSeparator}([000])000)_memory_helper_tile-00.png");
            }

            Process process;
            if (UseBasicSR)
                process = await BasicSR_Test(NoWindow, HotProfile);
            else
            {
                if (UseJoey)

                    process = await JoeyESRGAN(NoWindow, HotProfile);
                else
                    process = await ESRGAN(NoWindow, HotProfile);

                if (process == null)
                    return false;
            }

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.EnableRaisingEvents = true;

            gpuMonitor.MonitorVramStart(VramMonitorEnable, VramMonitorFrequency);

            if (async)
            {
                int processExitCode = await RunProcessAsync(process);

                gpuMonitor.MonitorVramTokenSource?.Cancel();

                if (processExitCode == -666)
                    return false;
                if (processExitCode != 0)
                {
                    Logger.Write("Error ocured during ESRGAN work!", Color.Red);
                    return false;
                }
                Logger.Write("ESRGAN finished!", Color.LightGreen);
            }
            else
            {
                RunProcessAsyncInMemory(process);
                Logger.Write("ESRGAN start running in background!", Color.LightGreen);
            }
            if (Helper.GetCondaEnv(UseCondaEnv, CondaEnv) != "")
            {
                Process pr = new Process();
                pr.StartInfo.FileName = "cmd";
                pr.StartInfo.Arguments = "/C conda activate";
                pr.StartInfo.CreateNoWindow = true;
                pr.Start();
            }
            return true;
        }

        void WriteTestScriptToDisk()
        {
            string archName = "ESRGAN";
            if (UseBasicSR) archName = "BasicSR";
            string scriptsDir = $"{Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}{DirSeparator}Scripts{DirSeparator}ESRGAN";
            string block = EmbeddedResource.GetFileText($"ImageEnhancingUtility.Core.Scripts.{archName}.block.py");
            string blockPath = $"{DirSeparator}block.py";
            string architecture = EmbeddedResource.GetFileText($"ImageEnhancingUtility.Core.Scripts.{archName}.architecture.py");
            string archPath = $"{DirSeparator}architecture.py";
            string upscale = EmbeddedResource.GetFileText($"ImageEnhancingUtility.Core.Scripts.{archName}.upscale.py");
            string upscaleFromMemory = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.Scripts.ESRGAN.upscaleFromMemory.py");

            string scriptPath = $"{DirSeparator}IEU_test.py";
            string upscalePath = $"{DirSeparator}upscale.py";
            string upscaleFromMemoryPath = $"{DirSeparator}upscaleFromMemory.py";

            Directory.CreateDirectory(scriptsDir);
            if (!File.Exists(scriptsDir + blockPath))
                File.WriteAllText(scriptsDir + blockPath, block);
            if (!File.Exists(scriptsDir + archPath))
                File.WriteAllText(scriptsDir + archPath, architecture);
            if (!File.Exists(scriptsDir + upscalePath))
                File.WriteAllText(scriptsDir + upscalePath, upscale);
            if (!File.Exists(scriptsDir + upscaleFromMemoryPath))
                File.WriteAllText(scriptsDir + upscaleFromMemoryPath, upscaleFromMemory);

            if (UseBasicSR) scriptPath = EsrganPath + $"{DirSeparator}codes{DirSeparator}IEU_test.py";
            else
            {
                File.Copy(scriptsDir + blockPath, EsrganPath + blockPath, true);
                File.Copy(scriptsDir + archPath, EsrganPath + archPath, true);
            }
            if (InMemoryMode)
                File.Copy(scriptsDir + upscaleFromMemoryPath, EsrganPath + scriptPath, true);
            else
                File.Copy(scriptsDir + upscalePath, EsrganPath + scriptPath, true);
        }

        async Task<int> DetectModelUpscaleFactor(ModelInfo checkedModel)
        {
            string upscaleSizePattern = "(?:_?[1|2|4|8|16]x_)|(?:_x[1|2|4|8|16]_?)|(?:_[1|2|4|8|16]x_?)|(?:_?x[1|2|4|8|16]_)";
            string upscaleSizePatternAlt = "(?:[1|2|4|8|16]x)|(?:x[1|2|4|8|16])|(?:[1|2|4|8|16]x)|(?:x[1|2|4|8|16])";
            var regResult = Regex.Match(checkedModel.Name.ToLower(), upscaleSizePattern);
            var regResultAlt = Regex.Match(checkedModel.Name.ToLower(), upscaleSizePatternAlt);
            int upscaleMultiplayer = -1;
            if (regResult.Success && regResult.Groups.Count == 1)
            {
                upscaleMultiplayer = int.Parse(regResult.Value.Replace("x", "").Replace("_", ""));
                checkedModel.UpscaleFactor = upscaleMultiplayer;
            }
            else if (regResultAlt.Success && regResultAlt.Groups.Count == 1)
            {
                upscaleMultiplayer = int.Parse(regResultAlt.Value.Replace("x", ""));
                var newName = checkedModel.Name.Replace(regResultAlt.Value, $"{upscaleMultiplayer}x_");
                var newFullname = checkedModel.FullName.Replace(checkedModel.Name, newName);
                File.Move(checkedModel.FullName, newFullname);
                checkedModel.FullName = newFullname;
                checkedModel.Name = newName;
                Logger.Write($"Changed model filename to {checkedModel.Name}", Color.LightBlue);
            }
            else
            {
                int processExitCodePthReader = -666;
                Logger.Write($"Detecting {checkedModel.Name} upscale size...");

                using (Process pthReaderProcess = PthReader(checkedModel.FullName))
                    processExitCodePthReader = await RunProcessAsync(pthReaderProcess);

                if (processExitCodePthReader != 0)
                {
                    Logger.Write($"Failed to detect {checkedModel.Name} upscale size!", Color.Red);
                    return upscaleMultiplayer;
                }
                Logger.Write($"{checkedModel.Name} upscale size is {hotModelUpscaleSize}", Color.LightGreen);
                checkedModel.UpscaleFactor = hotModelUpscaleSize;
                Helper.RenameModelFile(checkedModel, checkedModel.UpscaleFactor);
                Logger.Write($"Changed model filename to {checkedModel.Name}", Color.LightBlue);
                CreateModelTree();
            }
            return upscaleMultiplayer;
        }


        #region PYTHON PROCESS STUFF

        #region INMEMORY

        StreamWriter writer;

        async Task WriteImageToStream(Dictionary<string, string> images)
        {
            foreach (var key in images.Keys)
            {
                string json = JsonConvert.SerializeObject(new Dictionary<string, string>() { { key, images[key] } });
                //WriteToStream.Post(json);
                await writer.WriteLineAsync(json);
            }
            if (fileQueue.Count == 0 && !IsSub)
                WriteToStream.Complete();
            if (fileQueue != null)
                UpdateQueue();
        }

        void WriteModelsToStream()
        {
            var modelPaths = JsonConvert.SerializeObject(checkedModels.ConvertAll(x => x.FullName)).Replace("\\", "/");
            writer.WriteLine(modelPaths);
        }

        public void RunProcessAsyncInMemory(Process process)
        {
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.EnableRaisingEvents = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"cd {process.StartInfo.Arguments.Replace("\"", "\\\"").Replace("&", "&&")}\"";
                Logger.Write(process.StartInfo.Arguments);
            }
            else
            {
                process.StartInfo.FileName = "cmd";
                process.StartInfo.Arguments = "/C cd /d " + process.StartInfo.Arguments;
            }

            process.Exited += (sender, args) =>
            {
                process.OutputDataReceived -= SortOutputHandler;
                process.OutputDataReceived -= SortOutputHandlerPthReader;
                process.ErrorDataReceived -= SortOutputHandler;
                process.Dispose();
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            writer = process.StandardInput;
            WriteModelsToStream();
            return;
        }

        #endregion

        async Task<Process> ESRGAN(bool NoWindow, Profile HotProfile)
        {
            if (checkedModels.Count > 1 && HotProfile.UseDifferentModelForAlpha)
            {
                Logger.Write("Only single model must be selected when using different model for alpha");
                return null;
            }

            Process process = new Process();

            process.StartInfo.Arguments = $"{EsrganPath}";
            process.StartInfo.Arguments += Helper.GetCondaEnv(UseCondaEnv, CondaEnv);
            bool noValidModel = true;
            string torchDevice = UseCPU ? "cpu" : "cuda";
            int upscaleMultiplayer = 0;
            string resultsPath = ResultsPath;

            int tempOutMode = OutputDestinationMode;

            if (OverwriteMode == 1)
                resultsPath = LrPath;

            int modelIndex = 0;
            if (InMemoryMode)
            {
                noValidModel = false;
                process.StartInfo.Arguments +=
                   $" & python IEU_test.py \"blank\" 1 {torchDevice}" +
                   $" \"{LrPath + $"{DirSeparator}*"}\" \"{resultsPath}\" {tempOutMode} {InMemoryMode}";
            }
            else
                foreach (ModelInfo checkedModel in checkedModels)
                {
                    if ((upscaleMultiplayer = await DetectModelUpscaleFactor(checkedModel)) == 0)
                            continue;
                    noValidModel = false;

                    if (UseModelChain)
                    {
                        tempOutMode = 0;
                        resultsPath = LrPath;
                        if (modelIndex == checkedModels.Count - 1)
                            resultsPath = ResultsPath;
                    }

                    process.StartInfo.Arguments +=
                    $" & python IEU_test.py \"{checkedModel.FullName}\" {upscaleMultiplayer} {torchDevice}" +
                    $" \"{LrPath + $"{DirSeparator}*"}\" \"{resultsPath}\" {tempOutMode} {InMemoryMode}";

                    modelIndex++;
                }

            if (HotProfile.UseDifferentModelForAlpha)
            {   //detect upsacle factor for alpha model
                bool validModelAlpha = false;
                int upscaleMultiplayerAlpha = 0;

                if ((upscaleMultiplayerAlpha = await DetectModelUpscaleFactor(HotProfile.ModelForAlpha)) != 0)
                {
                    validModelAlpha = true;
                }

                if (upscaleMultiplayer != upscaleMultiplayerAlpha)
                {
                    Logger.Write("Scale of rgb model and alpha model must be the same");
                    return null;
                }
                if (validModelAlpha)
                    process.StartInfo.Arguments +=
                        $" & python IEU_test.py \"{HotProfile.ModelForAlpha.FullName}\" {upscaleMultiplayerAlpha} {torchDevice}" +
                        $" \"{LrPath + $"_alpha{DirSeparator}*"}\" \"{resultsPath}\" {OutputDestinationMode} {InMemoryMode}";
                else
                {
                    Logger.Write("Can't detect model for alpha scale");
                    return null;
                }
            }
            if (noValidModel)
            {
                Logger.Write("Can't start ESRGAN: no selected models with known upscale size");
                return null;
            }

            WriteTestScriptToDisk();

            process.ErrorDataReceived += SortOutputHandler;
            process.OutputDataReceived += SortOutputHandler;
            process.StartInfo.CreateNoWindow = NoWindow;

            if (!Directory.Exists(LrPath))
            {
                Logger.Write(LrPath + " doen't exist!");
                return null;
            }

            if (!InMemoryMode)
            {
                SearchOption searchOption = SearchOption.TopDirectoryOnly;
                if (OutputDestinationMode == 3)
                    searchOption = SearchOption.AllDirectories;
                SetTotalCounter(Directory.GetFiles(LrPath, "*", searchOption).Count() * checkedModels.Count);
                if (HotProfile.UseDifferentModelForAlpha)
                    SetTotalCounter(FilesTotal + Directory.GetFiles(LrPath + "_alpha", "*", searchOption).Count());
                ResetDoneCounter();
            }

            Logger.Write("Starting ESRGAN...");
            return process;
        }

        [Browsable(false)]
        public JoeyEsrgan JoeyEsrgan { get; set; } = new JoeyEsrgan();

        async Task<Process> JoeyESRGAN(bool NoWindow, Profile HotProfile)
        {
            if (checkedModels.Count > 1 && !UseModelChain)
            {
                Logger.Write("Only single model must be selected when not using model chain");
                return null;
            }

            Process process = new Process();

            process.StartInfo.Arguments = $"{EsrganPath}";
            process.StartInfo.Arguments += Helper.GetCondaEnv(UseCondaEnv, CondaEnv);

            int tempOutMode = OutputDestinationMode;

            //if (HotProfile.OverwriteMode == 1)
            //    resultsPath = LrPath;

            int modelIndex = 0;

            JoeyEsrgan.Model = $"{checkedModels[0].Name}";

            for (int i = 1; i < checkedModels.Count; i++)
            {
                //if ((upscaleMultiplayer = await DetectModelUpscaleFactor(checkedModel)) == 0)
                //    continue;
                //noValidModel = false;   

                JoeyEsrgan.Model += $">{checkedModels[i].Name}";
            }

            JoeyEsrgan.Input = InputDirectoryPath;
            JoeyEsrgan.Output = OutputDirectoryPath;

            JoeyEsrgan.TileSize = (int)Math.Round(Math.Sqrt(MaxTileResolution));
            JoeyEsrgan.Seamless = HotProfile.SeamlessTexture;
            JoeyEsrgan.CPU = UseCPU;

            var argumentString = JoeyEsrgan.ArgumentString;

            process.StartInfo.Arguments +=
               $" & python upscale.py {argumentString}";

            //if (HotProfile.UseDifferentModelForAlpha)
            //{   //detect upsacle factor for alpha model
            //    bool validModelAlpha = false;
            //    int upscaleMultiplayerAlpha = 0;

            //    if ((upscaleMultiplayerAlpha = await DetectModelUpscaleFactor(HotProfile.ModelForAlpha)) == 0)
            //    {
            //        validModelAlpha = true;
            //    }

            //    if (upscaleMultiplayer != upscaleMultiplayerAlpha)
            //    {
            //        WriteToLog("Upscale size for rgb model and alpha model must be the same");
            //        return null;
            //    }
            //    if (validModelAlpha)
            //        process.StartInfo.Arguments +=
            //            $" & python IEU_test.py \"{HotProfile.ModelForAlpha.FullName}\" {upscaleMultiplayerAlpha} {torchDevice}" +
            //            $" \"{LrPath + $"_alpha{DirectorySeparator}*"}\" \"{resultsPath}\" {OutputDestinationMode}";
            //}

            //if (noValidModel)
            //{
            //    WriteToLog("Can't start ESRGAN: no selected models with known upscale size");
            //    return null;
            //}           

            process.ErrorDataReceived += SortOutputHandler;
            process.OutputDataReceived += SortOutputHandler;
            process.StartInfo.CreateNoWindow = NoWindow;

            if (!Directory.Exists(LrPath))
            {
                Logger.Write(LrPath + " doen't exist!");
                return null;
            }

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;
            var filesNumber = Directory.GetFiles(InputDirectoryPath, "*", searchOption).Count();
            if (filesNumber == 0)
            {
                Logger.Write("No files in input folder.");
                return null;
            }
            SetTotalCounter(filesNumber * checkedModels.Count);
            //if (HotProfile.UseDifferentModelForAlpha)
            //    SetTotalCounter(FilesTotal + Directory.GetFiles(LrPath + "_alpha", "*", searchOption).Count());
            ResetDoneCounter();

            Logger.Write("Starting ESRGAN...");
            return process;
        }

        async Task<Process> BasicSR_Test(bool NoWindow, Profile HotProfile)
        {
            if (checkedModels.Count > 1 && HotProfile.UseDifferentModelForAlpha)
            {
                Logger.Write("Only single model must be selected when using different model for alpha");
                return null;
            }

            Process process = new Process();

            process.StartInfo.Arguments = $"{EsrganPath}";
            process.StartInfo.Arguments += Helper.GetCondaEnv(UseCondaEnv, CondaEnv);

            bool noValidModel = true;
            int upscaleMultiplayer = 0;

            List<TestConfig> configs = new List<TestConfig>();

            foreach (ModelInfo checkedModel in checkedModels)
            {
                TestConfig config = new TestConfig(checkedModel.FullName);

                if ((upscaleMultiplayer = await DetectModelUpscaleFactor(checkedModel)) == 0)
                    continue;
                noValidModel = false;

                config.Scale = upscaleMultiplayer;
                if (UseCPU)
                    config.GpuIds = null;
                TestDataset dataset = new TestDataset() { DatarootLR = LrPath, DatarootHR = ResultsPath };
                config.Datasets.Test = dataset;
                config.Path.Root = EsrganPath;
                configs.Add(config);
            }

            if (HotProfile.UseDifferentModelForAlpha)
            {
                TestConfig configAlpha = new TestConfig(HotProfile.ModelForAlpha.FullName);

                //detect upsacle factor for alpha model                
                bool validModelAlpha = false;
                int upscaleMultiplayerAlpha = 0;

                if ((upscaleMultiplayerAlpha = await DetectModelUpscaleFactor(HotProfile.ModelForAlpha)) == 0)
                {
                    validModelAlpha = true;
                }

                if (upscaleMultiplayer != upscaleMultiplayerAlpha)
                {
                    Logger.Write("Upscale size for rgb model and alpha model must be the same");
                    return null;
                }
                configAlpha.Scale = upscaleMultiplayerAlpha;
                if (UseCPU)
                    configAlpha.GpuIds = null;
                TestDataset dataset = new TestDataset() { DatarootLR = LrPath + $"_alpha{DirSeparator}*", DatarootHR = ResultsPath };
                configAlpha.Datasets.Test = dataset;
                if (validModelAlpha)
                    configs.Add(configAlpha);
            }
            if (noValidModel)
            {
                Logger.Write("Can't start BasicSR: no selected models with known upscale size");
                return null;
            }
            for (int i = 0; i < configs.Count; i++)
            {
                configs[i].SaveConfig($"testConfig_{i}", $"{EsrganPath}{DirSeparator}IEU_TestConfigs");
                process.StartInfo.Arguments += $" & python codes{DirSeparator}IEU_test.py -opt IEU_TestConfigs{DirSeparator}testConfig_{i}.json";
            }

            process.ErrorDataReceived += SortOutputHandler;
            process.OutputDataReceived += SortOutputHandler;
            process.StartInfo.CreateNoWindow = NoWindow;

            if (!Directory.Exists(LrPath))
            {
                Logger.Write(LrPath + " doen't exist!");
                return null;
            }
            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;
            SetTotalCounter(Directory.GetFiles(LrPath, "*", searchOption).Count() * checkedModels.Count);
            if (HotProfile.UseDifferentModelForAlpha)
                SetTotalCounter(FilesTotal + Directory.GetFiles(LrPath + "_alpha").Count());

            ResetDoneCounter();

            WriteTestScriptToDisk();

            Logger.Write("Starting BasicSR...");
            return process;
        }

        Process PthReader(string modelPath)
        {
            Process process = new Process();
            process.StartInfo.Arguments = $"{Helper.GetApplicationRoot()}";
            process.StartInfo.Arguments += Helper.GetCondaEnv(UseCondaEnv, CondaEnv);
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

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.EnableRaisingEvents = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"cd {process.StartInfo.Arguments.Replace("\"", "\\\"").Replace("&", "&&")}\"";
                Logger.Write(process.StartInfo.Arguments);
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
            if (InMemoryMode)
            {
                writer = process.StandardInput;
                WriteModelsToStream();
                if (IsSub)
                    WriteImageToStream(lrDict[lrDict.Keys.FirstOrDefault()]);
            }
            return tcs.Task;
        }

        public async Task<bool> CreateInterpolatedModel(string a, string b, double alpha, string outputName = "")
        {
            if (alpha <= 0.0 || alpha >= 1.0)
            {
                Logger.Write("Alpha should be between 0.0 and 1.0");
                Logger.Write($"Current value is: {alpha}");
                return false;
            }

            string outputPath;
            if (outputName != "")
                outputPath = $"{ModelsPath}{DirSeparator}{outputName}";
            else
                outputPath = $"{ModelsPath}{DirSeparator}{Path.GetFileNameWithoutExtension(a)}_{Path.GetFileNameWithoutExtension(b)}_interp_{alpha.ToString().Replace(",", "")}.pth";

            string script = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.Scripts.interpModels.py");
            File.WriteAllText(EsrganPath + $"{DirSeparator}interpModels.py", script);

            using (Process process = new Process())
            {
                process.StartInfo.Arguments = $"{EsrganPath}";
                process.StartInfo.Arguments += Helper.GetCondaEnv(UseCondaEnv, CondaEnv);
                process.StartInfo.Arguments += $" & python interpModels.py \"{a}\" \"{b}\" {alpha.ToString().Replace(",", ".")} \"{outputPath}\"";
                process.ErrorDataReceived += SortOutputHandler;
                process.OutputDataReceived += SortOutputHandler;
                int code = await RunProcessAsync(process);
                if (code == 0)
                {
                    Logger.Write("Finished interpolating!");
                    CreateModelTree();
                }
            }
            return true;

        }

        Dictionary<string, List<string>> compDict = new Dictionary<string, List<string>>();

        private async void SortOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!string.IsNullOrEmpty(outLine.Data)
                && outLine.Data != $"{EsrganPath}>"
                && outLine.Data != "^C"
                && !outLine.Data.Contains("UserWarning")
                && !outLine.Data.Contains("nn."))
            {
                if (outLine.Data.StartsWith("b'") && InMemoryMode)
                {
                    Regex regex = new Regex("(.*):::(.*):::(.*):::(.*)");
                    var img = regex.Match(outLine.Data).Groups[1].Value;
                    var path = regex.Match(outLine.Data).Groups[2].Value;
                    var origPath = regex.Match(outLine.Data).Groups[3].Value;
                    var modelName = regex.Match(outLine.Data).Groups[4].Value;
                    string base64img = img.Remove(0, 2);
                    base64img = base64img.Remove(base64img.Length - 1, 1);
                    regex = new Regex("(.*?)(_alpha)?(_[R|G|B])?_tile-[0-9]+(.*)");

                    MagickImage magickImage = MagickImage.FromBase64(base64img) as MagickImage;

                    //var origPath = regex.Match(path).Groups[1].Value.Replace(ResultsPath, InputDirectoryPath);
                    //var inputFile = Directory.GetFiles(InputDirectoryPath, $"*{Path.GetFileNameWithoutExtension(origPath)}*");
                    //var extension = Path.GetExtension(inputFile.FirstOrDefault());
                    //origPath = origPath + extension;
                    var match = regex.Match(origPath);
                    origPath = match.Groups[1].Value.Replace(LrPath, InputDirectoryPath) + match.Groups[4].Value;
                    if (origPath.Contains("([000])000)_memory_helper"))
                    {
                        File.Delete(origPath);
                        lrDict.Remove(origPath);
                        return;
                    }
                    if (!hrDict.ContainsKey(origPath))
                        hrDict.Add(origPath, new Dictionary<string, MagickImage>());
                    var hrTiles = hrDict[origPath];
                    hrTiles.Add(path.Replace(Path.GetExtension(path), ".png"), new MagickImage(magickImage));
                    if (!lrDict.ContainsKey(origPath))
                    {
                        Logger.Write($"Key for {origPath} is missing from LR dictionary!", Color.Red);
                        return;
                    }
                    var lrTiles = lrDict[origPath];
                    Logger.Write(path, Color.LightGreen);

                    if (hrTiles.Count == lrTiles.Count) //all tiles for current image
                    {
                        if (!IsSub)                        
                            await Merge(origPath);                        

                        if (!compDict.ContainsKey(modelName))
                            compDict.Add(modelName, new List<string>());
                        compDict[modelName].Add(origPath);

                        if (compDict[modelName].Count == fileQueuCount) //all images
                        {
                            writer.WriteLine("end"); //go to next model  

                            if (compDict.Keys.Count == checkedModels.Count &&
                                Array.TrueForAll<List<string>>(compDict.Values.ToArray(), x => x.Count == fileQueuCount))
                            {
                                if (!IsSub)
                                {
                                    WriteToStream.Completion.Wait();
                                    lrDict = new Dictionary<string, Dictionary<string, string>>();
                                    hrDict = new Dictionary<string, Dictionary<string, MagickImage>>();
                                }

                                compDict = new Dictionary<string, List<string>>();
                                writer.Close();
                                gpuMonitor.MonitorVramTokenSource?.Cancel();
                                return;
                            }

                            SetPipeline();

                            SearchOption searchOption = SearchOption.TopDirectoryOnly;
                            if (OutputDestinationMode == 3)
                                searchOption = SearchOption.AllDirectories;
                            DirectoryInfo inputDirectory = new DirectoryInfo(InputDirectoryPath);
                            FileInfo[] inputDirectoryFiles = inputDirectory.GetFiles("*", searchOption)
                                .Where(x => ImageFormatInfo.ImageExtensions.Contains(x.Extension.ToUpperInvariant())).ToArray();

                            fileQueue = CreateQueue(inputDirectoryFiles);
                            fileQueuCount = fileQueue.Count;

                            var firstFile = fileQueue.Dequeue();
                            hrDict.Add(firstFile.FullName, new Dictionary<string, MagickImage>());
                            SplitImage.Post(firstFile);
                        }
                    }
                    return;
                }
                if (Regex.IsMatch(outLine.Data, "^[0-9]+ .*$"))
                {
                    IncrementDoneCounter();
                    ReportProgress();
                    Logger.Write(outLine.Data, Color.LightGreen);
                }
                else
                    Logger.Write(outLine.Data);
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

        #endregion

        #region GPU

        [Category("Exposed")]
        [ProtoMember(48)]
        public int magicNumberFor4x { get; set; } = 100;
        [Category("Exposed")]
        [ProtoMember(49)]
        public int magicNumberFor1x { get; set; } = 200;

        GpuMonitor gpuMonitor;

        public async Task AutoSetTileSize()
        {
            int modelScale = 1;
            checkedModels = SelectedModelsItems;
            if (checkedModels.Count == 0)
            {
                Logger.Write("No models checked, assuming model size 4x for auto tile size.");
                modelScale = 4;
            }
            else
            {                
                foreach (ModelInfo checkedModel in checkedModels)
                {
                    if(checkedModel.UpscaleFactor == 0)
                        if ((checkedModel.UpscaleFactor = await DetectModelUpscaleFactor(checkedModel)) == 0)
                            continue;
                    if (modelScale < checkedModel.UpscaleFactor) modelScale = checkedModel.UpscaleFactor;
                }
            }

            gpuMonitor.vcurMemory = (gpuMonitor.gpu.MemoryInformation.CurrentAvailableDedicatedVideoMemoryInkB / 1000);
            Logger.Write($"Currently available VRAM: {gpuMonitor.vcurMemory} MB");

            int magicNumber = magicNumberFor4x;
            if (modelScale == 1)
                magicNumber = magicNumberFor1x;
            if (modelScale == 8)
                magicNumber = magicNumberFor4x / 2;
            var newmax = (int)gpuMonitor.vcurMemory * magicNumber;

            MaxTileResolutionWidth = MaxTileResolutionHeight = (int)Math.Sqrt(newmax);

            MaxTileResolution = newmax;
            Logger.Write($"Setting max tile size to {MaxTileResolutionWidth}x{MaxTileResolutionHeight}");
        }

        #endregion

        BatchValues batchValues;
        async public Task SplitUpscaleMerge()
        {
            if (CurrentProfile.UseModel == true)
                checkedModels = new List<ModelInfo>() { CurrentProfile.Model };
            else
                checkedModels = SelectedModelsItems;

            if (checkedModels.Count == 0)
            {
                Logger.Write("No models selected!");
                return;
            }

            if (!InMemoryMode)
                await SplitUpscaleMergeNormal();
            else
                await SplitUpscaleMergeInMemory();
        }              
        async public Task SplitUpscaleMergeNormal()
        {            
            await Split();
            bool upscaleSuccess = await Upscale(HidePythonProcess);
            if (upscaleSuccess)
                await Merge();
        }
        async public Task SplitUpscaleMergeInMemory()
        {
            SetPipeline();

            batchValues = new BatchValues()
            {
                MaxTileResolution = MaxTileResolution,
                MaxTileH = MaxTileResolutionHeight,
                MaxTileW = MaxTileResolutionWidth,
                OutputMode = OutputDestinationMode,
                OverwriteMode = OverwriteMode,
                OverlapSize = OverlapSize,
                Padding = PaddingSize,
                //Seamless = 
            };

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;
            DirectoryInfo inputDirectory = new DirectoryInfo(InputDirectoryPath);
            FileInfo[] inputDirectoryFiles = inputDirectory.GetFiles("*", searchOption)
                .Where(x => ImageFormatInfo.ImageExtensions.Contains(x.Extension.ToUpperInvariant())).ToArray();

            if (inputDirectoryFiles.Count() == 0)
            {
                Logger.Write("No input images.");
                return;
            }

            fileQueue = CreateQueue(inputDirectoryFiles);
            fileQueuCount = fileQueue.Count;

            await Upscale(HidePythonProcess, async: false);
            lrDict = new Dictionary<string, Dictionary<string, string>>();
            hrDict = new Dictionary<string, Dictionary<string, MagickImage>>();

            ResetTotalCounter();
            ResetDoneCounter();
            SetTotalCounter(inputDirectoryFiles.Length);
            ReportProgress();

            var firstFile = fileQueue.Dequeue();
            hrDict.Add(firstFile.FullName, new Dictionary<string, MagickImage>());

            if (AutoSetTileSizeEnable)
                await AutoSetTileSize();

            SplitImage.Post(firstFile);
            await WriteToStream.Completion.ConfigureAwait(false);
        }

        #region INMEMORY
        [Category("Exposed")][ProtoMember(51)]
        public int InMemoryMaxSplit { get; set; } = 2;
        Queue<FileInfo> fileQueue;
        int fileQueuCount = 0;
        Queue<FileInfo> CreateQueue(FileInfo[] files)
        {
            Queue<FileInfo> fileQueue = new Queue<FileInfo>();
            if (CreateMemoryImage)
            {
                var path = $"{InputDirectoryPath}{DirSeparator}([000])000)_memory_helper.png";
                Image image = Image.Black(MaxTileResolutionWidth, MaxTileResolutionHeight);
                image.WriteToFile(path);
                fileQueue.Enqueue(new FileInfo(path));
            }
            foreach (var file in files)
                fileQueue.Enqueue(file);
            return fileQueue;
        }

        ActionBlock<Dictionary<string, string>> WriteToStream;
        TransformBlock<FileInfo, Dictionary<string, string>> SplitImage;
        void SetPipeline()
        {
            WriteToStream = new ActionBlock<Dictionary<string, string>>(async images =>
            {
                await WriteImageToStream(images);
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
            });

            SplitImage = new TransformBlock<FileInfo, Dictionary<string, string>>(async file =>
            {
                await Split(file);

                return lrDict[file.FullName];
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = InMemoryMaxSplit
            });
            SplitImage.LinkTo(WriteToStream, new DataflowLinkOptions { PropagateCompletion = true });

        }
        #endregion

        #region PREVIEW

        private IEU previewIEU;

        public string PreviewDirPath = "";

        void SetPreviewIEU(ref IEU previewIEU)
        {
            string previewResultsDirPath = PreviewDirPath + $"{DirSeparator}results";
            string previewLrDirPath = PreviewDirPath + $"{DirSeparator}LR";
            string previewInputDirPath = PreviewDirPath + $"{DirSeparator}input";

            previewIEU.EsrganPath = EsrganPath;
            previewIEU.LrPath = previewLrDirPath;
            previewIEU.InputDirectoryPath = previewInputDirPath;
            previewIEU.ResultsPath = previewResultsDirPath;
            previewIEU.OutputDirectoryPath = PreviewDirPath;
            previewIEU.MaxTileResolution = MaxTileResolution;
            previewIEU.OverlapSize = OverlapSize;
            previewIEU.OutputDestinationMode = 0;
            previewIEU.UseCPU = UseCPU;
            previewIEU.UseBasicSR = UseBasicSR;
            previewIEU.CurrentProfile = CurrentProfile.Clone();
            previewIEU.OverwriteMode = 0;
            previewIEU.CurrentProfile.UseOriginalImageFormat = false;
            previewIEU.CurrentProfile.selectedOutputFormat = CurrentProfile.pngFormat;
            previewIEU.DisableRuleSystem = true;
            previewIEU.CreateMemoryImage = false;
            previewIEU.UseCondaEnv = UseCondaEnv;
            previewIEU.CondaEnv = CondaEnv;
            previewIEU.InMemoryMode = InMemoryMode;
            previewIEU.UseOldVipsMerge = UseOldVipsMerge;
            previewIEU.EnableBlend = EnableBlend;
            previewIEU.UseImageMagickMerge = UseImageMagickMerge;
            previewIEU.AutoSetTileSizeEnable = AutoSetTileSizeEnable;
            previewIEU.VramMonitorEnable = false;
            previewIEU.DebugMode = DebugMode;
            previewIEU.PaddingSize = PaddingSize;

            previewIEU.batchValues = new BatchValues()
            {
                MaxTileResolution = MaxTileResolution,
                MaxTileH = MaxTileResolutionHeight,
                MaxTileW = MaxTileResolutionWidth,
                OutputMode = 0,
                OverwriteMode = 0,
                OverlapSize = OverlapSize,
                Padding = PaddingSize,
                //Seamless = 
            };
        }

        async public Task<bool> Preview(string imagePath, System.Drawing.Image image, string modelPath, bool saveAsPng = false, bool copyToOriginal = false, string copyDestination = "")
        {
            if (!InMemoryMode)
                return await PreviewNormal(imagePath, image, modelPath, saveAsPng, copyToOriginal, copyDestination);
            else
                return await PreviewInMemory(imagePath, image, modelPath, saveAsPng, copyToOriginal);
        }

        async public Task<bool> PreviewNormal(string imagePath, System.Drawing.Image image, string modelPath, bool saveAsPng = false, bool copyToOriginal = false, string copyDestination = "")
        {
            PreviewDirPath = $"{EsrganPath}{DirSeparator}IEU_preview";
            string previewResultsDirPath = PreviewDirPath + $"{DirSeparator}results";
            string previewLrDirPath = PreviewDirPath + $"{DirSeparator}LR";
            string previewInputDirPath = PreviewDirPath + $"{DirSeparator}input";

            List<DirectoryInfo> previewFolders = new List<DirectoryInfo>() {
                new DirectoryInfo(PreviewDirPath),
                new DirectoryInfo(previewResultsDirPath),
                new DirectoryInfo(previewLrDirPath),
                new DirectoryInfo(previewInputDirPath) };

            foreach (var folder in previewFolders)
            {
                if (!folder.Exists)
                    folder.Create();
                else
                    folder.GetFiles("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
            }

            FileInfo previewOriginal = new FileInfo(previewInputDirPath + $"{DirSeparator}preview.png");
            FileInfo preview = new FileInfo(PreviewDirPath + $"{DirSeparator}preview.png");

            Bitmap i2;
            if (image == null)
                if (File.Exists(imagePath))
                    i2 = ImageOperations.LoadImageToBitmap(imagePath) as Bitmap;
                else
                    return false;
            else
                i2 = new Bitmap(image);

            i2.Save(previewOriginal.FullName, ImageFormat.Png);
            i2.Dispose();

            if (previewIEU == null)
                previewIEU = new IEU(true);

            SetPreviewIEU(ref previewIEU);

            ModelInfo previewModelInfo = new ModelInfo(Path.GetFileNameWithoutExtension(modelPath), modelPath);

            previewIEU.SelectedModelsItems = new List<ModelInfo>() { previewModelInfo };           

            await previewIEU.Split(new FileInfo[] { previewOriginal });            

            bool success = await previewIEU.Upscale(true);

            if (!success)
            {
                File.WriteAllText(PreviewDirPath + $"{DirSeparator}log.txt", previewIEU.Logger.Logs);
                return false;
            }
            CreateModelTree();
            if (!saveAsPng)
            {
                previewIEU.CurrentProfile.UseOriginalImageFormat = CurrentProfile.UseOriginalImageFormat;
                previewIEU.CurrentProfile.selectedOutputFormat = CurrentProfile.selectedOutputFormat;
            }

            await previewIEU.Merge();

            ImageFormatInfo outputFormat = CurrentProfile.FormatInfos.Where(x => x.Extension.Equals(".png", StringComparison.InvariantCultureIgnoreCase)).First();
            if (!saveAsPng)
            {
                if (CurrentProfile.UseOriginalImageFormat)
                    outputFormat = CurrentProfile.FormatInfos.Where(x => x.Extension.Equals(Path.GetExtension(imagePath), StringComparison.InvariantCultureIgnoreCase)).First(); //hack, may be bad
                else
                    outputFormat = CurrentProfile.selectedOutputFormat;
                preview = new FileInfo(PreviewDirPath + $"{DirSeparator}preview{outputFormat.Extension}");
            }
            File.WriteAllText(PreviewDirPath + $"{DirSeparator}log.txt", previewIEU.Logger.Logs);
            if (!File.Exists(preview.FullName))
                return false;

            if (copyToOriginal)
            {
                string modelName = Path.GetFileNameWithoutExtension(modelPath);
                string dir = Path.GetDirectoryName(imagePath);
                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                if (copyDestination == "")
                    copyDestination = $"{ dir }{DirSeparator}{fileName}_{modelName}{outputFormat.Extension}";
                //else
                //{
                //    copyDestination = $"{ Path.GetDirectoryName(copyDestination) }{DirectorySeparator}{Path.GetFileNameWithoutExtension(copyDestination)}_{modelName}{outputFormat.Extension}";
                //}
                File.Copy(preview.FullName, copyDestination, true);
            }
            return true;
        }

        async public Task<bool> PreviewInMemory(string imagePath, System.Drawing.Image image, string modelPath, bool saveAsPng = false, bool copyToOriginal = false)
        {
            PreviewDirPath = $"{EsrganPath}{DirSeparator}IEU_preview";
            string previewResultsDirPath = PreviewDirPath + $"{DirSeparator}results";
            string previewLrDirPath = PreviewDirPath + $"{DirSeparator}LR";
            string previewInputDirPath = PreviewDirPath + $"{DirSeparator}input";

            List<DirectoryInfo> previewFolders = new List<DirectoryInfo>() {
                new DirectoryInfo(PreviewDirPath),
                new DirectoryInfo(previewResultsDirPath),
                new DirectoryInfo(previewLrDirPath),
                new DirectoryInfo(previewInputDirPath) };

            foreach (var folder in previewFolders)
            {
                if (!folder.Exists)
                    folder.Create();
                else
                    folder.GetFiles("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
            }

            FileInfo previewOriginal = new FileInfo(previewInputDirPath + $"{DirSeparator}preview.png");
            FileInfo preview = new FileInfo(PreviewDirPath + $"{DirSeparator}preview.png");

            Bitmap i2;
            if (image == null)
                if (File.Exists(imagePath))
                    i2 = ImageOperations.LoadImageToBitmap(imagePath) as Bitmap;
                else
                    return false;
            else
                i2 = new Bitmap(image);

            i2.Save(previewOriginal.FullName, ImageFormat.Png);
            i2.Dispose();

            //if (previewIEU == null)
            previewIEU = new IEU(true);

            SetPreviewIEU(ref previewIEU);
            previewIEU.lrDict = new Dictionary<string, Dictionary<string, string>>();
            previewIEU.hrDict = new Dictionary<string, Dictionary<string, MagickImage>>
            {
                { previewOriginal.FullName, new Dictionary<string, MagickImage>() }
            };
            previewIEU.fileQueue = new Queue<FileInfo>();
            previewIEU.fileQueuCount = 1;

            ModelInfo previewModelInfo = new ModelInfo(Path.GetFileNameWithoutExtension(modelPath), modelPath);
            previewIEU.SelectedModelsItems = new List<ModelInfo>() { previewModelInfo };

            await previewIEU.Split(previewOriginal);
            
            SetPipeline();
            bool success = await previewIEU.Upscale(true);
            if (!success)
                File.WriteAllText(PreviewDirPath + $"{DirSeparator}log.txt", previewIEU.Logger.Logs);

            CreateModelTree();
            if (!saveAsPng)
            {
                //previewIEU.CurrentProfile.UseOriginalImageFormat = CurrentProfile.UseOriginalImageFormat;
                previewIEU.CurrentProfile.selectedOutputFormat = CurrentProfile.selectedOutputFormat;
            }
            await previewIEU.Merge(previewOriginal.FullName);

            ImageFormatInfo outputFormat = CurrentProfile.FormatInfos.Where(x => x.Extension.Equals(".png", StringComparison.InvariantCultureIgnoreCase)).First();
            if (!saveAsPng)
            {
                //if (CurrentProfile.UseOriginalImageFormat)
                //    outputFormat = CurrentProfile.FormatInfos.Where(x => x.Extension.Equals(Path.GetExtension(imagePath), StringComparison.InvariantCultureIgnoreCase)).First();
                //else
                outputFormat = CurrentProfile.selectedOutputFormat;
                preview = new FileInfo(PreviewDirPath + $"{DirSeparator}preview{outputFormat.Extension}");
            }
            if (!File.Exists(Path.ChangeExtension(preview.FullName, ".png")))
                return false;

            if (copyToOriginal)
            {
                string modelName = Path.GetFileNameWithoutExtension(modelPath);
                string dir = Path.GetDirectoryName(imagePath);
                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                string destination = $"{ dir }{DirSeparator}{ fileName}_{modelName}{outputFormat.Extension}";
                File.Copy(preview.FullName, destination);
            }
            return true;
        }

        #endregion

        #region IMAGE INTERPOLATION

        public async void InterpolateFolders
            (string originalPath, string resultsAPath, string resultsBPath, string destinationPath, double alpha, Profile HotProfile = null)
        {
            if (HotProfile == null)
                HotProfile = GlobalProfile;
            DirectoryInfo originalDirectory = new DirectoryInfo(originalPath);
            DirectoryInfo resultsADirectory = new DirectoryInfo(resultsAPath);
            DirectoryInfo resultsBDirectory = new DirectoryInfo(resultsBPath);
            DirectoryInfo destinationDirectory = new DirectoryInfo(destinationPath);

            FileInfo[] originalFiles = originalDirectory.GetFiles("*", SearchOption.AllDirectories);
            if (originalFiles.Count() == 0)
            {
                Logger.Write("No files in input folder!", Color.Red);
                return;
            }

            ResetDoneCounter();
            SetTotalCounter(originalFiles.Count());

            await Task.Run(() => Parallel.ForEach(originalFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = MaxConcurrency }, file =>
            {
                string originalExtension = file.Extension;
                string baseFilePath = file.FullName.Replace(originalDirectory.FullName, "").Replace(originalExtension, HotProfile.selectedOutputFormat.Extension);
                string pathA = resultsADirectory.FullName + baseFilePath;
                string pathB = resultsBDirectory.FullName + baseFilePath;
                string destinationFilePath = destinationPath + baseFilePath;

                if (!File.Exists(pathA) || !File.Exists(pathB))
                {
                    Logger.Write($"Results missing for {file.FullName}, skipping", Color.Red);
                    IncrementDoneCounter(false);
                    ReportProgress();
                    return;
                }
                //if (File.Exists(destinationFilePath))
                //    return;
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));
                InterpolateImages(pathA, pathB, destinationFilePath, alpha);
            }));

        }

        void InterpolateImages(string pathA, string pathB, string destinationFilePath, double alpha)
        {
            var result = ImageInterpolation.Interpolate(pathA, pathB, alpha);
            if (result != null)
            {
                ImageFormatInfo outputFormat = CurrentProfile.selectedOutputFormat;

                if (outputFormat.Extension == ".dds")
                    WriteToFileDds(result, destinationFilePath, CurrentProfile);
                else
                    result.Write(destinationFilePath);

                Logger.Write($"{Path.GetFileName(destinationFilePath)}", Color.LightGreen);
                IncrementDoneCounter();
            }
            else
            {
                Logger.Write($"{Path.GetFileName(destinationFilePath)}: failed to interpolate", Color.Red);
                IncrementDoneCounter(false);
            }

            ReportProgress();
        }

        public bool InterpolateImages(System.Drawing.Image imageA, System.Drawing.Image imageB, string destinationPath, double alpha)
        {
            var result = ImageInterpolation.Interpolate(imageA, imageB, destinationPath, alpha);
            if (result.Item1)
                Logger.Write($"{Path.GetFileName(destinationPath)}", Color.LightGreen);
            else
            {
                Logger.Write($"{Path.GetFileName(destinationPath)}: failed to interpolate.\n{result.Item2}", Color.Red);
                return false;
            }
            return true;
        }

        #endregion
    }

    public class Logger : ReactiveObject
    {
        private string logs;
        [Browsable(false)]
        public string Logs
        {
            get => logs;
            set => this.RaiseAndSetIfChanged(ref logs, value);
        }

        public SourceList<LogMessage> Log = new SourceList<LogMessage>();

        bool _debugMode = false;    
        public bool DebugMode
        {
            get => _debugMode;
            set => this.RaiseAndSetIfChanged(ref _debugMode, value);
        }

        public void Write(string text)
        {
            Write(text, Color.White);
        }

        public void WriteDebug(string text)
        {
            if (DebugMode)
                Write(text, Color.FromArgb(225, 0, 130));
        }

        public void Write(string text, Color color)
        {
            Write(new LogMessage(text, color));
        }

        public void Write(LogMessage message)
        {
            Log.Add(message);
            Logs += message.Text;
        }

        public void WriteOpenError(FileInfo file, string exMessage)
        {
            Write($"{exMessage}", Color.Red);
            Write($"Skipping <{file.Name}>...", Color.Red);
        }

    }
    public class GpuMonitor
    {
        public uint vmemory, vcurMemory;
        public PhysicalGPU gpu;       
        public CancellationTokenSource MonitorVramTokenSource;
        Logger Logger;

        public GpuMonitor(Logger logger)
        {
            Logger = logger;
        }

        public void GetVRAM()
        {
            NVIDIA.Initialize();
            var a = PhysicalGPU.GetPhysicalGPUs();
            if (a.Length == 0) return;
            gpu = a[0];
            vmemory = (gpu.MemoryInformation.AvailableDedicatedVideoMemoryInkB / 1000);
            vcurMemory = (gpu.MemoryInformation.CurrentAvailableDedicatedVideoMemoryInkB / 1000);
            Logger.Write($"{gpu.FullName}: {vmemory} MB");
            Logger.Write($"Currently available VRAM: {vcurMemory} MB");
        }
        public void MonitorVramStart(bool VramMonitorEnable, int VramMonitorFrequency)
        {
            MonitorVramTokenSource = new CancellationTokenSource();

            if (VramMonitorEnable)
            {
                CancellationToken ct = MonitorVramTokenSource.Token;

                NVIDIA.Initialize();
                var a = PhysicalGPU.GetPhysicalGPUs();
                var gpu = a[0];

                var task = Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();

                    while (true)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                            // Clean up here, then...
                            //ct.ThrowIfCancellationRequested();
                        }
                        var usage = (gpu.MemoryInformation.AvailableDedicatedVideoMemoryInkB - gpu.MemoryInformation.CurrentAvailableDedicatedVideoMemoryInkB) / 1000;
                        Logger.Write($"Using {usage} MB");
                        Thread.Sleep(VramMonitorFrequency);
                    }
                }, MonitorVramTokenSource.Token);
            }
        }

    }
}






