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
using ImageEnhancingUtility.Core.Utility;
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
//identical filenames with different extension
[assembly: InternalsVisibleTo("ImageEnhancingUtility.Tests")]
namespace ImageEnhancingUtility.Core
{
    [ProtoContract]    
    public class IEU : ReactiveObject
    {
        public readonly string AppVersion = "0.11.04";
        public readonly string GitHubRepoName = "IEU.Core";

        #region FIELDS/PROPERTIES              

        int SeamlessExpandSize = 16;

        bool IsSub = false;

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

        public double WindowMinWidth = 800, WindowMinHeight = 650;
       
        private double _windowWidth = 1000;
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
        private double _logPanelWidth = 400;
        [ProtoMember(3)]
        public double LogPanelWidth
        {
            get => _logPanelWidth;
            set => this.RaiseAndSetIfChanged(ref _logPanelWidth, value);
        }                    

        #region FOLDER_PATHS
        private string _esrganPath;
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
        private string _modelsPath;
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
        private string _imgPath;
        [ProtoMember(6)]
        public string InputDirectoryPath
        {
            get => _imgPath;
            set => this.RaiseAndSetIfChanged(ref _imgPath, value);
        }
        private string _resultsMergedPath;
        [ProtoMember(7)]
        public string OutputDirectoryPath
        {
            get => _resultsMergedPath;
            set => this.RaiseAndSetIfChanged(ref _resultsMergedPath, value);
        }
        private string _lrPath;
        [ProtoMember(8)]
        public string LrPath
        {
            get => _lrPath;
            set => this.RaiseAndSetIfChanged(ref _lrPath, value);
        }
        private string _resultsPath;
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
        [ProtoMember(12)]
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
        [ProtoMember(14)]
        public bool CreateMemoryImage
        {
            get => _createMemoryImage;
            set => this.RaiseAndSetIfChanged(ref _createMemoryImage, value);
        }

        [ProtoMember(15, IsRequired = true)]
        public bool CheckForUpdates = true;

        bool _windowOnTop = true;
        [ProtoMember(24, IsRequired = true)]
        public bool WindowOnTop
        {
            get => _windowOnTop;
            set => this.RaiseAndSetIfChanged(ref _windowOnTop, value);
        }

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

        public static List<double> ResizeImageScaleFactors = new List<double>() { 0.25, 0.5, 1.0, 2.0, 4.0 };

        [ProtoMember(16)]
        public bool UseCPU = false;

        [ProtoMember(17)]
        public bool UseBasicSR = false;

        string _lastModelForAlphaPath;
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

        readonly string DirectorySeparator = Path.DirectorySeparatorChar.ToString(); //@"\"; 

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
        List<Profile> _profiles = new List<Profile>();
        public SourceList<Profile> Profiles = new SourceList<Profile>();

        [ProtoMember(21)]
        List<Filter> _filters = new List<Filter>();
        public SourceList<Filter> Filters = new SourceList<Filter>();

        [ProtoMember(22)]
        public SortedDictionary<int, Rule> Ruleset = new SortedDictionary<int, Rule>(new RulePriority());
        public Rule GlobalRule;

        public string SaveProfileName = "NewProfile";
        Profile _selectedProfile;
        public Profile SelectedProfile
        {
            get => _selectedProfile;
            set => _selectedProfile = value;
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
            
            Task<bool> upscaleFunc(Tuple<bool, Profile> x) => Upscale(x==null?false:x.Item1, x?.Item2);
            UpscaleCommand = ReactiveCommand.CreateFromTask((Func<Tuple<bool, Profile>, Task<bool>>)upscaleFunc);
                        
            MergeCommand = ReactiveCommand.CreateFromTask(Merge);
            
            SplitUpscaleMergeCommand = ReactiveCommand.CreateFromTask(SplitUpscaleMerge);           

            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //    DirectorySeparator = @"\";
            //else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            //    DirectorySeparator = @"/";
            //else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            //    DirectorySeparator = @":";

            WriteToLog(RuntimeInformation.OSDescription);
            WriteToLog(RuntimeInformation.FrameworkDescription);
            //WriteToLogsThreadSafe("Linux: " + RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
            //WriteToLogsThreadSafe("Windows: " + RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            //WriteToLogsThreadSafe("OSx: " + RuntimeInformation.IsOSPlatform(OSPlatform.OSX));               

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

        void CreateModelTree()
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

        private void AddFilter(Filter newProfile)
        {
            Filters.Add(newProfile);
            _filters.Add(newProfile);
        }

        public void LoadFilter(Filter profile)
        {
            CurrentFilter = profile.Clone() as Filter;
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
            KeyValuePair<int, Rule> oldProfile = (KeyValuePair<int, Rule>) Ruleset.Where(x => x.Value.Name == name).FirstOrDefault(); 
            Ruleset.Remove(Ruleset.IndexOf(oldProfile));
            Ruleset.Add(Ruleset.Count, new Rule(name, profile, filter) { Priority = Ruleset.Count });
            //Ruleset = new SortedDictionary<int, Rule>(Ruleset);
        }
        
        public void LoadRule(Rule profile)
        {
            
        }

        public void DeleteRule(Rule profile)
        {
            if (Ruleset.Values.Contains(profile))
            {
                KeyValuePair<int, Rule> oldProfile = (KeyValuePair<int, Rule>)Ruleset.Where(x => x.Value == profile).FirstOrDefault();
                Ruleset.Remove(Ruleset.IndexOf(oldProfile));
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
            ProgressBarValue = ((double)FilesDone / (double)FilesTotal) * 100.00;            
            ProgressLabel = $@"{FilesDone}/{FilesTotal}";
        }
        #endregion
        
        void ImagePreprocess(ref MagickImage image, Profile HotProfile)
        {
            if (HotProfile.ResizeImageBeforeScaleFactor != 1.0)
            {
                image = ImageOperations.ResizeImage(image, HotProfile.ResizeImageBeforeScaleFactor, (FilterType)HotProfile.ResizeImageBeforeFilterType);
                //imageWidth = Convert.ToInt32(image.Width * resizeImageBeforeScaleFactor);
                //imageHeight = Convert.ToInt32(image.Height * resizeImageBeforeScaleFactor);
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
            if (HotProfile.ThresholdEnabled)
            {
                MagickImage alphaChannel = null;
                if (!HotProfile.IgnoreAlpha && finalImage.HasAlpha)
                {
                    alphaChannel = finalImage.Separate(Channels.Alpha).First() as MagickImage;
                }

                if (HotProfile.ThresholdBlackValue != 0)
                {
                    finalImage.HasAlpha = false;
                    finalImage.BlackThreshold(new Percentage((double)HotProfile.ThresholdBlackValue));
                    if (alphaChannel != null)
                    {                        
                        alphaChannel.BlackThreshold(new Percentage((double)HotProfile.ThresholdBlackValue));
                        finalImage.HasAlpha = true;
                        finalImage.Composite(alphaChannel, CompositeOperator.CopyAlpha);
                    }                    
                }

                if (HotProfile.ThresholdWhiteValue != 100)
                {
                    finalImage.HasAlpha = false;
                    finalImage.WhiteThreshold(new Percentage((double)HotProfile.ThresholdWhiteValue));
                    if (alphaChannel != null)
                    {
                        alphaChannel.WhiteThreshold(new Percentage((double)HotProfile.ThresholdWhiteValue));
                        finalImage.HasAlpha = true;
                        finalImage.Composite(alphaChannel, CompositeOperator.CopyAlpha);
                    }                   
                }
            }

            if (HotProfile.ResizeImageAfterScaleFactor != 1.0)
            {
                finalImage = ImageOperations.ResizeImage(finalImage, HotProfile.ResizeImageAfterScaleFactor, (FilterType)HotProfile.ResizeImageAfterFilterType);
            }
        }
       
        int maxConcurrency = 99;

        #region SPLIT    
       
        void CreateTiles(FileInfo file, MagickImage inputImage, bool imageHasAlpha, Profile HotProfile, MagickImage inputImageAlpha = null)
        {
            FileInfo fileAlpha = new FileInfo(file.DirectoryName + DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name) + "_alpha.png");
            string lrPathAlpha = LrPath + "_alpha";
            int imageWidth = inputImage.Width, imageHeight = inputImage.Height;
            MagickImage inputImageRed = null, inputImageGreen = null, inputImageBlue = null;

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

            int[] tiles;
            if (HotProfile.PreciseTileResolution)
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
                    if(!dimensionsAreOK && !HotProfile.SeamlessTexture)
                    {                        
                        inputImage = ImageOperations.PadImage(inputImage, tiles[0], tiles[1]);
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
            if (HotProfile.PreciseTileResolution)
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

                    if (imageHasAlpha && !HotProfile.IgnoreAlpha) //crop alpha
                    {
                        MagickImage outputImageAlpha = (MagickImage)inputImageAlpha.Clone();
                        outputImageAlpha.Crop(new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset, tileHeight + yOffset));
                        if (HotProfile.UseDifferentModelForAlpha)
                        {
                            string lrAlphaFolderPath = $"{lrPathAlpha}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}";
                            Directory.CreateDirectory(lrAlphaFolderPath);
                            outputImageAlpha.Write($"{lrAlphaFolderPath}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex.ToString("D2")}.png");                            
                        }
                        else
                            outputImageAlpha.Write($"{LrPath}{DirectorySeparator}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex.ToString("D2")}.png");
                    }
                    if (HotProfile.SplitRGB)
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
               
        void SplitTask(FileInfo file, Profile HotProfile)
        {
            MagickImage image = null, inputImage = null, inputImageAlpha = null;           
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
            
            ImagePreprocess(ref image, HotProfile);

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
            CreateTiles(file, inputImage, imageHasAlpha, HotProfile, inputImageAlpha);
            IncrementDoneCounter();            
            ReportProgress();
            GC.Collect();
        }
            
        async public Task Split(FileInfo[] inputFiles = null)
        {
            if (!IsSub)
                SaveSettings();
            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;

            DirectoryInfo inputDirectory = new DirectoryInfo(InputDirectoryPath);
            DirectoryInfo lrDirectory = new DirectoryInfo(LrPath);
            FileInfo[] inputDirectoryFiles = inputDirectory.GetFiles("*", searchOption);
            if (inputDirectoryFiles.Count() == 0)
            {
                WriteToLog("No files in input folder!", Color.Red);
                return;
            }

            DirectoryInfo lrAlphaDirectory = new DirectoryInfo(LrPath + "_alpha");

            lrDirectory.GetFiles("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
            WriteToLog($"'{LrPath}' is cleared", Color.LightBlue);

            //if (HotProfile.UseDifferentModelForAlpha)
            //{
            if (!lrAlphaDirectory.Exists)
                lrAlphaDirectory.Create();
            lrAlphaDirectory.GetFiles("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
            WriteToLog($"'{LrPath + "_alpha"}' is cleared", Color.LightBlue);
            //}

            WriteToLog("Creating tiles...");

            if (inputFiles == null)
                inputFiles = inputDirectoryFiles;
                       
            ResetDoneCounter();
            SetTotalCounter(inputFiles.Length);          
            ReportProgress();           
            
            await Task.Run(() => Parallel.ForEach(inputFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = maxConcurrency }, file =>
            {
                if (!file.Exists)
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

            WriteToLog("Finished!", Color.LightGreen);
        }

        #endregion

        #region MERGE

        bool GetTileResolution(FileInfo file, string basePath, int[] tiles, string resultSuffix, ref int tileWidth, ref int tileHeight, Profile HotProfile)
        {
            MagickImageInfo lastTile;
            try
            {
                string pathToLastTile = $"{ResultsPath + basePath}_tile-{((tiles[1] - 1) * tiles[0] + tiles[0] - 1).ToString("D2")}{resultSuffix}.png";
                if (HotProfile.SplitRGB)
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
        
        Image ExtractTiledTexture(Image imageResult, int upscaleModificator, int expandSize)
        {            
            int edgeSize = upscaleModificator * expandSize;
            Image tempImage = imageResult.Copy();
            tempImage = tempImage.ExtractArea(edgeSize, edgeSize, imageResult.Width - edgeSize * 2, imageResult.Height - edgeSize * 2).Copy();
            return tempImage;
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

        Image MergeTiles(FileInfo file, int[] tiles, string basePath, string basePathAlpha, string resultSuffix, List<FileInfo> tileFilesToDelete, bool imageHasAlpha, Profile HotProfile)
        {
            bool useMosaic = false;
            bool alphaReadError = false, cancelRgbGlobalbalance = false, cancelAlphaGlobalbalance = false;
            Image imageResult = null, imageAlphaResult = null;

            Image imageAlphaRow = null;
            int tileWidth = 0, tileHeight = 0;
            GetTileResolution(file, basePath, tiles, resultSuffix, ref tileWidth, ref tileHeight, HotProfile);

            for (int i = 0; i < tiles[1]; i++)
            {
                Image imageRow = null;

              for (int j = 0; j < tiles[0]; j++)
                {
                    int tileIndex = i * tiles[0] + j;

                    Image imageNextTile = null, imageAlphaNextTile = null;                 
                    try
                    {
                        if (HotProfile.SplitRGB)                        
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

                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
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
                                if (HotProfile.BalanceAlphas)
                                    UseGlobalbalance(ref imageAlphaRow, ref cancelAlphaGlobalbalance, $"{file.Name} alpha");
                            }
                        }
                        catch (VipsException ex)
                        {
                            alphaReadError = true;
                            if(!HotProfile.IgnoreSingleColorAlphas)
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

                    if (HotProfile.BalanceRgb)
                        UseGlobalbalance(ref imageRow, ref cancelRgbGlobalbalance, $"{file.Name}");
                    imageNextTile.Dispose();
                }
               

                if (i == 0)
                {
                    imageResult = imageRow;//.Copy();
                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
                        imageAlphaResult = imageAlphaRow.Copy();
                }
                else
                {
                    JoinTiles(ref imageResult, imageRow, useMosaic, Enums.Direction.Vertical, 0, -tileHeight * i);
                    imageRow.Dispose();

                    if (HotProfile.BalanceRgb)
                        UseGlobalbalance(ref imageResult, ref cancelRgbGlobalbalance, file.Name);

                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
                    {
                        JoinTiles(ref imageAlphaResult, imageAlphaRow, useMosaic, Enums.Direction.Vertical, 0, -tileHeight * i);
                        if (HotProfile.BalanceAlphas)
                            UseGlobalbalance(ref imageAlphaResult, ref cancelAlphaGlobalbalance, $"{file.Name} alpha");
                    }
                }
                GC.Collect();
            }

            if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
            {
                imageResult = imageResult.Bandjoin(imageAlphaResult);
                imageResult = imageResult.Copy(interpretation: "srgb").Cast("uchar");
                imageAlphaResult.Dispose();
            }
            return imageResult;
        }
        
        internal void MergeTask(FileInfo file, string basePath, Profile HotProfile, string outputFilename = "")
        {
            #region IMAGE READ
            
            string basePathAlpha = basePath;
            string resultSuffix = "";

            if (AdvancedUseResultSuffix)
                resultSuffix = AdvancedResultSuffix;

            if (OutputDestinationMode == 1) // grab alpha tiles from different folder
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
                image = ImageOperations.LoadImage(file);
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

            if (HotProfile.SeamlessTexture)
            {
                imageWidth += expandSize * 2;
                imageHeight += expandSize * 2;
            }

            if (imageHeight * imageWidth > MaxTileResolution)
            {
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolution);
                bool dimensionsAreOK = imageWidth % tiles[0] == 0 && imageHeight % tiles[1] == 0;
                if (!dimensionsAreOK && !HotProfile.SeamlessTexture)
                {
                    int[] newDimensions = Helper.GetGoodDimensions(image.Width, image.Height, tiles[0], tiles[1]);                   
                    tiles = Helper.GetTilesSize(newDimensions[0], newDimensions[1], MaxTileResolution);
                }
            }
            else
                tiles = new int[] { 1, 1 };
                       
            List<FileInfo> tileFilesToDelete = new List<FileInfo>();
            #endregion
            
            Image imageResult = MergeTiles(file, tiles, basePath, basePathAlpha, resultSuffix, tileFilesToDelete, imageHasAlpha, HotProfile);
            if (imageResult == null)
                return;

            #region SAVE IMAGE

            ImageFormatInfo outputFormat;
            if (HotProfile.UseOriginalImageFormat)
                outputFormat = HotProfile.FormatInfos.Where(x => x.Extension.Equals(file.Extension, StringComparison.InvariantCultureIgnoreCase)).First(); //hack, may be bad
            else
                outputFormat = HotProfile.selectedOutputFormat;
            if (outputFormat == null)
                outputFormat = new ImageFormatInfo(file.Extension);

            string destinationPath = OutputDirectoryPath + basePath + outputFormat;

            if(outputFilename != "")
                destinationPath = OutputDirectoryPath + basePath.Replace(Path.GetFileNameWithoutExtension(file.Name), outputFilename) + outputFormat;

            if (OutputDestinationMode == 3)            
                destinationPath = $"{OutputDirectoryPath}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirectorySeparator}" +
                    $"{Path.GetFileNameWithoutExtension(file.Name)}{outputFormat}";

            if (HotProfile.SeamlessTexture)
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
                (!HotProfile.ThresholdEnabled || (HotProfile.ThresholdBlackValue == 0 && HotProfile.ThresholdWhiteValue == 100)) &&
                HotProfile.ResizeImageAfterScaleFactor == 1.0) //no need to convert to MagickImage, save fast with vips
            {
                if (HotProfile.OverwriteMode == 2)
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
            MagickImage finalImage = new MagickImage(imageBuffer, readSettings);

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
            
            finalImage.Dispose();
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

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;           
           
            FileInfo[] inputFiles = di.GetFiles("*", searchOption);

            WriteToLog("Counting files...");
            await GetTotalFileNumber(inputFiles);

            WriteToLog("Merging tiles...");
            await Task.Run(() => Parallel.ForEach(inputFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = maxConcurrency }, file =>
            {
                if (!file.Exists)
                    return;

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

                if (OutputDestinationMode == 1)
                {
                    DirectoryInfo imagesFolder;

                    if (profile.SplitRGB)
                    {
                        imagesFolder = new DirectoryInfo(ResultsPath + $"{DirectorySeparator}images{DirectorySeparator}" + Path.GetFileNameWithoutExtension(file.Name) + "_R");

                        foreach (var image in imagesFolder.GetFiles("*", SearchOption.TopDirectoryOnly).
                            Where(x => x.Name.Contains(Path.GetFileNameWithoutExtension(file.Name) + "_R" + "_tile-00")))
                        {
                            string basePath = $"{DirectorySeparator}images{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}{DirectorySeparator}" +
                                $"{Path.GetFileNameWithoutExtension(image.Name).Replace("_R", "")}";
                            basePath = basePath.Remove(basePath.Length - 8, 8);                                                
                            MergeTask(file, basePath, profile);
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
                            MergeTask(file, basePath, profile);
                        }
                    }
                    return;
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
                            MergeTask(
                                file,
                                $"{DirectorySeparator}models{DirectorySeparator}{modelFolder.Name}{DirectorySeparator}{Path.GetFileNameWithoutExtension(file.Name)}",
                                profile);
                            break;
                        }
                    }
                    return;
                }
                if (OutputDestinationMode == 3)
                {                                                   
                    MergeTask(
                        file,
                        file.FullName.Replace(InputDirectoryPath, "").Replace(file.Name, Path.GetFileNameWithoutExtension(file.Name)),
                        profile);
                    return;
                }                
                MergeTask(file, DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name), profile);
            }));

            GC.Collect();
            WriteToLog("Finished!", Color.LightGreen);

            string pathToMergedFiles = OutputDirectoryPath;
            if (OutputDestinationMode == 1)
                pathToMergedFiles += $"{DirectorySeparator}images";
            if (OutputDestinationMode == 2)
                pathToMergedFiles += $"{DirectorySeparator}models";
        }

        async Task GetTotalFileNumber(FileInfo[] inputFiles)
        {
            if (OutputDestinationMode == 0 || OutputDestinationMode == 3)
            {
                SetTotalCounter(inputFiles.Length);              
                return;
            }

            await Task.Run(() => Parallel.ForEach(inputFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = maxConcurrency }, file =>
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

        async public Task<bool> Upscale(bool NoWindow = false, Profile HotProfile = null)
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

            DirectoryInfo directory = new DirectoryInfo(ResultsPath);
            if (!directory.Exists)
                directory.Create();

            if (CreateMemoryImage)
            {
                Image image = Image.Black(MaxTileResolutionWidth, MaxTileResolutionHeight);
                image.WriteToFile($"{LrPath}{DirectorySeparator}([000])000)_memory_helper_image.png");
            }

            Process process;
            if (UseBasicSR)
                process = await BasicSR_Test(NoWindow, HotProfile);
            else
                process = await ESRGAN(NoWindow, HotProfile);

            int processExitCode = await RunProcessAsync(process);
            if (processExitCode == -666)
                return false;
            if (processExitCode != 0)
            {
                WriteToLog("Error ocured during ESRGAN work!", Color.Red);
                return false;
            }
            WriteToLog("ESRGAN finished!", Color.LightGreen);


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
            if (UseBasicSR) scriptPath = EsrganPath + $"{DirectorySeparator}codes{DirectorySeparator}IEU_test.py";
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

        async Task<Process> ESRGAN(bool NoWindow, Profile HotProfile)
        {
            if (checkedModels.Count > 1 && HotProfile.UseDifferentModelForAlpha)
            {
                WriteToLog("Only single model must be selected when using different model for alpha");
                return null;
            }

            Process process = new Process();

            string upscaleSizePattern = "(?:_?[1|2|4|8|16]x_)|(?:_x[1|2|4|8|16]_?)|(?:_[1|2|4|8|16]x_?)|(?:_?x[1|2|4|8|16]_)";

            process.StartInfo.Arguments = $"{EsrganPath}";
            process.StartInfo.Arguments += GetCondaEnv();
            bool noValidModel = true;
            string torchDevice = UseCPU ? "cpu" : "cuda";
            int upscaleMultiplayer = 0;
            string resultsPath = ResultsPath;
            if (HotProfile.OverwriteMode == 1)
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
                process.StartInfo.Arguments +=
                    $" & python IEU_test.py \"{checkedModel.FullName}\" {upscaleMultiplayer} {torchDevice}" +
                    $" \"{LrPath + $"{DirectorySeparator}*"}\" \"{resultsPath}\"";
            }

            if (HotProfile.UseDifferentModelForAlpha)
            {   //detect upsacle factor for alpha model
                bool validModelAlpha = false;
                int upscaleMultiplayerAlpha = 0;
                var regResultAlpha = Regex.Match(HotProfile.ModelForAlpha.Name.ToLower(), upscaleSizePattern);
                if (regResultAlpha.Success && regResultAlpha.Groups.Count == 1)
                {
                    upscaleMultiplayerAlpha = int.Parse(regResultAlpha.Value.Replace("x", "").Replace("_", ""));
                    validModelAlpha = true;
                }
                else
                {
                    if (await DetectModelUpscaleFactor(HotProfile.ModelForAlpha))
                    {
                        upscaleMultiplayerAlpha = HotProfile.ModelForAlpha.UpscaleFactor;
                        validModelAlpha = true;
                    }
                }
                if (upscaleMultiplayer != upscaleMultiplayerAlpha)
                {
                    WriteToLog("Upscale size for rgb model and alpha model must be the same");
                    return null;
                }

                if (validModelAlpha)
                    process.StartInfo.Arguments +=
                        $" & python IEU_test.py \"{HotProfile.ModelForAlpha.FullName}\" {upscaleMultiplayerAlpha} {torchDevice}" +
                        $" \"{LrPath + $"_alpha{DirectorySeparator}*"}\" \"{resultsPath}\"";
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
            if (OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;
            SetTotalCounter(Directory.GetFiles(LrPath, "*", searchOption).Count() * checkedModels.Count);
            if (HotProfile.UseDifferentModelForAlpha)
                SetTotalCounter(FilesTotal + Directory.GetFiles(LrPath + "_alpha", "*", searchOption).Count());        

            ResetDoneCounter();

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
            string upscaleSizePattern = "(?:_?[1|2|4|8|16]x_)|(?:_x[1|2|4|8|16]_?)|(?:_[1|2|4|8|16]x_?)|(?:_?x[1|2|4|8|16]_)";

            process.StartInfo.Arguments = $"{EsrganPath}";
            process.StartInfo.Arguments += GetCondaEnv();

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

            if (HotProfile.UseDifferentModelForAlpha)
            {
                TestConfig configAlpha = new TestConfig(HotProfile.ModelForAlpha.FullName);

                //detect upsacle factor for alpha model                
                bool validModelAlpha = false;
                int upscaleMultiplayerAlpha = 0;
                var regResultAlpha = Regex.Match(HotProfile.ModelForAlpha.Name.ToLower(), upscaleSizePattern);
                if (regResultAlpha.Success && regResultAlpha.Groups.Count == 1)
                {
                    upscaleMultiplayerAlpha = int.Parse(regResultAlpha.Value.Replace("x", "").Replace("_", ""));
                    validModelAlpha = true;
                }
                else
                {
                    if (await DetectModelUpscaleFactor(HotProfile.ModelForAlpha))
                    {
                        upscaleMultiplayerAlpha = HotProfile.ModelForAlpha.UpscaleFactor;
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

        #endregion

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

            await Task.Run(() => Parallel.ForEach(originalFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = maxConcurrency }, file =>
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

        public void InterpolateImages(System.Drawing.Image imageA, System.Drawing.Image imageB, string destinationPath, double alpha)
        {
            var result = ImageInterpolation.Interpolate(imageA, imageB, destinationPath, alpha);
            if (result.Item1)            
                WriteToLog($"{Path.GetFileName(destinationPath)}", Color.LightGreen);      
            else            
                WriteToLog($"{Path.GetFileName(destinationPath)}: failed to interpolate.\n{result.Item2}", Color.Red);        
        }
        #endregion

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

            foreach (var folder in previewFolders)
            {
                if (!folder.Exists)
                    folder.Create();
                else
                    folder.GetFiles("*", SearchOption.AllDirectories).ToList().ForEach(x => x.Delete());
            }

            FileInfo previewOriginal = new FileInfo(previewInputDirPath + $"{DirectorySeparator}preview.png");
            FileInfo preview = new FileInfo(previewDirPath + $"{DirectorySeparator}preview.png");
                        
            //original.Save(previewOriginal.FullName, ImageFormat.Jpeg);

            var i2 = new System.Drawing.Bitmap(original);
            i2.Save(previewOriginal.FullName, ImageFormat.Png);

            if (previewIEU == null)
                previewIEU = new IEU(true);

            previewIEU.EsrganPath = EsrganPath;
            previewIEU.LrPath = previewLrDirPath;
            previewIEU.InputDirectoryPath = previewInputDirPath;
            previewIEU.ResultsPath = previewResultsDirPath;
            previewIEU.OutputDirectoryPath = previewDirPath;
            previewIEU.MaxTileResolution = MaxTileResolution;
            previewIEU.OverlapSize = OverlapSize;     
            previewIEU.OutputDestinationMode = 0;
            previewIEU.UseCPU = UseCPU;
            previewIEU.UseBasicSR = UseBasicSR;
            previewIEU.CurrentProfile = CurrentProfile.Clone();
            previewIEU.CurrentProfile.OverwriteMode = 0;
            previewIEU.DisableRuleSystem = true;

            await previewIEU.Split(new FileInfo[] { previewOriginal });
            ModelInfo previewModelInfo = new ModelInfo(Path.GetFileNameWithoutExtension(modelPath), modelPath);
            previewIEU.SelectedModelsItems = new List<ModelInfo>() { previewModelInfo };
            bool success = await previewIEU.Upscale(true);
            if (!success)
            {
                File.WriteAllText(previewDirPath + $"{DirectorySeparator}log.txt", previewIEU.Logs);
                return null;
            }
            CreateModelTree();
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
}
}



