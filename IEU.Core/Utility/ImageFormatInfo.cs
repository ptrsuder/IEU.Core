using DdsFileTypePlus;
using ProtoBuf;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    public class ImageFormatInfo: ReactiveObject
    {
        static List<string> VipsNativeFormats = new List<string>() { ".png", ".tiff", ".webp" };
        static List<string> ForeigntFormats = new List<string>() { ".tga", ".dds", ".jpg", ".bmp" };
        public static string[] ImageExtensions = new string[] { ".PNG", ".JPG", ".JPEG", ".WEBP", ".BMP", ".TGA", ".DDS", ".TIF", ".TIFF" };
         
       
        public static ImageFormatInfo pngFormat = new ImageFormatInfo(".png")
        { CompressionFactor = 3 };
      
        public static ImageFormatInfo tiffFormat = new ImageFormatInfo(".tiff")
        { TiffCompressionMethod = Dictionaries.TiffCompressionModes[TiffCompression.None], TiffQuality = 100 };
       
        public static ImageFormatInfo webpFormat = new ImageFormatInfo(".webp")
        { WebpCompressionMethod = Dictionaries.WebpPresets[WebpPreset.Default], WebpQuality = 100 };
        public static ImageFormatInfo tgaFormat = new ImageFormatInfo(".tga");
        public static ImageFormatInfo ddsFormat = new ImageFormatInfo(".dds");
        public static ImageFormatInfo jpgFormat = new ImageFormatInfo(".jpg");
        public static ImageFormatInfo bmpFormat = new ImageFormatInfo(".bmp");

        #region DDS

        public static List<DdsFileFormatSetting> ddsFileFormatsColor = new List<DdsFileFormatSetting>
        {
            new DdsFileFormatSetting("BC1 (Linear) [DXT1]", DdsFileTypePlus.DdsFileFormat.BC1),
            new DdsFileFormatSetting("BC1 (sRGB) [DXT1]", DdsFileTypePlus.DdsFileFormat.BC1Srgb),
            new DdsFileFormatSetting("BC7 (Linear)", DdsFileTypePlus.DdsFileFormat.BC7),
            new DdsFileFormatSetting("BC7 (sRGB)", DdsFileTypePlus.DdsFileFormat.BC7Srgb),
            new DdsFileFormatSetting("BC4 (Grayscale)", DdsFileTypePlus.DdsFileFormat.BC4),
            new DdsFileFormatSetting("Lossless", DdsFileTypePlus.DdsFileFormat.R8G8B8A8)
        };
        readonly static List<DdsFileFormatSetting> ddsFileFormatsColorAlpha = new List<DdsFileFormatSetting>
        {
            new DdsFileFormatSetting("BC3 (Linear) [DXT5]", DdsFileTypePlus.DdsFileFormat.BC3),
            new DdsFileFormatSetting("BC3 (sRGB) [DXT5]", DdsFileTypePlus.DdsFileFormat.BC3Srgb),
            new DdsFileFormatSetting("BC2 (Linear)", DdsFileTypePlus.DdsFileFormat.BC2),
            new DdsFileFormatSetting("BC7 (Linear)", DdsFileTypePlus.DdsFileFormat.BC7),
            new DdsFileFormatSetting("BC7 (sRGB)", DdsFileTypePlus.DdsFileFormat.BC7Srgb),
            new DdsFileFormatSetting("Lossless", DdsFileTypePlus.DdsFileFormat.R8G8B8A8)
        };
        readonly static List<DdsFileFormatSetting> ddsFileFormatsNormalMap = new List<DdsFileFormatSetting>
        {
            new DdsFileFormatSetting("BC5 (Two channels)", DdsFileTypePlus.DdsFileFormat.BC5),
            new DdsFileFormatSetting("Lossless", DdsFileTypePlus.DdsFileFormat.R8G8B8A8)
        };
        readonly static List<DdsFileFormatSetting>[] ddsFileFormats = new List<DdsFileFormatSetting>[]
        {
            ddsFileFormatsColor, ddsFileFormatsColorAlpha, ddsFileFormatsNormalMap };

        public static List<BC7CompressionMode> ddsBC7CompressionModes = new List<BC7CompressionMode>
        {
            BC7CompressionMode.Fast,
            BC7CompressionMode.Normal,
            BC7CompressionMode.Slow
        };


        private List<DdsFileFormatSetting> _ddsFileFormatCurrent = ddsFileFormatsColor;
        public List<DdsFileFormatSetting> DdsFileFormatsCurrent
        {
            get => _ddsFileFormatCurrent;
            set
            {
                DdsFileFormat = value[0];
                this.RaiseAndSetIfChanged(ref _ddsFileFormatCurrent, value);
            }
        }

        private int _ddsTextureTypeSelectedIndex = 0;
        public int DdsTextureTypeSelectedIndex
        {
            get => _ddsTextureTypeSelectedIndex;
            set
            {
                DdsFileFormatsCurrent = ddsFileFormats[value];
                DdsFileFormat = DdsFileFormatsCurrent[0];
                this.RaiseAndSetIfChanged(ref _ddsTextureTypeSelectedIndex, value);
            }
        }

        private int _ddsFileFormatSelectedIndex = 0;       
        public int DdsFileFormatSelectedIndex
        {
            get => _ddsFileFormatSelectedIndex;
            set
            {
                if (DdsFileFormatsCurrent.Count > 0)
                    DdsFileFormat = DdsFileFormatsCurrent[value];
                this.RaiseAndSetIfChanged(ref _ddsFileFormatSelectedIndex, value);
            }
        }

        private int _ddsBC7CompressionSelected = 0;     
        public int DdsBC7CompressionSelected
        {
            get => _ddsBC7CompressionSelected;
            set => this.RaiseAndSetIfChanged(ref _ddsBC7CompressionSelected, value);
        }


        private DdsFileFormatSetting _ddsFileFormat;
        public DdsFileFormatSetting DdsFileFormat
        {
            get => _ddsFileFormat;
            set => this.RaiseAndSetIfChanged(ref _ddsFileFormat, value);
        }
        BC7CompressionMode _ddsBC7CompressionMode;
        public BC7CompressionMode DdsBC7CompressionMode
        {
            get => _ddsBC7CompressionMode;
            set => this.RaiseAndSetIfChanged(ref _ddsBC7CompressionMode, value);
        }

        bool _ddsGenerateMipmaps = true;
        [ProtoMember(34, IsRequired = true)]
        public bool DdsGenerateMipmaps
        {
            get => _ddsGenerateMipmaps;
            set => this.RaiseAndSetIfChanged(ref _ddsGenerateMipmaps, value);
        }

        bool _ddsIsCubemap = false;
        [ProtoMember(42, IsRequired = true)]
        public bool DdsIsCubemap
        {
            get => _ddsIsCubemap;
            set => this.RaiseAndSetIfChanged(ref _ddsIsCubemap, value);
        }


        #endregion

        private string _extension;
        [ProtoMember(1)]
        public string Extension
        {
            get => _extension;
            set
            {
                _extension = value;
                if(string.IsNullOrEmpty(Name))
                    Name = _extension.ToUpper().Replace(".", "");
                VipsNative = VipsNativeFormats.Contains(_extension);
            }
        }
        public string Name { get; set; }
        public bool VipsNative { get; set; }
        [ProtoMember(2)]
        public int CompressionFactor { get; set; } = 3;
        [ProtoMember(3)]
        public int WebpQuality { get; set; } = 100;
        public int TiffQuality { get; set; } = 100;

        [ProtoMember(4)]
        public string WebpCompressionMethod { get; set; } = Dictionaries.WebpPresets[WebpPreset.Default];
        public string TiffCompressionMethod { get; set; } = Dictionaries.TiffCompressionModes[TiffCompression.None];

        public ImageFormatInfo(string extension)
        {
            Extension = extension;
            ddsFileFormatsColor = new List<DdsFileFormatSetting>
            {
                new DdsFileFormatSetting("BC1 (Linear) [DXT1]", DdsFileTypePlus.DdsFileFormat.BC1),
            new DdsFileFormatSetting("BC1 (sRGB) [DXT1]", DdsFileTypePlus.DdsFileFormat.BC1Srgb),
            new DdsFileFormatSetting("BC7 (Linear)", DdsFileTypePlus.DdsFileFormat.BC7),
            new DdsFileFormatSetting("BC7 (sRGB)", DdsFileTypePlus.DdsFileFormat.BC7Srgb),
            new DdsFileFormatSetting("BC4 (Grayscale)", DdsFileTypePlus.DdsFileFormat.BC4),
            new DdsFileFormatSetting("Lossless", DdsFileTypePlus.DdsFileFormat.R8G8B8A8)
            };

            DdsFileFormatsCurrent = ddsFileFormatsColor;
        }

        public ImageFormatInfo(string name, string extension)
        {
            Extension = extension;
            DdsFileFormatsCurrent = ddsFileFormatsColor;
        }

        public ImageFormatInfo() {}

        public override string ToString()
        {
            return Extension;
        }

        static public bool IsVipsNative(string extension)
        {
            return VipsNativeFormats.Contains(extension);
        }
        public ImageFormatInfo Clone()
        {
            MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, this);
            stream.Position = 0;
            ImageFormatInfo clone = Serializer.Deserialize<ImageFormatInfo>(stream);
            stream.Close();
            return clone;
        }
            }
}