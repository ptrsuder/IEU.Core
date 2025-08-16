using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;
using NetVips;

namespace ImageEnhancingUtility
{
    public class Dictionaries
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

        public static Dictionary<string, int> OutputDestinationModesMultModels = new Dictionary<string, int>
        {
                { "Folder for each image", 1 },
                { "Folder for each model", 2 }
        };
        public static Dictionary<string, int> OutputDestinationModesSingleModel = new Dictionary<string, int>
        {
                { "Default", 0 },
                { "Recursive", 3 },
                { "Folder for each image", 1 },
                { "Folder for each model", 2 }
        };

        public static Dictionary<string, int> OverwriteModesAll = new Dictionary<string, int>
        {
                { "None", 0 },
                { "Tiles", 1 },
                { "Original image", 2 }
        };
        public static Dictionary<string, int> OverwriteModesNone = new Dictionary<string, int> { { "None", 0 } };

        public static Dictionary<string, int> ddsTextureType = new Dictionary<string, int>
        {
                { "Color", 0 },
                { "Color + Alpha", 1 },
                { "Normal Map", 2 }
        };

        public static Dictionary<int, string> MagickFilterTypes = new Dictionary<int, string>()
        {
            { (int)FilterType.Box, "Box" },
            { (int)FilterType.Catrom, "Catrom" },
            { (int)FilterType.Point, "Point" },
            { (int)FilterType.Lanczos, "Lanczos" },
            { (int)FilterType.Mitchell, "Mitchell" },
            { (int)FilterType.Cubic, "Cubic" }
        };

        public static Dictionary<int, string> VipsKernel = new Dictionary<int, string>()
        {
            { 0, "Nearest" },
            { 1, "Linear" },
            { 2, "Cubic" },
            { 3, "Mitchell" },
            { 4, "Lanczos2" },
            { 5, "Lanczos3" }
        };    
    }
}
