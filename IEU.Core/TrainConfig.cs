using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ImageEnhancingUtility.Train
{
    public class TrainConfig : ReactiveObject
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
                if (value == "srragan_hfen")
                    InitHfen();
                if (value == "ppon")
                    InitPPON();
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
        Datasets _datasets = new Datasets();
        [JsonProperty("datasets")]
        [Browsable(false)]
        public Datasets Datasets { get => _datasets; set => this.RaiseAndSetIfChanged(ref _datasets, value); }
        [JsonProperty("network_G")]
        [Browsable(false)]
        public NetworkG NetworkG { get; set; } = new NetworkG();
        [Browsable(false)]
        [JsonProperty("network_D")]
        public NetworkD NetworkD { get; set; } = new NetworkD();

        Train _train = new Train();
        [JsonProperty("train")]
        [Browsable(false)]
        public Train Train { get => _train; set => this.RaiseAndSetIfChanged(ref _train, value); }
        Logger _logger = new Logger();
        [JsonProperty("logger")]
        [Browsable(false)]
        public Logger Logger { get => _logger; set => this.RaiseAndSetIfChanged(ref _logger, value); }
        [JsonProperty("IETU_Settings")]
        [Browsable(false)]
        public IETUSettings IETUSettings { get; set; } = new IETUSettings();

        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changed { get; }
        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changing { get; }
        [Browsable(false)]
        public new IObservable<Exception> ThrownExceptions { get; }

        void InitPPON()
        {
            Name = "001_PPON";
            Datasets.Train.HRSize = 192;
            NetworkG.WhichModelG = "ppon";
            NetworkG.Nb = 24;
            NetworkD.WhichModelD = "discriminator_vgg_192";
            Train.LrScheme = "StepLR_Restart";
            Train.LrG = 2e-4;
            Train.PixelCriterion = "cb";
            Train.FeatureCriterion = "elastic";
            Train.HfenCriterion = "l1";
            Train.TvWeight = 1e-6;
            Train.Niter = 210000;
        }
        void InitHfen()
        {
            Name = "001_HFEN";
            Datasets.Train.HRSize = 128;
            NetworkG = new NetworkG();
            NetworkD = new NetworkD();
            Train = new Train();
        }

    }

    public class TrainDataset : ReactiveObject
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "DIV2K";
        [JsonProperty("mode")]
        public string Mode { get; set; } = "LRHROTF";

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
        [JsonProperty("subset_file")]
        public object SubsetFile { get; set; } = null;
        [JsonProperty("use_shuffle")]
        public bool UseShuffle { get; set; } = true;
        [JsonProperty("n_workers")]
        public int NWorkers { get; set; } = 2;
        [JsonProperty("batch_size")]
        public int BatchSize { get; set; } = 16;
        int _hRSize = 128;
        [JsonProperty("HR_size")]
        public int HRSize
        {
            get => _hRSize;
            set => this.RaiseAndSetIfChanged(ref _hRSize, value);
        }
        [JsonProperty("use_flip")]
        public bool UseFlip { get; set; } = true;
        [JsonProperty("use_rot")]
        public bool UseRot { get; set; } = true;
        [JsonProperty("color")]
        public string Color { get; set; } = null;
        [JsonProperty("hr_crop")]
        public bool HrCrop { get; set; } = false;
        [JsonProperty("hr_rrot")]
        public bool HrRrot { get; set; } = false;
        [JsonProperty("lr_downscale")]
        public bool LrDownscale { get; set; } = false;
        List<DownscaleType> _lrDownscaleTypes = new List<DownscaleType>();
        [JsonProperty("lr_downscale_types")]
        public List<DownscaleType> LrDownscaleTypes
        {
            get => _lrDownscaleTypes;
            set => this.RaiseAndSetIfChanged(ref _lrDownscaleTypes, value);
        }
        [JsonProperty("lr_blur")]
        public bool LrBlur { get; set; } = false;
        [JsonProperty("lr_blur_types")]
        public List<string> LrBlurTypes { get; set; }
        [JsonProperty("lr_noise")]
        public bool LrNoise { get; set; } = false;
        [JsonProperty("lr_noise_types")]
        public List<string> LrNoiseTypes { get; set; }
        [JsonProperty("lr_noise2")]
        public bool LrNoise2 { get; set; } = false;
        [JsonProperty("lr_noise_types2")]
        public IList<string> LrNoiseTypes2 { get; set; }
        [JsonProperty("hr_noise")]
        public bool HrNoise { get; set; } = false;
        [JsonProperty("hr_noise_types")]
        public IList<string> HrNoiseTypes { get; set; }

        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changed { get; }
        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changing { get; }
        [Browsable(false)]
        public new IObservable<Exception> ThrownExceptions { get; }
    }
    public class ValDataset : ReactiveObject
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "val_set14_part";
        [JsonProperty("mode")]
        public string Mode { get; set; } = "LRHROTF";
        string _datarootHR = "../datasets/val/hr";
        [JsonProperty("dataroot_HR")]
        public string DatarootHR
        {
            get => _datarootHR;
            set => this.RaiseAndSetIfChanged(ref _datarootHR, value);
        }
        string _datarootLR = "../datasets/val/lr";
        [JsonProperty("dataroot_LR")]
        public string DatarootLR
        {
            get => _datarootLR;
            set => this.RaiseAndSetIfChanged(ref _datarootLR, value);
        }
        [JsonProperty("hr_crop")]
        public bool HrCrop { get; set; } = false;
        [JsonProperty("lr_downscale")]
        public bool LrDownscale { get; set; } = false;
        [JsonProperty("lr_downscale_types")]
        public List<DownscaleType> LrDownscaleTypes { get; set; }
    }
    public class Datasets : ReactiveObject
    {
        TrainDataset _train = new TrainDataset();
        [JsonProperty("train")]
        public TrainDataset Train { get; set; } = new TrainDataset();
        //{ get => _train; set => this.RaiseAndSetIfChanged(ref _train, value); }          
        [JsonProperty("val")]
        public ValDataset Val { get; set; } = new ValDataset();
    }
    public class Path : ReactiveObject
    {
        string _root = "C:\\BasicSR-master";
        [JsonProperty("root")]
        public string Root
        {
            get => _root;
            set => this.RaiseAndSetIfChanged(ref _root, value);
        }
        [JsonProperty("resume_state")]
        public string ResumeState { get; set; } = "";
        [JsonProperty("pretrain_model_G")]
        public string PretrainModelG { get; set; } = "";
    }
    public class NetworkG
    {
        [JsonProperty("which_model_G")]
        public string WhichModelG { get; set; } = "RRDB_net"; // RRDB_net | sr_resnet
        [JsonProperty("norm_type")]
        public object NormType { get; set; } = null;
        [JsonProperty("mode")]
        public string Mode { get; set; } = "CNA";
        [JsonProperty("nf")]
        public int Nf { get; set; } = 64;
        [JsonProperty("nb")]
        public int Nb { get; set; } = 23;
        [JsonProperty("in_nc")]
        public int InNc { get; set; } = 3;
        [JsonProperty("out_nc")]
        public int OutNc { get; set; } = 3;
        [JsonProperty("gc")]
        public int Gc { get; set; } = 32;
        [JsonProperty("group")]
        public int Group { get; set; } = 1;
        [JsonProperty("convtype")]
        public string Convtype { get; set; } = "Conv2D"; //"Conv2D" | "PartialConv2D"
    }
    public class NetworkD
    {
        [JsonProperty("which_model_D")]
        public string WhichModelD { get; set; } = "discriminator_vgg_128";
        [JsonProperty("norm_type")]
        public string NormType { get; set; } = "batch";
        [JsonProperty("act_type")]
        public string ActType { get; set; } = "leakyrelu";
        [JsonProperty("mode")]
        public string Mode { get; set; } = "CNA";
        [JsonProperty("nf")]
        public int Nf { get; set; } = 64;
        [JsonProperty("in_nc")]
        public int InNc { get; set; } = 3;
    }
    public class Train : ReactiveObject
    {
        [JsonProperty("lr_G")]
        public double LrG { get; set; } = 1e-4;
        [JsonProperty("weight_decay_G")]
        public double WeightDecayG { get; set; } = 0;
        [JsonProperty("beta1_G")]
        public double Beta1G { get; set; } = 0.9;
        [JsonProperty("lr_D")]
        public double LrD { get; set; } = 1e-4;
        [JsonProperty("weight_decay_D")]
        public double WeightDecayD { get; set; } = 0;
        [JsonProperty("beta1_D")]
        public double Beta1D { get; set; } = 0.9;
        /// <summary>
        /// lr change at every step (multiplied by)
        /// </summary>
        [JsonProperty("lr_gamma")]
        [Description("lr change at every step (multiplied by)")]
        public double LrGamma { get; set; } = 0.5;

        string _lrScheme = "MultiStepLR";
        /// <summary>
        /// <value>
        /// "MultiStepLR" | MultiStepLR_Restart | "StepLR" | StepLR_Restart | CosineAnnealingLR_Restart
        /// </value>
        /// </summary>       
        [Description("MultiStepLR | MultiStepLR_Restart | StepLR | StepLR_Restart | CosineAnnealingLR_Restart")]
        [JsonProperty("lr_scheme")]
        public string LrScheme
        {
            get => _lrScheme;
            set
            {
                if (value == "MultiStepLR")
                {
                    LrSteps = new int[] { 50000, 100000, 200000, 300000 };
                }
                if (value == "StepLR_Restart")
                {
                    LrStepSizes = new int[] { 1000, 250, 250 };
                    Restarts = new int[] { 138000, 172500 };
                }
                if (value == "MultiStepLR_Restart")
                {
                    LrSteps = new int[] { 34500, 69000, 103500, 155250, 189750, 241500 };
                    Restarts = new int[] { 138000, 172500 };
                }
                _lrScheme = value;
            }
        }
        [JsonProperty("lr_steps")]
        public int[] LrSteps { get; set; } = { 50000, 100000, 200000, 300000 };
        /// <summary>
        /// lr_() * each weight in "restart_weights" for each restart in "restarts"
        /// </summary>
        [JsonProperty("lr_step_sizes")]
        [Category("PPON")]
        [Description("lr_() * each weight in restart_weights for each restart in restarts")]
        public int[] LrStepSizes { get; set; } = { 1000, 250, 250 };
        /// <summary>
        /// Restart iterations for "MultiStepLR_Restart", "StepLR_Restart" and "CosineAnnealingLR_Restart"
        /// </summary>
        [JsonProperty("restarts")]
        [Category("PPON")]
        [Description("Restart iterations for MultiStepLR_Restart, StepLR_Restart and CosineAnnealingLR_Restart")]
        public int[] Restarts { get; set; } = { 138000, 172500 };
        [JsonProperty("restart_weights")]
        [Category("PPON")]
        public double[] RestartWeights { get; set; } = { 0.5, 0.5 };
        [JsonProperty("clear_state")]
        [Category("PPON")]
        public bool ClearState { get; set; } = true;
        /// <summary>
        /// "l1" | "l2" | "cb" | "elastic"
        /// </summary>
        [JsonProperty("pixel_criterion")]
        [Description("")]
        public string PixelCriterion { get; set; } = "l1"; // "l1" | "l2" | "cb"
        [JsonProperty("pixel_weight")]
        public double PixelWeight { get; set; } = 1e-2;
        /// <summary>
        /// "l1" | "l2" | "cb" | "elastic"
        /// </summary>
        [JsonProperty("feature_criterion")]
        public string FeatureCriterion { get; set; } = "l1"; // "l1" | "l2" | "cb"
        [JsonProperty("feature_weight")]
        public int FeatureWeight { get; set; } = 1;
        /// <summary>
        /// "l1" | "l2"
        /// </summary>
        [JsonProperty("hfen_criterion")]
        [Description("l1 | l2")]
        public string HfenCriterion { get; set; } = "l2"; // "l1" | "l2" | "cb"
        [JsonProperty("hfen_weight")]
        public double HfenWeight { get; set; } = 1e-6;
        [JsonProperty("tv_type")]
        public string TvType { get; set; } = "normal";
        [JsonProperty("tv_weight")]
        public double TvWeight { get; set; } = 0;

        /// <summary>
        /// <value>
        /// "ssim" | "ms-ssim"
        /// </value>
        /// </summary>
        [JsonProperty("ssim_type")]
        [Category("PPON")]
        [Description("ssim | ms-ssim")]
        public string SsimType { get; set; } = "ms-ssim";
        [JsonProperty("ssim_weight")]
        [Category("PPON")]
        public double SsimWeight { get; set; } = 1;

        [JsonProperty("gan_type")]
        public string GanType { get; set; } = "vanilla";
        [JsonProperty("gan_weight")]
        public double GanWeight { get; set; } = 5e-3;

        [JsonProperty("train_phase")]
        [Category("PPON")]
        public int TrainPhase { get; set; } = 1;
        [JsonProperty("phase1_s")]
        [Category("PPON")]
        public int Phase1S { get; set; } = 138000;
        [JsonProperty("phase2_s")]
        [Category("PPON")]
        public int Phase2S { get; set; } = 172500;
        [JsonProperty("phase3_s")]
        [Category("PPON")]
        public int Phase3S { get; set; } = 210000;

        [JsonProperty("manual_seed")]
        public int ManualSeed { get; set; } = 0;
        [JsonProperty("niter")]
        public double Niter { get; set; } = 5e5;
        [JsonProperty("val_freq")]
        public int ValFreq { get; set; } = 1000;

        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changed { get; }
        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changing { get; }
        [Browsable(false)]
        public new IObservable<Exception> ThrownExceptions { get; }
    }
    public class Logger
    {
        [JsonProperty("print_freq")]
        public int PrintFreq { get; set; } = 100;
        [JsonProperty("save_checkpoint_freq")]
        public int SaveCheckpointFreq { get; set; } = 1000;
    }
    public class IETUSettings
    {
        [JsonProperty("IETU_DisablePretrainedModel")]
        public bool DisablePretrainedModel = true;
        [JsonProperty("IETU_DisableResumeState")]
        public bool DisableResumeState = true;
        [JsonProperty("IETU_ResumeState")]
        public string ResumeState { get; set; } = "";
        [JsonProperty("IETU_PretrainModelG")]
        public string PretrainModelG { get; set; } = "";
        [JsonProperty("IETU_UseHrAsLr")]
        public bool UseHrAsLr = true;
    }
}
