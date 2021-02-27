using System;
using System.Collections.Generic;
using System.IO;

namespace ImageEnhancingUtility.Core
{
    public class BatchValues
    {
        public int OutputMode;
        public int OverwriteMode;
        public int MaxTileResolution;
        public int MaxTileW;
        public int MaxTileH;
        public int OverlapSize;
        public int Padding;
        public bool Seamless;
        public string ResultSuffix;
        public bool UseModelChain;
        public List<ModelInfo> ModelChain = new List<ModelInfo>();

        //alpha settings
        public Dictionary<string, ImageValues> images = new Dictionary<string, ImageValues>();
    }

    public class ImageValues
    {
        public string Path;
        public int[] Dimensions;        

        public bool Resized { get => ResizeMod != 1.0; }        
        public double ResizeMod = 1.0;

        public int[] CropToDimensions;

        public int[] FinalDimensions;

        public int PaddingSize;

        public int TileW, TileH;        
        public int Columns, Rows;
        public int TilesCount { get => Columns * Rows; }

        public bool UseAlpha;
        public bool AlphaSolidColor;

        public Profile profile1;

        public bool Rgba = false;

        public List<UpscaleResult> results = new List<UpscaleResult>();      
    }

    public class UpscaleResult
    {
        public string BasePath; 
        public ModelInfo Model;   

        public UpscaleResult(string basePath, ModelInfo model)
        {
            BasePath = basePath;
            Model = model;
        }
    }
}
