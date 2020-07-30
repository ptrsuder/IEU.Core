﻿using System;
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
[assembly: InternalsVisibleTo("ImageEnhancingUtility.Tests")]
namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    public partial class IEU : ReactiveObject
    {
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

        public readonly string AppVersion = "0.12.00";
        public readonly string GitHubRepoName = "IEU.Core";

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

        bool _createMemoryImage = true;
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
                    WriteToLog($"{value} is saved as model for alphas, but it is missing", Color.LightYellow);
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

        private bool _vramMonitorEnable = true;
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

        bool _useOldVipsMerge = false;
        [ProtoMember(47, IsRequired = true)]
        public bool UseOldVipsMerge
        {
            get => _useOldVipsMerge;
            set => this.RaiseAndSetIfChanged(ref _useOldVipsMerge, value);
        }

        #endregion

        #region MAINTAB_PROGRESS
        private string logs;
        [Browsable(false)]
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
                
        readonly string DirectorySeparator = Path.DirectorySeparatorChar.ToString();

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

            WriteToLog(RuntimeInformation.OSDescription);
            WriteToLog(RuntimeInformation.FrameworkDescription);                     

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
                GetVRAM();
            }
            catch
            {
                WriteToLog("Failed to get Nvidia GPU info.");
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
                WriteToLog($"{di.FullName} doesn't exist!");
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
        public void WriteToLog(string text)
        {
            WriteToLog(text, Color.White);
        }

        public void WriteToLogDebug(string text)
        {
            if (DebugMode)
                WriteToLog(text, Color.FromArgb(225, 0, 130));
        }

        public void WriteToLog(string text, Color color)
        {
            WriteToLog(new LogMessage(text, color));
        }

        public void WriteToLog(LogMessage message)
        {
            Log.Add(message);
            Logs += message.Text;
        }

        public void WriteToLogOpenError(FileInfo file, string exMessage)
        {
            WriteToLog($"{exMessage}", Color.Red);
            WriteToLog($"Skipping <{file.Name}>...", Color.Red);
        }

        private void ReportProgress()
        {
            double fdd = (double)FilesDone;
            if (FilesDone == 0 && FilesTotal != 0)
                fdd = 0.001;
            ProgressBarValue = (fdd / (double)FilesTotal) * 100.00;
            ProgressLabel = $@"{FilesDone}/{FilesTotal}";
        }
#endregion
        
        void ImagePreprocess(ref MagickImage image, Profile HotProfile)
        {
            if (HotProfile.ResizeImageBeforeScaleFactor != 1.0)
            {
                double scale = HotProfile.ResizeImageBeforeScaleFactor;
                int divider = scale < 1 ? (int)(1 / scale) : (int)scale;
                bool dimensionsAreOK = image.Width % divider == 0 && image.Height % divider == 0;
                if (!dimensionsAreOK && !HotProfile.SeamlessTexture)
                {
                    image = ImageOperations.PadImage(image, divider, divider);
                }
                image = ImageOperations.ResizeImage(image, HotProfile.ResizeImageBeforeScaleFactor, (FilterType)HotProfile.ResizeImageBeforeFilterType);
            }

            switch (HotProfile.NoiseReductionType)
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

        void ImagePostrpocess(ref MagickImage finalImage, Profile HotProfile)
        {
            MagickImage alphaChannel = null;
            if (!HotProfile.IgnoreAlpha && finalImage.HasAlpha && HotProfile.ThresholdAlphaEnabled)
                alphaChannel = finalImage.Separate(Channels.Alpha).First() as MagickImage;

            if (HotProfile.ThresholdBlackValue != 0)
            {
                finalImage.HasAlpha = false;
                if (HotProfile.ThresholdEnabled)
                {
                    WriteToLogDebug($"Applying BW threshold for RGB");
                    finalImage.BlackThreshold(new Percentage((double)HotProfile.ThresholdBlackValue));
                }
                if (alphaChannel != null && HotProfile.ThresholdAlphaEnabled)
                {
                    WriteToLogDebug($"Applying BW threshold for alpha");
                    alphaChannel.BlackThreshold(new Percentage((double)HotProfile.ThresholdBlackValue));
                }
            }

            if (HotProfile.ThresholdWhiteValue != 100)
            {
                finalImage.HasAlpha = false;
                if (HotProfile.ThresholdEnabled)
                    finalImage.WhiteThreshold(new Percentage((double)HotProfile.ThresholdWhiteValue));
                if (alphaChannel != null && HotProfile.ThresholdAlphaEnabled)
                    alphaChannel.WhiteThreshold(new Percentage((double)HotProfile.ThresholdWhiteValue));
            }
            if (alphaChannel != null)
            {
                finalImage.HasAlpha = true;
                finalImage.Composite(alphaChannel, CompositeOperator.CopyAlpha);
            }

            if (HotProfile.ResizeImageAfterScaleFactor != 1.0)
            {
                WriteToLogDebug($"Resize image x{HotProfile.ResizeImageAfterScaleFactor}");
                finalImage = ImageOperations.ResizeImage(finalImage, HotProfile.ResizeImageAfterScaleFactor, (FilterType)HotProfile.ResizeImageAfterFilterType);
            }
        }

        [Category("Exposed")]
        [ProtoMember(52)]
        public int MaxConcurrency { get; set; } = 99;

#region SPLIT    

        void CreateTiles(FileInfo file, MagickImage inputImage, bool imageHasAlpha, Profile HotProfile, MagickImage inputImageAlpha = null)
        {
            FileInfo fileAlpha = new FileInfo(file.DirectoryName + DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name) + "_alpha.png");
            string lrPathAlpha = LrPath + "_alpha";
            int imageWidth = inputImage.Width, imageHeight = inputImage.Height;
            MagickImage inputImageRed = null, inputImageGreen = null, inputImageBlue = null;

            int[] tiles;
            if (HotProfile.PreciseTileResolution)
            {
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolutionWidth, MaxTileResolutionHeight);
                if (tiles[0] == 0 || tiles[1] == 0)
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
                    if (!dimensionsAreOK && !HotProfile.SeamlessTexture)
                    {
                        inputImage = ImageOperations.PadImage(inputImage, tiles[0], tiles[1]);
                    }
                }
            }
            ImagePreprocess(ref inputImage, HotProfile);

            imageWidth = inputImage.Width;
            imageHeight = inputImage.Height;
            tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolution);

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
                    WriteToLog("Image is not RGB");
                    return;
                }
            }

            int tileWidth = imageWidth / tiles[0];
            int tileHeight = imageHeight / tiles[1];
            bool addColumn = false, addRow = false;
            int rows = tiles[1], columns = tiles[0];
            if (HotProfile.PreciseTileResolution)
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

            Directory.CreateDirectory($"{LrPath}{DirectorySeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}");
            Dictionary<string, string> lrImages = new Dictionary<string, string>();
            if (InMemoryMode)
                lrDict.Add(file.FullName, lrImages);

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

                    if (imageHasAlpha && !HotProfile.IgnoreAlpha) //crop alpha
                    {
                        MagickImage outputImageAlpha = (MagickImage)inputImageAlpha.Clone();
                        outputImageAlpha.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        string lrAlphaFolderPath = $"{lrPathAlpha}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}";
                        if (InMemoryMode)
                        {
                            if (HotProfile.UseDifferentModelForAlpha)
                            {
                                var outPath = $"{lrAlphaFolderPath}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}{file.Extension}";
                                lrImages.Add(outPath, outputImageAlpha.ToBase64());
                            }
                            else
                            {
                                var outPath = $"{LrPath}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}{file.Extension}";
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
                                outputImageAlpha.Write($"{LrPath}{DirectorySeparator}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}.png");
                        }
                    }
                    if (HotProfile.SplitRGB)
                    {
                        var pathBase = $"{LrPath}{DirectorySeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{Path.GetFileNameWithoutExtension(file.Name)}";
                        var pathR = $"{pathBase}_R_tile-{tileIndex:D2}.png";
                        var pathG = $"{pathBase}_G_tile-{tileIndex:D2}.png";
                        var pathB = $"{pathBase}_B_tile-{tileIndex:D2}.png";

                        MagickImage outputImageRed = (MagickImage)inputImageRed.Clone();
                        outputImageRed.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));     
                        MagickImage outputImageGreen = (MagickImage)inputImageGreen.Clone();
                        outputImageGreen.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        MagickImage outputImageBlue = (MagickImage)inputImageBlue.Clone();
                        outputImageBlue.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));                        

                        if (InMemoryMode)
                        {
                            lrImages.Add(pathR, outputImageRed.ToBase64());
                            lrImages.Add(pathG, outputImageGreen.ToBase64());
                            lrImages.Add(pathB, outputImageBlue.ToBase64());
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
                        outputImage.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        MagickFormat format = MagickFormat.Png24;
                        var dirpath = Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "");
                        string outPath = $"{LrPath}{dirpath}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}_tile-{tileIndex:D2}.png";
                        if (!InMemoryMode)
                            outputImage.Write(outPath, format);
                        else
                        {
                            outPath = $"{LrPath}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}_tile-{tileIndex:D2}{file.Extension}";
                            lrImages.Add(outPath, outputImage.ToBase64());
                        }
                    }
                }
            }
            inputImage.Dispose();
            WriteToLog($"{file.Name} SPLIT DONE", Color.LightGreen);
        }

        void SplitTask(FileInfo file, Profile HotProfile)
        {
            MagickImage image, inputImage, inputImageAlpha = null;
            bool imageHasAlpha = false;

            try
            {
                image = ImageOperations.LoadImage(file);
                imageHasAlpha = image.HasAlpha;
            }
            catch (Exception ex)
            {
                WriteToLog($"Failed to read file {file.Name}!", Color.Red);
                WriteToLog(ex.Message);
                return;
            }

            if (imageHasAlpha && !HotProfile.IgnoreAlpha)
                inputImageAlpha = (MagickImage)image.Separate(Channels.Alpha).First();

            if (HotProfile.SeamlessTexture)
            {
                image = ImageOperations.ExpandTiledTexture(image);
            }

            inputImage = (MagickImage)image.Clone();
            inputImage.HasAlpha = false;

            if (imageHasAlpha && !HotProfile.IgnoreAlpha)
            {
                if (HotProfile.UseFilterForAlpha)
                    imageHasAlpha = false;
                else
                {
                    bool isSolidWhite = inputImageAlpha.TotalColors == 1 && inputImageAlpha.Histogram().ContainsKey(new MagickColor("#FFFFFF"));
                    if (HotProfile.IgnoreSingleColorAlphas && isSolidWhite)
                    {
                        inputImageAlpha.Dispose();
                        imageHasAlpha = false;
                    }
                    else
                    {
                        if (HotProfile.SeamlessTexture)
                            inputImageAlpha = ImageOperations.ExpandTiledTexture(inputImageAlpha);
                    }
                }
            }
            CreateTiles(file, inputImage, imageHasAlpha, HotProfile, inputImageAlpha);
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
            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;

            DirectoryInfo inputDirectory = new DirectoryInfo(InputDirectoryPath);
            DirectoryInfo lrDirectory = new DirectoryInfo(LrPath);
            FileInfo[] inputDirectoryFiles = inputDirectory.GetFiles("*", searchOption)
               .Where(x => ImageFormatInfo.ImageExtensions.Contains(x.Extension.Remove(0, 1).ToUpperInvariant())).ToArray();
            if (inputDirectoryFiles.Count() == 0)
            {
                WriteToLog("No files in input folder!", Color.Red);
                return;
            }

            DirectoryInfo lrAlphaDirectory = new DirectoryInfo(LrPath + "_alpha");
            if(lrDirectory.Exists)
            { 
                lrDirectory.GetFiles("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
            lrDirectory.GetDirectories("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
            WriteToLog($"'{LrPath}' is cleared", Color.LightBlue);
            }
            else
                lrDirectory.Create();


            if (!lrAlphaDirectory.Exists)
                lrAlphaDirectory.Create();
            else
            {
                lrAlphaDirectory.GetFiles("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
                lrAlphaDirectory.GetDirectories("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
                WriteToLog($"'{LrPath + "_alpha"}' is cleared", Color.LightBlue);
            }         

            WriteToLog("Creating tiles...");

            if (inputFiles == null)
                inputFiles = inputDirectoryFiles;

            if (!InMemoryMode)
            {
                ResetDoneCounter();
                SetTotalCounter(inputFiles.Length);
                ReportProgress();
            }

            await Task.Run(() => Parallel.ForEach(inputFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = MaxConcurrency }, file =>
            {
                if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper().Remove(0, 1)))
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
                    WriteToLog($"{file.Name} is filtered, skipping", Color.HotPink);
                    return;
                }
            }));

            if (!InMemoryMode)
                WriteToLog("Finished!", Color.LightGreen);
        }

        public async Task Split(FileInfo file)
        {
            WriteToLog($"{file.Name} SPLIT START");

            if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper().Remove(0, 1)))
                return;
            bool fileSkipped = true;
            List<Rule> rules = new List<Rule>(Ruleset.Values);
            if (DisableRuleSystem)
                rules = new List<Rule> { new Rule("Simple rule", CurrentProfile, CurrentFilter) };

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
                WriteToLog($"{file.Name} is filtered, skipping", Color.HotPink);
                return;
            }
        }

#endregion

#region MERGE

        int[] GetTileResolution(Tuple<string, MagickImage> pathImage, string basePath, out int[] tiles, string resultSuffix, ref int tileWidth, ref int tileHeight, Profile HotProfile)
        {
            MagickImageInfo lastResultTileInfo, lastLrTileInfo;
            FileInfo file = new FileInfo(pathImage.Item1);
            MagickImage image = pathImage.Item2;

            if (image.Height * image.Width > MaxTileResolution)
            {
                tiles = Helper.GetTilesSize(image.Width, image.Height, MaxTileResolution);
                bool dimensionsAreOK = image.Width % tiles[0] == 0 && image.Height % tiles[1] == 0;
                if (!dimensionsAreOK && !HotProfile.SeamlessTexture)
                {
                    int[] newDimensions = Helper.GetGoodDimensions(image.Width, image.Height, tiles[0], tiles[1]);
                    tiles = Helper.GetTilesSize(newDimensions[0], newDimensions[1], MaxTileResolution);
                }
            }
            else
                tiles = new int[] { 1, 1 };

            int detectedSize = 1;
            try
            {
                var files = Directory.GetFiles(ResultsPath, "*", SearchOption.AllDirectories);
                Dictionary<string, MagickImage> hrTiles = null;
                if (InMemoryMode)
                {
                    hrTiles = hrDict[pathImage.Item1];
                    files = hrTiles.Select(x => x.Key).ToArray();
                }
                int lastTileIndex = -1;
                var baseName = $"{basePath}_tile";
                if(HotProfile.SplitRGB)
                    baseName = $"{basePath}_R_tile";
                foreach (var f in files.Where(x => x.ToLower().Contains(baseName.ToLower())))
                {
                    //var a = Path.GetFileNameWithoutExtension(f);
                    //var b = Path.GetFileNameWithoutExtension(file.Name);
                    //var tr = a == b + "_tile-00";
                    //if (!tr)
                    //    continue;
                    //var match = Regex.Match(Path.GetFileNameWithoutExtension(f), $"({Path.GetFileNameWithoutExtension(file.Name)}_tile-)([0-9]*)");
                    var match = Regex.Match(Path.GetFileNameWithoutExtension(f), $"(.*_tile-)([0-9]*)");
                    string t = match.Groups[2].Value;
                    if (Int32.Parse(t) > lastTileIndex)
                        lastTileIndex = Int32.Parse(t);
                }
                if(lastTileIndex == -1)
                {
                    WriteToLog($"Couldn't find last HR tile index for {baseName}", Color.Red);
                    return new int[] { 0, 0, 0 };
                }
                string pathToLastTile = $"{ResultsPath + baseName}-{lastTileIndex:D2}{resultSuffix}.png";
                string pathToLastLrTile = $"{LrPath + baseName}-{lastTileIndex:D2}{resultSuffix}.png";
                //pathImage.Item1.Replace(InputDirectoryPath, LrPath).Replace(file.Extension, "") + $"_tile-{lastTileIndex:D2}{file.Extension}";
                //$"{LrPath + basePath}_tile-{lastTileIndex.ToString("D2")}{resultSuffix}.png";

                if (HotProfile.SplitRGB && OutputDestinationMode == 1)
                {
                    baseName = Path.GetFileNameWithoutExtension(file.Name);
                    pathToLastTile = $"{ResultsPath + basePath.Replace(baseName, baseName + "_R")}_tile-{lastTileIndex:D2}{resultSuffix}.png";
                }

                WriteToLogDebug($"tile-{lastTileIndex:D2} is last for {ResultsPath + basePath}");

                int tileLrWidth, tileLrHeight;
                if (InMemoryMode)
                {
                    MagickImage lastHrTile = hrTiles.Where(x => x.Key.ToLower() == pathToLastTile.ToLower()).FirstOrDefault().Value;
                    if (lastHrTile == null)
                    {
                        WriteToLog($"Couldn't find last HR tile for {pathToLastTile}", Color.Red);
                        return new int[] { 0,0,0 };
                    }
                    tileWidth = lastHrTile.Width;
                    tileHeight = lastHrTile.Height;
                    var lrImages = lrDict[pathImage.Item1];
                    MagickImage lastLrTile = MagickImage.FromBase64(lrImages.Where(x => x.Key.ToLower() == pathToLastLrTile.ToLower()).FirstOrDefault().Value) as MagickImage;
                    if (lastLrTile == null)
                    {
                        WriteToLog($"Couldn't find last LR tile for {pathToLastLrTile}", Color.Red);
                        return new int[] { 0, 0, 0 };
                    }
                    tileLrWidth = lastLrTile.Width;
                    tileLrHeight = lastLrTile.Height;
                }
                else
                {
                    lastResultTileInfo = new MagickImageInfo(pathToLastTile);
                    tileWidth = lastResultTileInfo.Width;
                    tileHeight = lastResultTileInfo.Height;
                    pathToLastLrTile = pathToLastLrTile.Replace(new FileInfo(pathToLastLrTile).Extension, ".png");
                    if (!File.Exists(pathToLastLrTile))
                        lastLrTileInfo = new MagickImageInfo(pathToLastTile);
                    else
                        lastLrTileInfo = new MagickImageInfo(pathToLastLrTile);

                    tileLrWidth = lastLrTileInfo.Width;
                    tileLrHeight = lastLrTileInfo.Height;
                }
                if(HotProfile.SeamlessTexture)
                {
                    int expandSize = SeamlessExpandSize;
                    if (image.Height <= 32 || image.Width <= 32)
                        expandSize = 8;
                    tileLrWidth -= expandSize * 2;
                    tileLrHeight -= expandSize * 2;
                }

                int lastTileIndexExpected = (tiles[1] - 1) * tiles[0] + tiles[0];

                int tileWidthOld = image.Width / tiles[0];
                int tileHeightOld = image.Height / tiles[1];
                double diff = (double)(tileWidthOld * tileHeightOld * lastTileIndexExpected) / (tileLrWidth * tileLrHeight * (lastTileIndex + 1));
                if ((int)Math.Round(diff, 0) != 1)
                {
                    double mod = diff / 2;
                    detectedSize = (int)Math.Round(mod, 0);
                    tiles = Helper.GetTilesSize(image.Width / detectedSize, image.Height / detectedSize, MaxTileResolution);
                }
            }
            catch (Exception ex)
            {
                WriteToLogOpenError(file, ex.Message);
            }
            return new int[] { image.Width / detectedSize, image.Height / detectedSize, image.HasAlpha ? 1 : 0 };
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
                    false,
                    HotProfile.ddsGenerateMipmaps,
                    ResamplingAlgorithm.Bilinear,
                    processedSurface,
                    null);
                fileStream.Close();
            }
            else
                finalImage.Write(destinationPath, MagickFormat.Dds);
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

        Image JoinRGB(Tuple<string, MagickImage> pathImage, string basePath, string baseName, int tileIndex, string resultSuffix, List<FileInfo> tileFilesToDelete)
        {
            Image imageNextTileR, imageNextTileG, imageNextTileB;
            FileInfo tileR, tileG, tileB;
            if (OutputDestinationMode == 1)
            {
                tileR = new FileInfo($"{ResultsPath + basePath.Replace(baseName, baseName + "_R")}_tile-{tileIndex:D2}{resultSuffix}.png");
                tileG = new FileInfo($"{ResultsPath + basePath.Replace(baseName, baseName + "_G")}_tile-{tileIndex:D2}{resultSuffix}.png");
                tileB = new FileInfo($"{ResultsPath + basePath.Replace(baseName, baseName + "_B")}_tile-{tileIndex:D2}{resultSuffix}.png");
            }
            else
            {
                tileR = new FileInfo($"{ResultsPath + basePath}_R_tile-{tileIndex:D2}{resultSuffix}.png");
                tileG = new FileInfo($"{ResultsPath + basePath}_G_tile-{tileIndex:D2}{resultSuffix}.png");
                tileB = new FileInfo($"{ResultsPath + basePath}_B_tile-{tileIndex:D2}{resultSuffix}.png");
            }
            if(InMemoryMode)
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
                    WriteToLog($"{filename}: globabalance is canceled", Color.LightYellow);
                else
                    WriteToLog($"{filename}: {ex.Message}", Color.Red);
            }
        }

        void JoinTiles(ref Image imageRow, Image imageNextTile, string direction, int dx, int dy)
        {
            int mblendSize = EnableBlend ? OverlapSize : 0;
            imageRow = imageRow.Merge(imageNextTile, direction, dx, dy, mblendSize);
        }

        void JoinTilesNew(ref Image imageRow, Image imageNextTile, bool Copy, string direction, int dx, int dy)
        {    
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

            Image maskVips = CreateMask(imageRow.Width, imageRow.Height, overlap / 2, direction);

            Image expandedImage;
            if(Copy)
                expandedImage = imageRow.Bandjoin(new Image[] { maskVips }).Copy();
            else
                expandedImage = imageRow.Bandjoin(new Image[] { maskVips }).CopyMemory();

            Image result = Image.Black(resultW, resultH);
            result = result.Composite2(imageNextTile, "over", -dx, -dy);
            result = result.Composite2(expandedImage, "over", 0, 0);
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
            mask = mask.Gaussblur(4, precision: "integer");          
            return mask;
        }

        void JoinTilesNew(ref MagickImage imageRow, MagickImage imageNextTile, string direction, int dx, int dy)
        {
            MagickImage expandedImage = new MagickImage(imageRow);
            int overlap = expandedImage.Width + dx;            
            Bitmap mask;
            Rectangle brushSize, gradientRectangle;
            LinearGradientMode brushDirection;   
            Gravity tileG = Gravity.East;
            Gravity rowG = Gravity.West;
            int resultW = imageRow.Width, resultH = imageRow.Height;
            if (direction == Enums.Direction.Horizontal)
            {
                overlap = expandedImage.Width + dx;               
                brushSize = new Rectangle(-dx, -dy, overlap, imageRow.Height);
                brushDirection = LinearGradientMode.Horizontal;
                gradientRectangle = new Rectangle(-dx, -dy, overlap, imageNextTile.Height);
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
            MagickImage result = new MagickImage(MagickColor.FromRgb(0,0,0), resultW, resultH);            

            result.Composite(imageNextTile, tileG);
            result.Composite(expandedImage, rowG, CompositeOperator.Over);
            imageRow = new MagickImage(result);
        }

        Image MergeTiles(Tuple<string, MagickImage> pathImage, int[] tiles, int[] tileSize, string basePath, string basePathAlpha, string resultSuffix, List<FileInfo> tileFilesToDelete, bool imageHasAlpha, Profile HotProfile)
        {
            bool alphaReadError = false, cancelRgbGlobalbalance = false, cancelAlphaGlobalbalance = false;
            Image imageResult = null, imageAlphaResult = null;
            FileInfo file = new FileInfo(pathImage.Item1);
            Image imageAlphaRow = null;
            int tileWidth = tileSize[0], tileHeight = tileSize[1];
                    
            Dictionary<string, MagickImage> hrTiles = null;
            if (InMemoryMode)
            {
                hrTiles = hrDict[pathImage.Item1];
            }

            for (int i = 0; i < tiles[1]; i++)
            {
                Image imageRow = null;

                for (int j = 0; j < tiles[0]; j++)
                {
                    int tileIndex = i * tiles[0] + j;

                    Image imageNextTile, imageAlphaNextTile;
                    try
                    {
                        if (HotProfile.SplitRGB)
                            imageNextTile = JoinRGB(pathImage, basePath, Path.GetFileNameWithoutExtension(file.Name), tileIndex, resultSuffix, tileFilesToDelete);
                        else
                        {
                            string newTilePath = $"{ResultsPath + basePath}_tile-{tileIndex:D2}{resultSuffix}.png";
                            if (InMemoryMode)
                                imageNextTile = ImageOperations.ConvertToVips(hrTiles[newTilePath]);
                            else
                            {
                                imageNextTile = Image.NewFromFile(newTilePath, false, Enums.Access.Sequential);
                                tileFilesToDelete.Add(new FileInfo($"{ResultsPath + basePath}_tile-{tileIndex:D2}{resultSuffix}.png"));
                            }
                        }
                    }
                    catch (VipsException ex)
                    {
                        WriteToLogOpenError(file, ex.Message);
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
                                MagickImage upscaledAlpha = null;

                                int inputTileWidth = image.Width / tiles[0];
                                int upscaleMod = tileWidth / inputTileWidth;
                                if (upscaleMod != 1)
                                    upscaledAlpha = ImageOperations.ResizeImage(inputImageAlpha, upscaleMod, (FilterType)HotProfile.AlphaFilterType);
                                else
                                    upscaledAlpha = inputImageAlpha;
                                byte[] buffer = upscaledAlpha.ToByteArray(MagickFormat.Png00);
                                imageAlphaResult = Image.NewFromBuffer(buffer);
                                alphaReadError = true;
                            }
                            else
                            {
                                var newAlphaTilePath = $"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{resultSuffix}.png";
                                if (InMemoryMode)
                                    imageAlphaNextTile = ImageOperations.ConvertToVips(hrTiles[newAlphaTilePath]);
                                else
                                {
                                    imageAlphaNextTile = Image.NewFromFile(newAlphaTilePath, false, Enums.Access.Sequential);
                                    tileFilesToDelete.Add(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{resultSuffix}.png"));
                                }

                                if (j == 0)
                                {
                                    imageAlphaRow = imageAlphaNextTile;
                                }
                                else
                                {
                                    if(UseOldVipsMerge)
                                        JoinTiles(ref imageAlphaRow, imageAlphaNextTile, Enums.Direction.Horizontal, -tileWidth * j, 0);
                                    else
                                        JoinTilesNew(ref imageAlphaRow, imageAlphaNextTile, false, Enums.Direction.Horizontal, -tileWidth * j, 0);
                                    if (HotProfile.BalanceAlphas)
                                        UseGlobalbalance(ref imageAlphaRow, ref cancelAlphaGlobalbalance, $"{file.Name} alpha");
                                }
                            }
                        }
                        catch (VipsException ex)
                        {
                            alphaReadError = true;
                            if (!HotProfile.IgnoreSingleColorAlphas)
                                WriteToLogOpenError(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{resultSuffix}.png"), ex.Message);
                        }
                    }

                    if (j == 0)
                    {
                        imageRow = imageNextTile;
                        continue;
                    }
                    else
                    {
                        if(UseOldVipsMerge)
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
                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
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

                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
                    {
                        if(UseOldVipsMerge)
                            JoinTiles(ref imageAlphaResult, imageAlphaRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                        else
                            JoinTilesNew(ref imageAlphaResult, imageAlphaRow, true, Enums.Direction.Vertical, 0, -tileHeight * i);

                        if (HotProfile.BalanceAlphas)
                            UseGlobalbalance(ref imageAlphaResult, ref cancelAlphaGlobalbalance, $"{file.Name} alpha");
                    }
                }
                GC.Collect();
            }
            bool alphaIsUpscaledWithFilter = imageAlphaResult != null && imageAlphaResult.Width == imageResult.Width && imageAlphaResult.Height == imageResult.Height;
            if ((imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError) || alphaIsUpscaledWithFilter)
            {
                //WriteToLogDebug("Detected alpha upscaled with filter");
                imageResult = imageResult.Bandjoin(imageAlphaResult);
                imageResult = imageResult.Copy(interpretation: "srgb").Cast("uchar");
                imageAlphaResult.Dispose();
            }
            return imageResult;
        }

        MagickImage MergeTilesNew(Tuple<string, MagickImage> pathImage, int[] tiles, int[] tileSize, string basePath, string basePathAlpha, string resultSuffix, List<FileInfo> tileFilesToDelete, bool imageHasAlpha, Profile HotProfile)
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
                            WriteToLog("RGB split is unsupported with IM merge, sorry!");
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
                        WriteToLogOpenError(file, ex.Message);
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
                                WriteToLog($"Upscaling alpha x{upscaleMod} with {HotProfile.AlphaFilterType} filter", Color.LightBlue);
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
                                WriteToLogOpenError(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{resultSuffix}.png"), ex.Message);
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

        internal void MergeTask(Tuple<string, MagickImage> pathImage, string basePath, Profile HotProfile, string outputFilename = "")
        {
            FileInfo file = new FileInfo(pathImage.Item1);

#region IMAGE READ
            string basePathAlpha = basePath;
            string resultSuffix = "";

            if (UseResultSuffix)
                resultSuffix = ResultSuffix;

            if (OutputDestinationMode == 1) // grab alpha tiles from different folder
            {
                string fileName = Path.GetFileNameWithoutExtension(file.Name);
                basePathAlpha = basePathAlpha.Replace(
                    $"{DirectorySeparator}Images{DirectorySeparator}{fileName}",
                    $"{DirectorySeparator}Images{DirectorySeparator}{fileName}_alpha");
            }

            bool imageHasAlpha = false;
            int imageWidth = 0, imageHeight = 0;

            int[] tiles;
            int tileWidth = 0, tileHeight = 0;
            try
            {
                int[] dimesions = GetTileResolution(pathImage, basePath, out tiles, resultSuffix, ref tileWidth, ref tileHeight, HotProfile);
                imageWidth = dimesions[0];
                imageHeight = dimesions[1];
                imageHasAlpha = dimesions[2] == 1;
                if (imageWidth == 0 || imageHeight == 0)
                    return;

                WriteToLogDebug($"Image dimensions: {imageWidth}x{imageHeight}, alpha: {imageHasAlpha}");
                WriteToLogDebug($"Tiles: {tiles[0]}x{tiles[1]}, {tileWidth}x{tileHeight}");
            }
            catch
            {
                WriteToLog($"Failed to read file {file.Name}!", Color.Red);
                return;
            }

            
            MagickImage inputImage = pathImage.Item2;
            if (inputImage.HasAlpha && !HotProfile.IgnoreAlpha && HotProfile.IgnoreSingleColorAlphas)
            {
                using (MagickImage inputImageAlpha = (MagickImage)inputImage.Separate(Channels.Alpha).First())
                {
                    bool singleColor = inputImageAlpha.TotalColors == 1;
                    bool isSolidWhite = singleColor && inputImageAlpha.Histogram().ContainsKey(new MagickColor("#FFFFFF"));
                    WriteToLogDebug($"Alpha is solid white: {isSolidWhite}");
                    if (isSolidWhite)
                    {
                        inputImage.HasAlpha = false;
                        imageHasAlpha = false;
                    }
                }
            }

            int expandSize = SeamlessExpandSize;
            if (imageHeight <= 32 || imageWidth <= 32)
                expandSize = 8;

            if (HotProfile.SeamlessTexture)
            {
                WriteToLogDebug($"Seamless texture, expand size: {expandSize}");
                imageWidth += expandSize * 2;
                imageHeight += expandSize * 2;
            }

            List<FileInfo> tileFilesToDelete = new List<FileInfo>();
#endregion

            bool dimensionsAreOK = imageWidth % tiles[0] == 0 && imageHeight % tiles[1] == 0;
            if (!dimensionsAreOK && !HotProfile.SeamlessTexture)
            {
                WriteToLogDebug($"Dimensions are wrong.");
                int[] newDimensions = Helper.GetGoodDimensions(imageWidth, imageHeight, tiles[0], tiles[1]);
                WriteToLogDebug($"Good dimensions: {newDimensions[0]}x{newDimensions[1]}");
                tiles = Helper.GetTilesSize(newDimensions[0], newDimensions[1], MaxTileResolution);
                WriteToLogDebug($"New tiles: {tiles[0]}x{tiles[1]}");
            }
            MagickImage finalImage = null;
            Image imageResult = null;

            ImageFormatInfo outputFormat;
            if (HotProfile.UseOriginalImageFormat)
                outputFormat = HotProfile.FormatInfos.Where(x => x.Extension.Equals(file.Extension, StringComparison.InvariantCultureIgnoreCase)).First();
            else
                outputFormat = HotProfile.selectedOutputFormat;
            if (outputFormat == null)
                outputFormat = new ImageFormatInfo(file.Extension);

            string destinationPath = OutputDirectoryPath + basePath + outputFormat;

            if (outputFilename != "")
                destinationPath = OutputDirectoryPath + basePath.Replace(Path.GetFileNameWithoutExtension(file.Name), outputFilename) + outputFormat;

            if (OutputDestinationMode == 3)
                destinationPath = $"{OutputDirectoryPath}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}" +
                    $"{Path.GetFileNameWithoutExtension(file.Name)}{outputFormat}";
            WriteToLogDebug($"Destination path: {destinationPath}");

            WriteToLogDebug($"Using merge with gradient blend: {UseImageMagickMerge}");
            int upscaleModificator = 1;
            double upMod = 1;
            int mergedWidth = 0, mergedHeight = 0;
            if (UseImageMagickMerge)
            {
                finalImage = MergeTilesNew(pathImage, tiles, new int[] { tileWidth, tileHeight }, basePath, basePathAlpha, resultSuffix, tileFilesToDelete, imageHasAlpha, HotProfile);
                upMod = (double)finalImage.Width / imageWidth;
                mergedWidth = finalImage.Width;
                mergedHeight = finalImage.Height;
            }
            else
            {
                imageResult = MergeTiles(pathImage, tiles, new int[] { tileWidth, tileHeight }, basePath, basePathAlpha, resultSuffix, tileFilesToDelete, imageHasAlpha, HotProfile);
                upMod = (double)imageResult.Width / imageWidth;
                mergedWidth = imageResult.Width;
                mergedHeight = imageResult.Height;
            }
            upscaleModificator = (int)Math.Round(upMod);

            if (HotProfile.SeamlessTexture)
            {
                WriteToLogDebug($"Extrating seamless texture. Upscale modificator: {upscaleModificator}");
                if (UseImageMagickMerge)
                    finalImage = ExtractTiledTexture(finalImage, upscaleModificator, expandSize);
                else
                    ExtractTiledTexture(ref imageResult, upscaleModificator, expandSize);
            }
            else
            {
                if (mergedWidth % imageWidth != 0 || mergedHeight % imageHeight != 0) // result image dimensions are wrong
                {
                    WriteToLogDebug($"Final image dimensions are wrong.");
                    WriteToLogDebug($"Upscale modificator: {upscaleModificator}");
                    if (UseImageMagickMerge)
                        finalImage.Crop(imageWidth * upscaleModificator, imageHeight * upscaleModificator, Gravity.Northwest);
                    else
                        imageResult = imageResult.Crop(0, 0, imageWidth * upscaleModificator, imageHeight * upscaleModificator);
                }
            }

#region SAVE IMAGE

            if (!UseImageMagickMerge)
            {
                if (imageResult == null)
                    return;
                if (outputFormat.VipsNative &&
                    (!HotProfile.ThresholdEnabled || (HotProfile.ThresholdBlackValue == 0 && HotProfile.ThresholdWhiteValue == 100)) &&
                      HotProfile.ResizeImageAfterScaleFactor == 1.0) //no need to convert to MagickImage, save fast with vips
                {
                    WriteToLogDebug($"Saving with vips");

                    if (HotProfile.OverwriteMode == 2)
                    {
                        WriteToLogDebug($"Overwriting file");
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
                    pathImage.Item2.Dispose();
                    IncrementDoneCounter();
                    //ReportProgress();
                    WriteToLog($"<{file.Name}> MERGE DONE", Color.LightGreen);

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

            ImagePostrpocess(ref finalImage, HotProfile);

            if (HotProfile.OverwriteMode == 2)
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
                WriteToFileDds(finalImage, destinationPath, HotProfile);
            else
                finalImage.Write(destinationPath);
            imageResult?.Dispose();
            finalImage.Dispose();
            pathImage.Item2.Dispose();
            IncrementDoneCounter();
            ReportProgress();
            WriteToLog($"{file.Name} DONE", Color.LightGreen);
            if (HotProfile.DeleteResults)
                tileFilesToDelete.ForEach(x => x.Delete());
            GC.Collect();
#endregion
        }

        async public Task Merge()
        {
            if (!IsSub)
                SaveSettings();

            DirectoryInfo di = new DirectoryInfo(InputDirectoryPath);

            ResetDoneCounter();
            ResetTotalCounter();

            int tempOutMode = OutputDestinationMode;
            if (UseModelChain) tempOutMode = 0;

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (tempOutMode == 3)
                searchOption = SearchOption.AllDirectories;

            FileInfo[] inputFiles = di.GetFiles("*", searchOption)
               .Where(x => ImageFormatInfo.ImageExtensions.Contains(x.Extension.Remove(0, 1).ToUpperInvariant())).ToArray();

            WriteToLog("Counting files...");
            await GetTotalFileNumber(inputFiles);

            WriteToLog("Merging tiles...");
            await Task.Run(() => Parallel.ForEach(inputFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = MaxConcurrency }, file =>
            //foreach(var file in inputFiles)
            {
                if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper().Remove(0, 1)))
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
                    WriteToLog($"{file.Name} is filtered, skipping", Color.HotPink);
                    return;
                }

                if (tempOutMode == 0)
                {
                    MergeTask(pathImage, DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name), profile);
                    return;
                }

                if (tempOutMode == 1)
                {
                    DirectoryInfo imagesFolder;

                    if (profile.SplitRGB) //search for initial tiles in _R folder
                    {
                        imagesFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}images{DirectorySeparator}" + Path.GetFileNameWithoutExtension(file.Name) + "_R");

                        foreach (var image in imagesFolder.GetFiles("*", SearchOption.TopDirectoryOnly).
                            Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_R_tile-00")))
                        {
                            string basePath = $"{DirectorySeparator}images{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}{DirectorySeparator}" +
                                $"{Path.GetFileNameWithoutExtension(image.Name).Replace("_R", "")}";
                            basePath = basePath.Remove(basePath.Length - 8, 8);
                            MergeTask(pathImage, basePath, profile);
                        }
                    }
                    else
                    {
                        imagesFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}Images{DirectorySeparator}" + Path.GetFileNameWithoutExtension(file.Name));
                        if ((!imagesFolder.Exists || imagesFolder.GetFiles().Length == 0) && !InMemoryMode)
                        {
                            WriteToLogOpenError(file, "Can't find tiles in result folder for " + file.Name);
                            return;
                        }
                        foreach (var image in imagesFolder.GetFiles("*", SearchOption.TopDirectoryOnly).
                            Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_tile-00")))
                        {
                            string basePath = $"{DirectorySeparator}Images{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}{DirectorySeparator}{Path.GetFileNameWithoutExtension(image.Name)}";
                            basePath = basePath.Remove(basePath.Length - 8, 8); //remove "_tile-00"                                 
                            MergeTask(pathImage, basePath, profile);
                        }
                    }
                    return;
                }
                if (tempOutMode == 2)
                {
                    DirectoryInfo modelsFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}Models{DirectorySeparator}");
                    if (!modelsFolder.Exists)
                    {
                        WriteToLog(modelsFolder.FullName + " doesn't exist!", Color.Red);
                        return;
                    }

                    foreach (var modelFolder in modelsFolder.GetDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        foreach (var image in modelFolder.GetFiles("*", SearchOption.TopDirectoryOnly).Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_tile-00")))
                        {
                            string basePath = $"{DirectorySeparator}Models{DirectorySeparator}{modelFolder.Name}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}";
                            MergeTask(pathImage, basePath, profile);
                        }
                    }
                    return;
                }
                if (tempOutMode == 3)
                {
                    MergeTask(
                        pathImage,
                        file.FullName.Replace(InputDirectoryPath, "").Replace(file.Name, Path.GetFileNameWithoutExtension(file.Name)),
                        profile);
                    return;
                }
            }
            ));

            GC.Collect();
            WriteToLog("Finished!", Color.LightGreen);

            string pathToMergedFiles = OutputDirectoryPath;
            if (tempOutMode == 1)
                pathToMergedFiles += $"{DirectorySeparator}Images";
            if (tempOutMode == 2)
                pathToMergedFiles += $"{DirectorySeparator}Models";
        }

        async Task Merge(string path)
        {
            if (!IsSub)
                SaveSettings();

            DirectoryInfo di = new DirectoryInfo(InputDirectoryPath);

            //ResetDoneCounter();
            //ResetTotalCounter();     

            FileInfo file = new FileInfo(path);

            WriteToLog($"{file.Name} MERGE START");

            if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper().Remove(0, 1)))
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
                WriteToLog($"{file.Name} is filtered, skipping", Color.HotPink);
                return;
            }

            await Task.Run(() =>
            {
                if (OutputDestinationMode == 0)
                {
                    MergeTask(pathImage, DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name), profile);
                }

                if (OutputDestinationMode == 1)
                {
                    DirectoryInfo imagesFolder;

                    if (InMemoryMode)
                    {
                        //for (int i = 0; i < hrDict[file.FullName].Keys.Count; i += lrDict[file.FullName].Keys.Count)
                        //{
                        var tilePath = hrDict[file.FullName].Keys.ElementAt(0);

                        var index = tilePath.LastIndexOf(DirectorySeparator);
                        var indexPrev = tilePath.LastIndexOf(DirectorySeparator, index - 1);
                        var modelName = tilePath.Substring(index + 1);

                        string basePath = $"{DirectorySeparator}Images{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}{DirectorySeparator}{Path.GetFileNameWithoutExtension(modelName)}";
                        basePath = basePath.Remove(basePath.Length - 8, 8); //remove "_tile-00"  
                        MergeTask(pathImage, basePath, profile);
                        //}
                    }
                    else
                    {
                        if (profile.SplitRGB) //search for initial tiles in _R folder
                        {
                            imagesFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}Images{DirectorySeparator}" + Path.GetFileNameWithoutExtension(file.Name) + "_R");

                            foreach (var image in imagesFolder.GetFiles("*", SearchOption.TopDirectoryOnly).
                                Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_R_tile-00")))
                            {
                                string basePath = $"{DirectorySeparator}Images{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}{DirectorySeparator}" +
                                    $"{Path.GetFileNameWithoutExtension(image.Name).Replace("_R", "")}";
                                basePath = basePath.Remove(basePath.Length - 8, 8);
                                MergeTask(pathImage, basePath, profile);
                            }
                        }
                        else
                        {
                            imagesFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}Images{DirectorySeparator}" + Path.GetFileNameWithoutExtension(file.Name));
                            FileInfo[] imageFiles = null;
                            if (InMemoryMode)
                            {
                                var name = ResultsPath + $"{DirectorySeparator}Images{DirectorySeparator}" + Path.GetFileNameWithoutExtension(file.Name);
                                imageFiles = hrDict[file.FullName].Keys.Where(x => x.Contains(name)).Select(x => new FileInfo(x)).ToArray();
                            }
                            else
                            {
                                if (!imagesFolder.Exists || imagesFolder.GetFiles().Length == 0)
                                {
                                    WriteToLogOpenError(file, "Can't find tiles in result folder for " + file.Name);
                                    return;
                                }
                                imageFiles = imagesFolder.GetFiles("*", SearchOption.TopDirectoryOnly).
                                    Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_tile-00")).ToArray();
                            }

                            foreach (var image in imageFiles)
                            {
                                string basePath = $"{DirectorySeparator}Images{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}{DirectorySeparator}{Path.GetFileNameWithoutExtension(image.Name)}";
                                basePath = basePath.Remove(basePath.Length - 8, 8); //remove "_tile-00"                                 
                                MergeTask(pathImage, basePath, profile);
                            }
                        }
                    }
                }
                if (OutputDestinationMode == 2)
                {
                    DirectoryInfo modelsFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}Models{DirectorySeparator}");

                    if (InMemoryMode)
                    {
                        //for (int i = 0; i < hrDict[file.FullName].Keys.Count; i += lrDict[file.FullName].Keys.Count)
                        //{
                        var tilePath = hrDict[file.FullName].Keys.ElementAt(0);

                        var index = tilePath.LastIndexOf(DirectorySeparator);
                        var indexPrev = tilePath.LastIndexOf(DirectorySeparator, index - 1);
                        var modelName = tilePath.Substring(indexPrev + 1, index - indexPrev - 1);

                        string basePath = $"{DirectorySeparator}Models{DirectorySeparator}{modelName}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}";
                        MergeTask(pathImage, basePath, profile);
                        //}

                    }
                    else
                    {
                        modelsFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}Models{DirectorySeparator}");
                        if (!modelsFolder.Exists)
                        {
                            WriteToLog(modelsFolder.FullName + " doesn't exist!", Color.Red);
                            return;
                        }
                        foreach (var modelFolder in modelsFolder.GetDirectories("*", SearchOption.TopDirectoryOnly))
                        {
                            foreach (var image in modelFolder.GetFiles("*", SearchOption.TopDirectoryOnly).Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_tile-00")))
                            {
                                string basePath = $"{DirectorySeparator}Models{DirectorySeparator}{modelFolder.Name}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}";
                                MergeTask(pathImage, basePath, profile);
                            }
                        }
                    }
                }
                if (OutputDestinationMode == 3)
                {
                    MergeTask(
                        pathImage,
                        file.FullName.Replace(InputDirectoryPath, "").Replace(file.Name, Path.GetFileNameWithoutExtension(file.Name)),
                        profile);
                }
            });

            if (InMemoryMode)
            {                
                lrDict.Remove(path);
                hrDict.Remove(path);
            }
            GC.Collect();

            string pathToMergedFiles = OutputDirectoryPath;
            if (OutputDestinationMode == 1)
                pathToMergedFiles += $"{DirectorySeparator}images";
            if (OutputDestinationMode == 2)
                pathToMergedFiles += $"{DirectorySeparator}models";
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

        async Task GetTotalFileNumber(FileInfo[] inputFiles)
        {
            if (OutputDestinationMode == 0 || OutputDestinationMode == 3)
            {
                SetTotalCounter(inputFiles.Length);
                return;
            }

            await Task.Run(() => Parallel.ForEach(inputFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = MaxConcurrency }, file =>
            {
                if (OutputDestinationMode == 1)
                {
                    DirectoryInfo imagesFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}images{DirectorySeparator}" + Path.GetFileNameWithoutExtension(file.Name));
                    if (!imagesFolder.Exists || imagesFolder.GetFiles().Length == 0)
                    {
                        return;
                    }

                    foreach (var image in imagesFolder.GetFiles("*", SearchOption.TopDirectoryOnly).Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_tile-00")))
                    {
                        IncrementTotalCounter();
                    }
                }

                if (OutputDestinationMode == 2)
                {
                    DirectoryInfo modelsFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}models{DirectorySeparator}");
                    if (!modelsFolder.Exists)
                        return;

                    foreach (var modelFolder in modelsFolder.GetDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        foreach (var image in modelFolder.GetFiles("*", SearchOption.TopDirectoryOnly).Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_tile-00")))
                        {
                            IncrementTotalCounter();
                            break;
                        }
                    }
                }
            }));
        }

        #endregion

#region UPSCALE               

        CancellationTokenSource MonitorVramTokenSource;

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
                WriteToLog("No models selected!");
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
                        WriteToLog($"Can't use {model.Name} after another {latestSize}x model.");
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
                image.WriteToFile($"{LrPath}{DirectorySeparator}([000])000)_memory_helper_(ieu_is_the_best)_tile-00.png");
            }

            Process process;
            if (UseBasicSR)
                process = await BasicSR_Test(NoWindow, HotProfile);
            else
                process = await ESRGAN(NoWindow, HotProfile);
                       
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.EnableRaisingEvents = true;

            MonitorVramStart();

            if (async)
            {
                int processExitCode = await RunProcessAsync(process);

                MonitorVramTokenSource?.Cancel();

                if (processExitCode == -666)
                    return false;
                if (processExitCode != 0)
                {
                    WriteToLog("Error ocured during ESRGAN work!", Color.Red);
                    return false;
                }
                WriteToLog("ESRGAN finished!", Color.LightGreen);
            }
            else
            {
                RunProcessAsyncInMemory(process);
                WriteToLog("ESRGAN start running in background!", Color.LightGreen);
            }
            if (GetCondaEnv() != "")
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

            string block = EmbeddedResource.GetFileText($"ImageEnhancingUtility.Core.Scripts.{archName}.block.py");
            string architecture = EmbeddedResource.GetFileText($"ImageEnhancingUtility.Core.Scripts.{archName}.architecture.py");
            string script = EmbeddedResource.GetFileText($"ImageEnhancingUtility.Core.Scripts.{archName}.upscale.py");
            if (GreyscaleModel)
                script = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.Scripts.ESRGAN.upscaleGrayscale.py");
            if (InMemoryMode)
                script = EmbeddedResource.GetFileText("ImageEnhancingUtility.Core.Scripts.ESRGAN.upscaleFromMemory.py");

            string scriptPath = EsrganPath + $"{DirectorySeparator}IEU_test.py";
            if (UseBasicSR) scriptPath = EsrganPath + $"{DirectorySeparator}codes{DirectorySeparator}IEU_test.py";
            else
            {
                File.WriteAllText(EsrganPath + $"{DirectorySeparator}block.py", block);
                File.WriteAllText(EsrganPath + $"{DirectorySeparator}architecture.py", architecture);
            }

            File.WriteAllText(scriptPath, script);
        }

        string GetCondaEnv()
        {
            if (UseCondaEnv && CondaEnv != "")
                return $" & conda activate {CondaEnv}";
            else
                return "";
        }

#region PYTHON PROCESS STUFF

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
            }
            else if (regResultAlt.Success && regResultAlt.Groups.Count == 1)
            {
                upscaleMultiplayer = int.Parse(regResultAlt.Value.Replace("x", ""));
                var newName = checkedModel.Name.Replace(regResultAlt.Value, $"{upscaleMultiplayer}x_");
                var newFullname = checkedModel.FullName.Replace(checkedModel.Name, newName);
                File.Move(checkedModel.FullName, newFullname);
                checkedModel.FullName = newFullname;
                checkedModel.Name = newName;
                WriteToLog($"Changed model filename to {checkedModel.Name}", Color.LightBlue);
            }
            else
            {
                int processExitCodePthReader = -666;
                WriteToLog($"Detecting {checkedModel.Name} upscale size...");

                using (Process pthReaderProcess = PthReader(checkedModel.FullName))
                    processExitCodePthReader = await RunProcessAsync(pthReaderProcess);

                if (processExitCodePthReader != 0)
                {
                    WriteToLog($"Failed to detect {checkedModel.Name} upscale size!", Color.Red);
                    return upscaleMultiplayer;
                }
                WriteToLog($"{checkedModel.Name} upscale size is {hotModelUpscaleSize}", Color.LightGreen);
                checkedModel.UpscaleFactor = hotModelUpscaleSize;
                Helper.RenameModelFile(checkedModel, checkedModel.UpscaleFactor);
                WriteToLog($"Changed model filename to {checkedModel.Name}", Color.LightBlue);
                CreateModelTree();
            }
            return upscaleMultiplayer;
        }

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

        async Task<Process> ESRGAN(bool NoWindow, Profile HotProfile)
        {
            if (checkedModels.Count > 1 && HotProfile.UseDifferentModelForAlpha)
            {
                WriteToLog("Only single model must be selected when using different model for alpha");
                return null;
            }

            Process process = new Process();

            process.StartInfo.Arguments = $"{EsrganPath}";
            process.StartInfo.Arguments += GetCondaEnv();
            bool noValidModel = true;
            string torchDevice = UseCPU ? "cpu" : "cuda";
            int upscaleMultiplayer = 0;
            string resultsPath = ResultsPath;

            int tempOutMode = OutputDestinationMode;            

            if (HotProfile.OverwriteMode == 1)
                resultsPath = LrPath;

            int modelIndex = 0;
            if(InMemoryMode)
            {
                noValidModel = false;                
                process.StartInfo.Arguments +=
                   $" & python IEU_test.py \"blank\" 1 {torchDevice}" +
                   $" \"{LrPath + $"{DirectorySeparator}*"}\" \"{resultsPath}\" {tempOutMode} {InMemoryMode}";
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
                    $" \"{LrPath + $"{DirectorySeparator}*"}\" \"{resultsPath}\" {tempOutMode} {InMemoryMode}";

                    modelIndex++;
                }

            if (HotProfile.UseDifferentModelForAlpha)
            {   //detect upsacle factor for alpha model
                bool validModelAlpha = false;
                int upscaleMultiplayerAlpha = 0;

                if ((upscaleMultiplayerAlpha = await DetectModelUpscaleFactor(HotProfile.ModelForAlpha)) == 0)
                {
                    validModelAlpha = true;
                }

                if (upscaleMultiplayer != upscaleMultiplayerAlpha)
                {
                    WriteToLog("Upscale size for rgb model and alpha model must be the same");
                    return null;
                }
                if (validModelAlpha)
                    process.StartInfo.Arguments +=
                        $" & python IEU_test.py \"{HotProfile.ModelForAlpha.FullName}\" {upscaleMultiplayerAlpha} {torchDevice}" +
                        $" \"{LrPath + $"_alpha{DirectorySeparator}*"}\" \"{resultsPath}\" {OutputDestinationMode}";
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

            WriteToLog("Starting ESRGAN...");
            return process;
        }

        async Task<Process> BasicSR_Test(bool NoWindow, Profile HotProfile)
        {
            if (checkedModels.Count > 1 && HotProfile.UseDifferentModelForAlpha)
            {
                WriteToLog("Only single model must be selected when using different model for alpha");
                return null;
            }

            Process process = new Process();     

            process.StartInfo.Arguments = $"{EsrganPath}";
            process.StartInfo.Arguments += GetCondaEnv();

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
            for (int i = 0; i < configs.Count; i++)
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
            SetTotalCounter(Directory.GetFiles(LrPath, "*", searchOption).Count() * checkedModels.Count);
            if (HotProfile.UseDifferentModelForAlpha)
                SetTotalCounter(FilesTotal + Directory.GetFiles(LrPath + "_alpha").Count());

            ResetDoneCounter();

            WriteTestScriptToDisk();

            WriteToLog("Starting BasicSR...");
            return process;
        }

        Process PthReader(string modelPath)
        {
            Process process = new Process();
            process.StartInfo.Arguments = $"{Helper.GetApplicationRoot()}";
            process.StartInfo.Arguments += GetCondaEnv();
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
            if(InMemoryMode)
            {
                writer = process.StandardInput;
                WriteModelsToStream();
                if (IsSub)
                    WriteImageToStream(lrDict[lrDict.Keys.FirstOrDefault()]);
            }            
            return tcs.Task;
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
                WriteToLog(process.StartInfo.Arguments);
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

        public async Task<bool> CreateInterpolatedModel(string a, string b, double alpha, string outputName = "")
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
                process.StartInfo.Arguments += GetCondaEnv();
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
                    if (origPath.Contains("([000])000)_memory_helper_(ieu_is_the_best)"))
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
                        WriteToLog($"Key for {origPath} is missing from LR dictionary!", Color.Red);
                        return;
                    }
                    var lrTiles = lrDict[origPath];
                    WriteToLog(path, Color.LightGreen);
                                        
                    if (hrTiles.Count == lrTiles.Count) //all tiles for current image
                    {
                        if (!IsSub)
                        {                            
                            await Merge(origPath);
                        }

                        if (!compDict.ContainsKey(modelName))
                            compDict.Add(modelName, new List<string>());
                        compDict[modelName].Add(origPath);
                        
                        if (compDict[modelName].Count == fileQueuCount) //all images
                        {
                            writer.WriteLine("end"); //go to next model  

                            if( compDict.Keys.Count == checkedModels.Count &&
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
                                MonitorVramTokenSource?.Cancel();
                                return;
                            }

                            SetPipeline();

                            SearchOption searchOption = SearchOption.TopDirectoryOnly;
                            if (OutputDestinationMode == 3)
                                searchOption = SearchOption.AllDirectories;
                            DirectoryInfo inputDirectory = new DirectoryInfo(InputDirectoryPath);
                            FileInfo[] inputDirectoryFiles = inputDirectory.GetFiles("*", searchOption)
                                .Where(x => ImageFormatInfo.ImageExtensions.Contains(x.Extension.Remove(0, 1).ToUpperInvariant())).ToArray();

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

        #endregion

        [Category("Exposed")]
        [ProtoMember(48)]
        public int magicNumberFor4x { get; set; } = 105;
        [Category("Exposed")]
        [ProtoMember(49)]
        public int magicNumberFor1x { get; set; } = 200;

#region GPU

        uint vmemory, vcurMemory;
        PhysicalGPU gpu;

        public async Task AutoSetTileSize()
        {
            int modelScale = 1;
            checkedModels = SelectedModelsItems;
            if (checkedModels.Count == 0)
            {
                WriteToLog("No models checked, assuming model size 4x for auto tile size.");
                modelScale = 4;
            }
            else
            {
                int upscaleMultiplayer = 0;
                foreach (ModelInfo checkedModel in checkedModels)
                {
                    string upscaleSizePattern = "(?:_?[1|2|4|8|16]x_)|(?:_x[1|2|4|8|16]_?)|(?:_[1|2|4|8|16]x_?)|(?:_?x[1|2|4|8|16]_)";
                    var regResult = Regex.Match(checkedModel.Name.ToLower(), upscaleSizePattern);
                    if (regResult.Success && regResult.Groups.Count == 1)
                    {
                        upscaleMultiplayer = int.Parse(regResult.Value.Replace("x", "").Replace("_", ""));
                    }
                    else
                    {
                        if ((upscaleMultiplayer = await DetectModelUpscaleFactor(checkedModel)) == 0)
                            continue;                        
                    }
                    if (modelScale < upscaleMultiplayer) modelScale = upscaleMultiplayer;
                }
            }

            vcurMemory = (gpu.MemoryInformation.CurrentAvailableDedicatedVideoMemoryInkB / 1000);
            WriteToLog($"Currently available VRAM: {vcurMemory} MB");
            int magicNumber = magicNumberFor4x;
            if(modelScale == 1)
                magicNumber = magicNumberFor1x;
            var newmax = (int) vcurMemory * magicNumber;

            MaxTileResolutionWidth = MaxTileResolutionHeight= (int)Math.Sqrt(newmax);    

            MaxTileResolution = newmax;
            WriteToLog($"Setting max tile size to {MaxTileResolutionWidth}x{MaxTileResolutionHeight}");
        } 

        void GetVRAM()
        {
            NVIDIA.Initialize();
            var a = PhysicalGPU.GetPhysicalGPUs();
            if (a.Length == 0) return;
            gpu = a[0];
            vmemory = (gpu.MemoryInformation.AvailableDedicatedVideoMemoryInkB / 1000);
            vcurMemory = (gpu.MemoryInformation.CurrentAvailableDedicatedVideoMemoryInkB / 1000);
            WriteToLog($"{gpu.FullName}: {vmemory} MB");
            WriteToLog($"Currently available VRAM: {vcurMemory} MB");
        }

        void MonitorVramStart()
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
                        WriteToLog($"Using {usage} MB");
                        Thread.Sleep(VramMonitorFrequency);
                    }
                }, MonitorVramTokenSource.Token);
            }
        }

#endregion
       
        public void GetCheckedModels()
        {
            checkedModels = SelectedModelsItems;
            for(int i = 0; i < checkedModels.Count; i++)
            {
                checkedModels[i].Priority = i;
            }
            if (checkedModels.Count == 0)            
                WriteToLog("No models selected!");    
        }

        async public Task SplitUpscaleMerge()
        {
            if (CurrentProfile.UseModel == true)
                checkedModels = new List<ModelInfo>() { CurrentProfile.Model };
            else
                checkedModels = SelectedModelsItems;

            if (checkedModels.Count == 0)
            {
                WriteToLog("No models selected!");
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

        [Category("Exposed")]
        [ProtoMember(51)]
        public int InMemoryMaxSplit { get; set; } = 2;
        Queue<FileInfo> fileQueue;

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

        int fileQueuCount = 0;

        async public Task SplitUpscaleMergeInMemory()
        {
            SetPipeline();

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;
            DirectoryInfo inputDirectory = new DirectoryInfo(InputDirectoryPath);
            FileInfo[] inputDirectoryFiles = inputDirectory.GetFiles("*", searchOption)
                .Where( x => ImageFormatInfo.ImageExtensions.Contains(x.Extension.Remove(0,1).ToUpperInvariant())).ToArray();

            if (inputDirectoryFiles.Count() == 0)
            {
                WriteToLog("No input images.");
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

        Queue<FileInfo> CreateQueue(FileInfo[] files)
        {
            Queue<FileInfo> fileQueue = new Queue<FileInfo>();
            if (CreateMemoryImage)
            {
                var path = $"{InputDirectoryPath}{DirectorySeparator}([000])000)_memory_helper_(ieu_is_the_best).png";
                Image image = Image.Black(MaxTileResolutionWidth, MaxTileResolutionHeight);                
                image.WriteToFile(path);
                fileQueue.Enqueue(new FileInfo(path));
            }           
            foreach (var file in files)
                fileQueue.Enqueue(file);
            return fileQueue;
        }
        
#region IMAGE INTERPOLATION

        public async void InterpolateFolders(string originalPath, string resultsAPath, string resultsBPath, string destinationPath, double alpha, Profile HotProfile = null)
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
                WriteToLog("No files in input folder!", Color.Red);
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
                    WriteToLog($"Results missing for {file.FullName}, skipping", Color.Red);
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
            if(result != null)
            {
                ImageFormatInfo outputFormat = CurrentProfile.selectedOutputFormat;                

                if (outputFormat.Extension == ".dds")
                    WriteToFileDds(result, destinationFilePath, CurrentProfile);
                else
                    result.Write(destinationFilePath);

                WriteToLog($"{Path.GetFileName(destinationFilePath)}", Color.LightGreen);
                IncrementDoneCounter();                
            }
            else
            {
                WriteToLog($"{Path.GetFileName(destinationFilePath)}: failed to interpolate", Color.Red);
                IncrementDoneCounter(false);               
            }

            ReportProgress();
        }

        public bool InterpolateImages(System.Drawing.Image imageA, System.Drawing.Image imageB, string destinationPath, double alpha)
        {
            var result = ImageInterpolation.Interpolate(imageA, imageB, destinationPath, alpha);
            if (result.Item1)            
                WriteToLog($"{Path.GetFileName(destinationPath)}", Color.LightGreen);
            else
            {
                WriteToLog($"{Path.GetFileName(destinationPath)}: failed to interpolate.\n{result.Item2}", Color.Red);
                return false;
            }
            return true;
        }

#endregion

#region PREVIEW

        private IEU previewIEU;

        public string PreviewDirPath = "";

        void SetPreviewIEU(ref IEU previewIEU)
        {            
            string previewResultsDirPath = PreviewDirPath + $"{DirectorySeparator}results";
            string previewLrDirPath = PreviewDirPath + $"{DirectorySeparator}LR";
            string previewInputDirPath = PreviewDirPath + $"{DirectorySeparator}input";

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
            previewIEU.CurrentProfile.OverwriteMode = 0;
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
        }

        async public Task<bool> Preview(string imagePath, System.Drawing.Image image, string modelPath, bool saveAsPng = false, bool copyToOriginal = false)
        {           
            if (!InMemoryMode)
                return await PreviewNormal(imagePath, image, modelPath, saveAsPng, copyToOriginal);
            else
                return await PreviewInMemory(imagePath, image, modelPath, saveAsPng, copyToOriginal);
        }

        async public Task<bool> PreviewNormal(string imagePath, System.Drawing.Image image, string modelPath, bool saveAsPng = false, bool copyToOriginal = false)
        {
            PreviewDirPath = $"{EsrganPath}{DirectorySeparator}IEU_preview";
            string previewResultsDirPath = PreviewDirPath + $"{DirectorySeparator}results";
            string previewLrDirPath = PreviewDirPath + $"{DirectorySeparator}LR";
            string previewInputDirPath = PreviewDirPath + $"{DirectorySeparator}input";          

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

            FileInfo previewOriginal = new FileInfo(previewInputDirPath + $"{DirectorySeparator}preview.png");
            FileInfo preview = new FileInfo(PreviewDirPath + $"{DirectorySeparator}preview.png");

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

            await previewIEU.Split(new FileInfo[] { previewOriginal });
            ModelInfo previewModelInfo = new ModelInfo(Path.GetFileNameWithoutExtension(modelPath), modelPath);
            previewIEU.SelectedModelsItems = new List<ModelInfo>() { previewModelInfo };
            
            bool success = await previewIEU.Upscale(true);       

            if (!success)
            {
                File.WriteAllText(PreviewDirPath + $"{DirectorySeparator}log.txt", previewIEU.Logs);
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
                preview = new FileInfo(PreviewDirPath + $"{DirectorySeparator}preview{outputFormat.Extension}");
            }
            if (!File.Exists(preview.FullName))
                return false;

            if (copyToOriginal)
            {                
                string modelName = Path.GetFileNameWithoutExtension(modelPath);
                string dir = Path.GetDirectoryName(imagePath);
                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                string destination = $"{ dir }{DirectorySeparator}{ fileName}_{modelName}{outputFormat.Extension}";
                File.Copy(preview.FullName, destination, true);
            }
            return true;
        }

        async public Task<bool> PreviewInMemory(string imagePath, System.Drawing.Image image, string modelPath, bool saveAsPng = false, bool copyToOriginal = false)
        {
            PreviewDirPath = $"{EsrganPath}{DirectorySeparator}IEU_preview";
            string previewResultsDirPath = PreviewDirPath + $"{DirectorySeparator}results";
            string previewLrDirPath = PreviewDirPath + $"{DirectorySeparator}LR";
            string previewInputDirPath = PreviewDirPath + $"{DirectorySeparator}input";

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

            FileInfo previewOriginal = new FileInfo(previewInputDirPath + $"{DirectorySeparator}preview.png");
            FileInfo preview = new FileInfo(PreviewDirPath + $"{DirectorySeparator}preview.png");

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

            await previewIEU.Split(previewOriginal);
            ModelInfo previewModelInfo = new ModelInfo(Path.GetFileNameWithoutExtension(modelPath), modelPath);
            previewIEU.SelectedModelsItems = new List<ModelInfo>() { previewModelInfo };
            SetPipeline();
            bool success = await previewIEU.Upscale(true);
            if (!success)            
                File.WriteAllText(PreviewDirPath + $"{DirectorySeparator}log.txt", previewIEU.Logs);

            CreateModelTree();
            if (!saveAsPng)
            {
                previewIEU.CurrentProfile.UseOriginalImageFormat = CurrentProfile.UseOriginalImageFormat;
                previewIEU.CurrentProfile.selectedOutputFormat = CurrentProfile.selectedOutputFormat;
            }
            await previewIEU.Merge(previewOriginal.FullName);

            ImageFormatInfo outputFormat = CurrentProfile.FormatInfos.Where(x => x.Extension.Equals(".png", StringComparison.InvariantCultureIgnoreCase)).First();
            if (!saveAsPng)
            {
                if (CurrentProfile.UseOriginalImageFormat)
                    outputFormat = CurrentProfile.FormatInfos.Where(x => x.Extension.Equals(Path.GetExtension(imagePath), StringComparison.InvariantCultureIgnoreCase)).First();
                else
                    outputFormat = CurrentProfile.selectedOutputFormat;
                preview = new FileInfo(PreviewDirPath + $"{DirectorySeparator}preview{outputFormat.Extension}");
            }
            if (!File.Exists(preview.FullName))
                return false;

            if (copyToOriginal)
            {
                string modelName = Path.GetFileNameWithoutExtension(modelPath);
                string dir = Path.GetDirectoryName(imagePath);
                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                string destination = $"{ dir }{DirectorySeparator}{ fileName}_{modelName}{outputFormat.Extension}";
                File.Copy(preview.FullName, destination);
            }
            return true;
        }

#endregion

    }
}



