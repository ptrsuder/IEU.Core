using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageEnhancingUtility.Core
{
    class BatchValues
    {
        public int OutputMode;
        public int OverwriteMode;
        public int MaxTileResolution;
        public int Overlap;
        public int Padding;
        public bool Seamless;
        //alpha settings
    }

    class ImageValues
    {
        //preprocess
        //postprocess
        bool splitrgb;
    }
}
