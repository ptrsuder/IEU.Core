using ImageEnhancingUtility.Core;
using Newtonsoft.Json;
using ProtoBuf;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ImageEnhancingUtility.BasicSR;

namespace ImageEnhancingUtility.Train
{ 
    [ProtoContract]
    public class IETU: ReactiveObject
    {
        public static List<int> ScaleSizes = new List<int>() { 1, 2, 4, 8, 16 };
        public static List<int> HrSizes = new List<int>() { 128, 192 };
        public static List<string> NetworkModels = new List<string>() { "srragan_hfen", "ppon" };

        public IEU Core;
        TrainConfig _trainConfig;
        public TrainConfig TrainConfig
        {
            get => _trainConfig;
            set => this.RaiseAndSetIfChanged(ref _trainConfig, value);
        }
        public string SaveConfigName = "NewConfig";
        string _selectedConfigName = "";
        public string SelectedConfigName
        {
            get => _selectedConfigName;
            set => _selectedConfigName = value?.Replace(".json","");
        }

        [ProtoMember(0)]
        public string DatasetFolderPath;
        [ProtoMember(1)]
        public bool IgnoreFewColorsTiles = true;
        [ProtoMember(2)]
        public int ValidationTileNumber = 10; 

        public List<int> CheckedLrDownscaleTypes { get; set; }

        private List<FileInfo> _configs = new List<FileInfo>();
        public List<FileInfo> Configs
        {
            get => _configs;
            set => this.RaiseAndSetIfChanged(ref _configs, value);            
        }      

        public static Dictionary<NoiseType, string> NoiseTypes = new Dictionary<NoiseType, string>()
        {
            {NoiseType.Jpeg,  "JPEG"},
            {NoiseType.Gaussian,  "gaussian"},
            {NoiseType.Quantize,  "quantize"},
            {NoiseType.Poisson,  "poisson"},
            {NoiseType.Dither,  "dither"},
            {NoiseType.Speckle,  "speckle"},
            {NoiseType.SaltPepper,  "s&p"},
            {NoiseType.Clean,  "clean"}
        };

        DictionaryBindingList<NoiseType, int> _noiseValues = new DictionaryBindingList<NoiseType, int>()
        {
            {NoiseType.Jpeg,  0},
            {NoiseType.Gaussian,  0},
            {NoiseType.Quantize,  0},
            {NoiseType.Poisson,  0},
            {NoiseType.Dither,  0},
            {NoiseType.Speckle,  0},
            {NoiseType.SaltPepper,  0},
            {NoiseType.Clean,  0}
        };

        public DictionaryBindingList<NoiseType, int> NoiseValues
        {
            get => _noiseValues;
            set => this.RaiseAndSetIfChanged(ref _noiseValues, value);            
        }

        List<int> GetNoiseArray(Dictionary<NoiseType, int> noiseList)
        {
            List<int> values = new List<int>();
            values.AddRange(noiseList.Values);
            return values;
        }

        void CreateConfigList()
        {
            DirectoryInfo configDir = new DirectoryInfo("configs");
            if (!configDir.Exists)
                return;
            List<FileInfo> tempList = new List<FileInfo>();           
            foreach (var file in configDir.GetFiles("*.json"))
                tempList.Add(file);
                //tempList.Add(System.IO.Path.GetFileNameWithoutExtension(file.Name));
            Configs = tempList;
        }

        public ReactiveCommand<string, Unit> SaveConfigCommand { get; }
        public ReactiveCommand<string, Unit> LoadConfigCommand { get; }
        public ReactiveCommand<string, Unit> DeleteConfigCommand { get; }
        public ReactiveCommand<Unit, Unit> TrainCommand { get; }

        public IETU()
        {
            Action<string> func = x => SaveConfig(x);
            SaveConfigCommand = ReactiveCommand.Create(func);
            func = x => LoadConfig(x);
            LoadConfigCommand = ReactiveCommand.Create(func);
            func = x => DeleteConfig(x);
            DeleteConfigCommand = ReactiveCommand.Create(func);
            TrainCommand = ReactiveCommand.CreateFromTask(Train);

            TrainConfig = new TrainConfig();            
            TrainConfig.WhenAnyValue(x => x.Path.Root).Subscribe(y => TrainConfig.Datasets.Train.DatarootHR = y + "\\datasets\\hr");
            TrainConfig.WhenAnyValue(x => x.Path.Root).Subscribe(y => TrainConfig.Datasets.Train.DatarootLR = y + "\\datasets\\lr");
            TrainConfig.WhenAnyValue(x => x.Path.Root).Subscribe(y => TrainConfig.Datasets.Val.DatarootHR = y + "\\datasets\\val\\hr");
            TrainConfig.WhenAnyValue(x => x.Path.Root).Subscribe(y => TrainConfig.Datasets.Val.DatarootLR = y + "\\datasets\\val\\lr");

            TrainConfig.WhenAnyValue(x => x.Datasets.Train.HRSize).Subscribe(y => TrainConfig.NetworkD.WhichModelD = $"discriminator_vgg_{y}");

            Core = new IEU();
            CreateConfigList();

            var properties = this.GetType().GetProperties();
        }

        public void ReadSettings()
        {
            if (!File.Exists("ietu.settings.proto"))
                return;
            FileStream fileStream = new FileStream("ietu.settings.proto", FileMode.Open)
            {
                Position = 0
            };
            Serializer.Merge(fileStream, this);
            fileStream.Close();
        }
        public void SaveSettings()
        {
            FileStream fileStream = new FileStream("ietu.settings.proto", FileMode.Create);
            Serializer.Serialize(fileStream, this);
            fileStream.Close();
        }

        public void LoadConfig(string name)
        {
            if (string.IsNullOrEmpty(name))
                name = SelectedConfigName;
            
            string json = File.ReadAllText("configs\\" + name + ".json");
            //JsonConvert.PopulateObject(json, TrainConfig);
            TrainConfig = JsonConvert.DeserializeObject<TrainConfig>(json);
            DictionaryBindingList<NoiseType, int> newNoiseValues = NoiseValues;
            foreach (var item in TrainConfig.Datasets.Train.LrNoiseTypes)
            {
                var myKey = NoiseTypes.FirstOrDefault(x => x.Value == item).Key;
                int index = newNoiseValues.IndexOf(myKey);
                int value = newNoiseValues[index].Value;
                newNoiseValues[index] = new KeyValue<NoiseType, int>(myKey, value + 1);
            }
            NoiseValues = newNoiseValues;
        }
        public void SaveConfig(string name, string destination="")
        {
            if (string.IsNullOrEmpty(destination))
                destination = "configs";
            string lrPath = TrainConfig.Datasets.Train.DatarootLR, valLrPath = TrainConfig.Datasets.Val.DatarootLR, pretrained = TrainConfig.Path.PretrainModelG, resumestate = TrainConfig.Path.ResumeState;
            if (string.IsNullOrEmpty(name))
                name = SaveConfigName;
            TrainConfig.Datasets.Train.LrNoiseTypes = new List<string>();
            foreach (var item in NoiseValues)
            {
                for (int i = 0; i < item.Value; i++)
                    TrainConfig.Datasets.Train.LrNoiseTypes.Add(NoiseTypes[item.Key]);
            }
            if (TrainConfig.IETUSettings.UseHrAsLr)
            {
                TrainConfig.Datasets.Train.DatarootLR = TrainConfig.Datasets.Train.DatarootHR;
                TrainConfig.Datasets.Val.DatarootLR = TrainConfig.Datasets.Val.DatarootHR;
            }
            if (TrainConfig.IETUSettings.DisablePretrainedModel)
                TrainConfig.Path.PretrainModelG = null;
            if(TrainConfig.IETUSettings.DisableResumeState)
                TrainConfig.Path.ResumeState = null;

            string json = JsonConvert.SerializeObject(TrainConfig, Formatting.Indented);
            Directory.CreateDirectory(destination);
            File.WriteAllText(destination+ "\\" + name + ".json", json);
            CreateConfigList();
            TrainConfig.Datasets.Train.DatarootLR = lrPath;
            TrainConfig.Path.PretrainModelG = pretrained;
            TrainConfig.Path.ResumeState = resumestate;
            TrainConfig.Datasets.Val.DatarootLR = valLrPath;
        }
        public void DeleteConfig(string name)
        {
            if (string.IsNullOrEmpty(name))
                name = SelectedConfigName;
            File.Delete("configs\\" + name + ".json");
            CreateConfigList();
        }

        public async void PrepareHR()
        {
            Core.MaxTileResolutionHeight = TrainConfig.Datasets.Train.HRSize;
            Core.MaxTileResolutionWidth = Core.MaxTileResolutionHeight;
            Core.PreciseTileResolution = true;
            Core.InputDirectoryPath = DatasetFolderPath;
            Core.LrPath = TrainConfig.Datasets.Train.DatarootHR;
            await Core.Split();

            Random rand = new Random();
            DirectoryInfo directoryInfo = new DirectoryInfo(Core.LrPath);
            for (int i = 0; i < ValidationTileNumber; i++)
            {
                FileInfo[] tiles = directoryInfo.GetFiles();                
                int randIndex = rand.Next(1, tiles.Length);
                tiles[randIndex].MoveTo(TrainConfig.Datasets.Val.DatarootHR + "\\" + tiles[randIndex].Name);
            }
        }

        public async Task Train()
        {
            Process process = GetTrainProcess();
                       
            int processExitCode = await Core.RunProcessAsync(process);
            
            if (processExitCode != 0)
            {
                Core.WriteToLog("Error ocured during training!", System.Drawing.Color.Red);               
            }
        }

        Process GetTrainProcess()
        {
            SaveConfig("Current_Train_Config", TrainConfig.Path.Root);
            Process process = new Process();
            string trainScriptPath = $"{TrainConfig.Path.Root}\\codes\\";
            string trainScriptName = "train.py";
            if(TrainConfig.Model == "ppon")
                trainScriptName = "train_ppon.py";
            process.StartInfo.Arguments += $"{trainScriptPath} & python {trainScriptName} -opt \"{TrainConfig.Path.Root}\\Current_Train_Config.json\"";         

            process.ErrorDataReceived += SortOutputHandler;
            process.OutputDataReceived += SortOutputHandler;

            Core.WriteToLog("Starting training with current config...");

            return process;
        }

        private void SortOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!string.IsNullOrEmpty(outLine.Data))            
                Core.WriteToLog(outLine.Data);            
        }

    }
}