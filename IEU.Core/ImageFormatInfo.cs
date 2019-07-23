using System;
using System.Collections.Generic;
using System.Text;

namespace ImageEnhancingUtility.Core
{
    public class ImageFormatInfo
    {
        private string _extension;
        public string Extension
        {
            get => _extension;
            set
            {
                _extension = value;
                DisplayName = _extension.ToUpper().Remove(0, 1);
                VipsNative = losslessFormats.Contains(_extension);
            }
        }
        public string DisplayName { get; set; }
        public bool VipsNative { get; set; }
        public bool Lossless { get; set; }
        public int CompressionFactor { get; set; }
        public string CompressionMethod { get; set; }

        public ImageFormatInfo(string extension)
        {
            Extension = extension;
        }

        public override string ToString()
        {
            return Extension;
        }

        static public bool IsVipsNative(string extension)
        {
            return losslessFormats.Contains(extension);
        }

        static List<string> losslessFormats = new List<string>() { ".png", ".tiff", ".webp" };
        static List<string> convertFormats = new List<string>() { ".tga", ".dds", ".jpg", ".bmp" };
    }
}
