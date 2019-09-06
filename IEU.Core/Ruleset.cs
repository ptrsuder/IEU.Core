using ImageMagick;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    class Ruleset
    {
        [ProtoMember(1)]
        public string Name;
        [ProtoMember(2)]
        public string Description;
        
        bool IsGlobal = false;

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
            ".JPEG", ".BMP", ".TIFF"};
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

        public Ruleset()
        {

        }


    }
}
