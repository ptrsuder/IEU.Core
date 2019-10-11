using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageEnhancingUtility.Core
{
    public class MergeInputData
    {
        public FileInfo file;
        public int[] tiles;
        public string basePath;
        public string basePathAlpha;
        public string resultSuffix;
        public List<FileInfo> tileFilesToDelete;
        public bool imageHasAlpha;      
        public bool alphaReadError;
        public bool useMosaic = false;
        public int tileWidth;
        public int tileHeight;
        public bool cancelAlphaGlobalbalance = false;
        public bool cancelRgbGlobalbalance = false;
    }
}
