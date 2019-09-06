using Newtonsoft.Json;
using ReactiveUI;
using System.ComponentModel;
using System.IO;

namespace ImageEnhancingUtility.BasicSR
{
    public class TestConfig : BasicConfig
    {
        [JsonProperty("datasets")]
        public TestDatasets Datasets = new TestDatasets();

        public TestConfig(string modelPath, string modelType)
        {
            Path.PretrainModelG = modelPath;
            Model = modelType;
        }

        public TestConfig(string modelPath)
        {
            Path.PretrainModelG = modelPath;
            long modelFilesize = new FileInfo(modelPath).Length;
            if (modelFilesize / (1024 * 1024) > 68)
                Model = "ppon";           
        }

        public void SaveConfig(string name, string destination = "")
        {
            if (string.IsNullOrEmpty(destination))
                destination = "configs";

            if (string.IsNullOrEmpty(name))
                name = "testConfig";

            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            Directory.CreateDirectory(destination);
            File.WriteAllText(destination + "\\" + name + ".json", json);
        }

        override internal void InitHfen()
        {
            NetworkG.WhichModelG = "RRDB_net";
            NetworkG.Nb = 23;
        }

        override internal void InitPPON()
        {
            NetworkG.WhichModelG = "ppon";
            NetworkG.Nb = 24;
        }
    }

    public class TestDatasets
    {
        [JsonProperty("test")]
        public TestDataset Test { get; set; } = new TestDataset();
    }
    public class TestDataset : ReactiveObject
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "DIV2K";
        [JsonProperty("mode")]
        public string Mode { get; set; } = "LR";
        string _datarootHR = "../datasets/train/hr";
        [JsonProperty("dataroot_HR")]
        public string DatarootHR
        {
            get => _datarootHR;
            set => this.RaiseAndSetIfChanged(ref _datarootHR, value);
        }
        string _datarootLR = "../datasets/train/lr";
        [JsonProperty("dataroot_LR")]
        public string DatarootLR
        {
            get => _datarootLR;
            set => this.RaiseAndSetIfChanged(ref _datarootLR, value);
        }

    }

}
