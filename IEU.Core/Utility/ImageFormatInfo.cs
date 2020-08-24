using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    public class ImageFormatInfo
    {
        static List<string> VipsNativeFormats = new List<string>() { ".png", ".tiff", ".webp" };
        static List<string> ForeigntFormats = new List<string>() { ".tga", ".dds", ".jpg", ".bmp" };
        public static string[] ImageExtensions = new string[] {".PNG",".JPG",".JPEG",".WEBP",".BMP",".TGA",".DDS",".TIF",".TIFF"};

        private string _extension;
        [ProtoMember(1)]
        public string Extension
        {
            get => _extension;
            set
            {
                _extension = value;
                DisplayName = _extension.ToUpper().Replace(".", "");
                VipsNative = VipsNativeFormats.Contains(_extension);
            }
        }       
        public string DisplayName { get; set; }
        public bool VipsNative { get; set; }     
        [ProtoMember(2)]
        public int CompressionFactor { get; set; }
        [ProtoMember(3)]
        public int QualityFactor { get; set; }
        [ProtoMember(4)]
        public string CompressionMethod { get; set; }
      
        public ImageFormatInfo(string extension)
        {
            Extension = extension;
        }

        public ImageFormatInfo()
        {            
        }

        public override string ToString()
        {
            return Extension;
        }

        static public bool IsVipsNative(string extension)
        {
            return VipsNativeFormats.Contains(extension);
        }
    }
}
