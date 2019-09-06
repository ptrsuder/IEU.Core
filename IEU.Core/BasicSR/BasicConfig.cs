using Newtonsoft.Json;
using ReactiveUI;
using System.ComponentModel;
using System.IO;

namespace ImageEnhancingUtility.BasicSR
{
    abstract public class BasicConfig : ReactiveObject
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "001_HFEN";
        [JsonProperty("use_tb_logger")]
        public bool UseTbLogger { get; set; } = true;
        string _model = "srragan_hfen";
        [JsonProperty("model")]
        public string Model
        {
            get => _model;
            set
            {
                if (value == "ppon")
                    InitPPON();
                if (value == "srragan")
                    InitHfen();
                _model = value;
            }
        }
        [JsonProperty("scale")]
        public int Scale { get; set; } = 4;
        [JsonProperty("gpu_ids")]
        [Browsable(false)]
        public int[] GpuIds { get; set; } = { 0 };
        [JsonProperty("path")]
        [Browsable(false)]
        public Path Path { get; set; } = new Path();
        [JsonProperty("network_G")]
        [Browsable(false)]
        public NetworkG NetworkG { get; set; } = new NetworkG();

        abstract internal void InitHfen();
        abstract internal void InitPPON();        
    }
}
