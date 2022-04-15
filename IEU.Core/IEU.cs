using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
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
using Newtonsoft.Json;
using PaintDotNet;
using ProtoBuf;
using ReactiveUI;
using Color = System.Drawing.Color;
using Image = NetVips.Image;
using Path = System.IO.Path;
using ReactiveCommand = ReactiveUI.ReactiveCommand;
using Unit = System.Reactive.Unit;
using Timer = System.Timers.Timer;
using System.Timers;

//TODO:
//new filter: (doesn't)have result
//identical filenames with different extension
[assembly: InternalsVisibleTo("ImageEnhancingUtility.Tests")]
namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    public partial class IEU : ReactiveObject
    {
        public readonly string AppVersion = "0.13.05";
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
        private bool _autoSetTileSizeEnable = false;
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

        public ReactiveCommand<FileInfo[], Unit> SplitCommand { get; }
        public ReactiveCommand<Tuple<bool, Profile>, bool> UpscaleCommand { get; }
        public ReactiveCommand<Unit, Unit> MergeCommand { get; }
        public ReactiveCommand<Unit, bool> SplitUpscaleMergeCommand { get; }

        #region CONSTRUCTOR       
       
        public IEU(bool isSub = false)
        {
            IsSub = isSub;

            Task splitFunc(FileInfo[] x) => Split();
            SplitCommand = ReactiveCommand.CreateFromTask((Func<FileInfo[], Task>)splitFunc);

            Task<bool> upscaleFunc(Tuple<bool, Profile> x) => Upscale(x != null && x.Item1, x?.Item2);
            UpscaleCommand = ReactiveCommand.CreateFromTask((Func<Tuple<bool, Profile>, Task<bool>>)upscaleFunc);

            MergeCommand = ReactiveCommand.CreateFromTask(Merge);

            Task<bool> runAllFunc() => SplitUpscaleMerge();
            SplitUpscaleMergeCommand = ReactiveCommand.CreateFromTask(runAllFunc);

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
            CurrentProfile = GlobalProfile.Clone();
            CurrentFilter = GlobalFilter.Clone();
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

        public async Task CreateModelTree()
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
                {
                    var mdl = new ModelInfo(fi.Name, fi.FullName, d.Name);
                    mdl.UpscaleFactor = await DetectModelUpscaleFactor(mdl);
                    newList.Add(mdl);
                }

            foreach (FileInfo fi in di.GetFiles("*.pth", SearchOption.TopDirectoryOnly))
            {
                var mdl = new ModelInfo(fi.Name, fi.FullName);
                mdl.UpscaleFactor = await DetectModelUpscaleFactor(mdl);
                newList.Add(mdl);
            }            

            ModelsItems.Clear();
            ModelsItems.AddRange(newList);

            if (ModelsItems != null && ModelsItems.Count > 0)
                LastModelForAlphaPath = ModelsItems.Items.ToArray()[0].FullName;
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
            double fdd = FilesDone;
            if (FilesDone == 0 && FilesTotal != 0)
                fdd = 0.001;
            if(FilesDone == FilesTotal && InMemoryMode)
                PrintTime();
            ProgressBarValue = (fdd / FilesTotal) * 100.00;
            ProgressLabel = $@"{FilesDone}/{FilesTotal}";
        }
        #endregion
               
        [Category("Exposed")]
        [ProtoMember(52)]
        public int MaxConcurrency { get; set; } = 99;

        BatchValues batchValues;
        void WriteBatchValues(BatchValues batchValues, string path = "CurrentSession.json")
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(batchValues));
        }
        BatchValues ReadBatchValues(string path = "CurrentSession.json")
        {           
            var batch = JsonConvert.DeserializeObject<BatchValues>(File.ReadAllText(path));
            OutputDestinationMode = batch.OutputMode;
            OverwriteMode = batch.OverwriteMode;
            MaxTileResolutionWidth = batch.MaxTileW;
            MaxTileResolutionHeight = batch.MaxTileH;
            MaxTileResolution = batch.MaxTileResolution;
            OverlapSize = batch.OverlapSize;
            CurrentProfile.PaddingSize = batch.Padding;
            ResultSuffix = batch.ResultSuffix;

            return batch;
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
        
        #region UPSCALE               

        async public Task<bool> Upscale(bool NoWindow = true, Profile HotProfile = null, bool async = true)
        {
            if (HotProfile == null)
                HotProfile = GlobalProfile;
            if (DisableRuleSystem)
                HotProfile = CurrentProfile;
            if (!IsSub)
                SaveSettings();

            if (HotProfile.UseModel && HotProfile.Model != null && !IsSub)
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
                if (CurrentProfile.UseJoey)
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
                Logger.Write("ESRGAN starts running in background", Color.LightGreen);
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
                checkedModel.UpscaleFactor = upscaleMultiplayer;
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
                process.OutputDataReceived -= EsrganOutputHandler;
                process.OutputDataReceived -= PthReaderOutputHandler;
                process.ErrorDataReceived -= EsrganOutputHandler;
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
                    if (checkedModel.UpscaleFactor == 0)
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

                if (HotProfile.ModelForAlpha.UpscaleFactor != 0)                
                    validModelAlpha = true;                

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

            process.ErrorDataReceived += EsrganOutputHandler;
            process.OutputDataReceived += EsrganOutputHandler;
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

        JoeyEsrgan _joeyEsrgan = new JoeyEsrgan();
        [ProtoMember(62)]
        [Browsable(false)]
        public JoeyEsrgan JoeyEsrgan
        {
            get => _joeyEsrgan;
            set => this.RaiseAndSetIfChanged(ref _joeyEsrgan, value);
        }

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

            if (checkedModels.Count == 1)
                JoeyEsrgan.ModelsArgument = $"{checkedModels[0].FullName}";
            else
            {
                JoeyEsrgan.ModelsArgument = $"{checkedModels[0].Name}";
                for (int i = 1; i < checkedModels.Count; i++)
                    JoeyEsrgan.ModelsArgument += $">{checkedModels[i].Name}";               
            }

            JoeyEsrgan.Input = InputDirectoryPath;
            JoeyEsrgan.Output = OutputDirectoryPath;

            //JoeyEsrgan.TileSize = (int)Math.Round(Math.Sqrt(MaxTileResolution));
            //JoeyEsrgan.SeamlessMod = HotProfile.SeamlessTexture?SeamlessMod.Tile:SeamlessMod.None;
            //JoeyEsrgan.CPU = UseCPU;

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

            process.ErrorDataReceived += EsrganOutputHandler;
            process.OutputDataReceived += EsrganOutputHandler;
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

                if (checkedModel.UpscaleFactor == 0)
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

                if (HotProfile.ModelForAlpha.UpscaleFactor == 0)
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

            process.ErrorDataReceived += EsrganOutputHandler;
            process.OutputDataReceived += EsrganOutputHandler;
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

            process.ErrorDataReceived += EsrganOutputHandler;
            process.OutputDataReceived += PthReaderOutputHandler;

            return process;
        }

        public Task<int> RunProcessAsync(Process process, bool ignoreInMemory = false)
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
                process.OutputDataReceived -= EsrganOutputHandler;
                process.OutputDataReceived -= PthReaderOutputHandler;
                process.ErrorDataReceived -= EsrganOutputHandler;
                process.Dispose();
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (InMemoryMode && !ignoreInMemory) //for preview
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
                process.ErrorDataReceived += EsrganOutputHandler;
                process.OutputDataReceived += EsrganOutputHandler;
                int code = await RunProcessAsync(process, true);
                if (code == 0)
                {
                    Logger.Write("Finished interpolating!");
                    await CreateModelTree();
                }
            }
            return true;

        }

        Dictionary<string, List<string>> compDict = new Dictionary<string, List<string>>();

        private async void EsrganOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
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
                        {
                            var res = batchValues.images[origPath].results.Where(x => Path.GetFileNameWithoutExtension(x.Model.Name) == modelName).First();
                            await Merge(origPath, res);
                        }

                        if (!compDict.ContainsKey(modelName))
                            compDict.Add(modelName, new List<string>());
                        compDict[modelName].Add(origPath);

                        if (compDict[modelName].Count == fileQueuCount) //all images
                        {
                            writer.WriteLine("end"); //go to next model  

                            if (compDict.Keys.Count == checkedModels.Count &&
                                Array.TrueForAll(compDict.Values.ToArray(), x => x.Count == fileQueuCount))
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
                            
                            FileInfo[] inputDirectoryFiles = batchValues.images.Keys.Select(x => new FileInfo(x)).ToArray();                            
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

        private void PthReaderOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
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
        public int magicNumberFor4x { get; set; } = 95;
        [Category("Exposed")]
        [ProtoMember(49)]
        public int magicNumberFor1x { get; set; } = 195;

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
                
        Stopwatch stopWatch;

        void PrintTime()
        {
            stopWatch.Stop();
            var ts = stopWatch.Elapsed;
            var st = String.Format("{0:00}:{1:00}.{2:00}",
                                    ts.Hours * 60 + ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Logger.Write($"Finished in {st}");
        }
        

        async public Task<bool> SplitUpscaleMerge()
        {
            if (CurrentProfile.UseModel == true)
                checkedModels = new List<ModelInfo>() { CurrentProfile.Model };
            else
                checkedModels = SelectedModelsItems;

            if (checkedModels.Count == 0)
            {
                Logger.Write("No models selected!");
                return false;
            }

            stopWatch = new Stopwatch();
            stopWatch.Start();

            if (CurrentProfile.UseJoey)
            {
                bool upscaleSuccess = await Upscale(HidePythonProcess);                
                PrintTime();
                return true;
            }

            if (InMemoryMode)
                await SplitUpscaleMergeInMemory();            
            else
            {
                await SplitUpscaleMergeNormal();
                PrintTime();
                return true;
            }
            return false;
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
                Padding = CurrentProfile.PaddingSize              
            };            

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;
            DirectoryInfo inputDirectory = new DirectoryInfo(InputDirectoryPath);
            FileInfo[] inputDirectoryFiles = inputDirectory.GetFiles("*", searchOption)
                .Where(x => ImageFormatInfo.ImageExtensions.Contains(x.Extension.ToUpperInvariant())).ToArray();
            //FileInfo[] inputDirectoryFiles = batchValues.images.Keys.Select(x => new FileInfo(x)).ToArray();            

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
            SetTotalCounter(inputDirectoryFiles.Count() * checkedModels.Count);          
            ReportProgress();

            var firstFile = fileQueue.Dequeue();
            hrDict.Add(firstFile.FullName, new Dictionary<string, MagickImage>());

            if (AutoSetTileSizeEnable)
                await AutoSetTileSize();

            mergeTasks = new List<Task>();   

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

        List<Task> mergeTasks;

        TransformBlock<FileInfo, Dictionary<string, string>> SplitImage;
        ActionBlock<Dictionary<string, string>> WriteToStream;        
        void SetPipeline()
        {
            SplitImage = new TransformBlock<FileInfo, Dictionary<string, string>>(async file =>
            {
                await Split(file);

                return lrDict[file.FullName];
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = InMemoryMaxSplit
            });

            WriteToStream = new ActionBlock<Dictionary<string, string>>(async images =>
            {
                await WriteImageToStream(images);
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
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
            previewIEU.CurrentProfile.PaddingSize = CurrentProfile.PaddingSize;           

            previewIEU.batchValues = new BatchValues()
            {
                MaxTileResolution = MaxTileResolution,
                MaxTileH = MaxTileResolutionHeight,
                MaxTileW = MaxTileResolutionWidth,
                OutputMode = 0,
                OverwriteMode = 0,
                OverlapSize = OverlapSize,
                Padding = CurrentProfile.PaddingSize                
            };
        }

        public string PreviewLog { get => previewIEU.Logger.Logs; }
        async public Task<bool> Preview(string imagePath, System.Drawing.Image image, ModelInfo previewModel, bool saveAsPng = false, bool copyToOriginal = false, string copyDestination = "")
        {
            if (!InMemoryMode)
                return await PreviewNormal(imagePath, image, previewModel, saveAsPng, copyToOriginal, copyDestination);
            else
                return await PreviewInMemory(imagePath, image, previewModel, saveAsPng, copyToOriginal);
        }

        async public Task<bool> PreviewNormal
            (string imagePath, System.Drawing.Image image, ModelInfo previewModel,
            bool saveAsPng = false, bool copyToOriginal = false, string copyDestination = "")
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

            previewIEU = new IEU(true);

            SetPreviewIEU(ref previewIEU);            

            previewIEU.SelectedModelsItems = new List<ModelInfo>() { previewModel };           

            await previewIEU.Split(new FileInfo[] { previewOriginal });            

            bool success = await previewIEU.Upscale(true);

            if (!success)
            {
                File.WriteAllText(PreviewDirPath + $"{DirSeparator}log.txt", previewIEU.Logger.Logs);
                return false;
            }
           
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
                string modelName = Path.GetFileNameWithoutExtension(previewModel.Name);
                string dir = Path.GetDirectoryName(imagePath);
                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                if (copyDestination == "")
                    copyDestination = $"{ dir }{DirSeparator}{fileName}_{modelName}{outputFormat.Extension}";               
                File.Copy(preview.FullName, copyDestination, true);
            }
            return true;
        }

        async public Task<bool> PreviewInMemory(string imagePath, System.Drawing.Image image, ModelInfo previewModel, bool saveAsPng = false, bool copyToOriginal = false)
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
            
            previewIEU = new IEU(true);

            SetPreviewIEU(ref previewIEU);
            previewIEU.lrDict = new Dictionary<string, Dictionary<string, string>>();
            previewIEU.hrDict = new Dictionary<string, Dictionary<string, MagickImage>>
            {
                { previewOriginal.FullName, new Dictionary<string, MagickImage>() }
            };
            previewIEU.fileQueue = new Queue<FileInfo>();
            previewIEU.fileQueuCount = 1;
           
            previewIEU.SelectedModelsItems = new List<ModelInfo>() { previewModel };

            await previewIEU.Split(previewOriginal);
            
            SetPipeline();
            bool success = await previewIEU.Upscale(true);
            if (!success)
            {
                File.WriteAllText(PreviewDirPath + $"{DirSeparator}log.txt", previewIEU.Logger.Logs);
                return false;
            }
            
            if (!saveAsPng)
            {
                //previewIEU.CurrentProfile.UseOriginalImageFormat = CurrentProfile.UseOriginalImageFormat;
                previewIEU.CurrentProfile.selectedOutputFormat = CurrentProfile.selectedOutputFormat;
            }
            await previewIEU.Merge(previewOriginal.FullName, previewIEU.batchValues.images.Values.FirstOrDefault().results[0]);

            ImageFormatInfo outputFormat = CurrentProfile.FormatInfos.Where(x => x.Extension.Equals(".png", StringComparison.InvariantCultureIgnoreCase)).First();
            if (!saveAsPng)
            {
                //if (CurrentProfile.UseOriginalImageFormat)
                //    outputFormat = CurrentProfile.FormatInfos.Where(x => x.Extension.Equals(Path.GetExtension(imagePath), StringComparison.InvariantCultureIgnoreCase)).First();
                //else
                outputFormat = CurrentProfile.selectedOutputFormat;
                preview = new FileInfo(PreviewDirPath + $"{DirSeparator}preview{outputFormat.Extension}");
            }
            File.WriteAllText(PreviewDirPath + $"{DirSeparator}log.txt", previewIEU.Logger.Logs);
            if (!preview.Exists)
                return false;

            if (copyToOriginal)
            {
                string modelName = Path.GetFileNameWithoutExtension(previewModel.Name);
                string dir = Path.GetDirectoryName(imagePath);
                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                string destination = $"{ dir }{DirSeparator}{ fileName}_{modelName}{outputFormat.Extension}";
                File.Copy(preview.FullName, destination, true);
            }
            return true;
        }

        #endregion

        Process currentEsrganProcess;
        public void Stop()
        {
            if (currentEsrganProcess == null || currentEsrganProcess.HasExited)
                return;
            //cancelled = true;
            currentEsrganProcess.Kill();
        }

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

   }






