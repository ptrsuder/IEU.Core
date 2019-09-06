using ProtoBuf;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using ImageMagick;

namespace ImageEnhancingUtility.Core
{
    class Settings : ReactiveObject
    {
        public static Dictionary<TiffCompression, string> TiffCompressionModes = new Dictionary<TiffCompression, string>()
        {
            { TiffCompression.None, "VIPS_FOREIGN_TIFF_COMPRESSION_NONE" },
            { TiffCompression.Jpeg, "VIPS_FOREIGN_TIFF_COMPRESSION_JPEG" },
            { TiffCompression.Deflate, "VIPS_FOREIGN_TIFF_COMPRESSION_DEFLATE" },
            { TiffCompression.LZW, "VIPS_FOREIGN_TIFF_COMPRESSION_LZW" }
        };

        public static Dictionary<WebpPreset, string> WebpPresets = new Dictionary<WebpPreset, string>()
        {
            { WebpPreset.Default, "VIPS_FOREIGN_WEBP_PRESET_DEFAULT" },
            { WebpPreset.Picture, "VIPS_FOREIGN_WEBP_PRESET_PICTURE" },
            { WebpPreset.Photo, "VIPS_FOREIGN_WEBP_PRESET_PHOTO" },
            { WebpPreset.Drawing, "VIPS_FOREIGN_WEBP_PRESET_DRAWING" },
            { WebpPreset.Icon, "VIPS_FOREIGN_WEBP_PRESET_ICON" },
            { WebpPreset.Text, "VIPS_FOREIGN_WEBP_PRESET_TEXT" },
        };


        private bool _ignoreAlpha = false;
        [ProtoMember(10)]
        public bool IgnoreAlpha
        {
            get => _ignoreAlpha;
            set => this.RaiseAndSetIfChanged(ref _ignoreAlpha, value);
        }
        [ProtoMember(34, IsRequired = true)]
        public bool IgnoreSingleColorAlphas = true;
        [ProtoMember(35)]
        public bool BalanceMonotonicImages = false;
        [ProtoMember(36, IsRequired = true)]
        public bool BalanceAlphas = true;

        private bool _preserveImageFormat;
        [ProtoMember(11)]
        public bool UseOriginalImageFormat
        {
            get => _preserveImageFormat;
            set => this.RaiseAndSetIfChanged(ref _preserveImageFormat, value);
        }

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

        public bool SplitRGB = false;

        public bool SeamlessTexture = false;

        public ImageFormatInfo SelectedOutputFormat;
        int _selectedOutputFormatIndex = 0;
        [ProtoMember(29)]
        public int SelectedOutputFormatIndex
        {
            get => _selectedOutputFormatIndex;
            set
            {
                if (FormatInfos != null)
                    SelectedOutputFormat = FormatInfos[value];
                this.RaiseAndSetIfChanged(ref _selectedOutputFormatIndex, value);
            }
        }

        bool ConvertImageFormat = false;

        private bool _useDifferentModelForAlpha = false;
        [ProtoMember(33)]
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

        [ProtoMember(37)]
        public ImageFormatInfo pngFormat = new ImageFormatInfo(".png")
        { CompressionFactor = 0 };
        [ProtoMember(38)]
        public ImageFormatInfo tiffFormat = new ImageFormatInfo(".tiff")
        { CompressionMethod = TiffCompressionModes[TiffCompression.None], QualityFactor = 100 };
        [ProtoMember(39)]
        public ImageFormatInfo webpFormat = new ImageFormatInfo(".webp")
        { CompressionMethod = WebpPresets[WebpPreset.Default], QualityFactor = 100 };
        public ImageFormatInfo tgaFormat = new ImageFormatInfo(".tga");
        public ImageFormatInfo ddsFormat = new ImageFormatInfo(".dds");
        public ImageFormatInfo jpgFormat = new ImageFormatInfo(".jpg");
        public ImageFormatInfo bmpFormat = new ImageFormatInfo(".bmp");
    }
}
