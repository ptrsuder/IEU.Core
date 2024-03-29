﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ImageEnhancingUtility.Core.Utility;
using ImageMagick;
using Color = System.Drawing.Color;
using Image = NetVips.Image;
using Path = System.IO.Path;

namespace ImageEnhancingUtility.Core
{
    public partial class IEU
    {
        async public Task Split(FileInfo[] inputFiles = null)
        {
            if (CurrentPreset.AutoSetTileSizeEnable)
                await AutoSetTileSize();

            if (!IsSub)
                SaveSettings();

            checkedModels = SelectedModelsItems;            

            SearchOption searchOption = SearchOption.TopDirectoryOnly;
            if (CurrentPreset.OutputDestinationMode == 3)
                searchOption = SearchOption.AllDirectories;

            DirectoryInfo inputDirectory = new DirectoryInfo(InputDirectoryPath);
            DirectoryInfo lrDirectory = new DirectoryInfo(LrPath);
            FileInfo[] inputDirectoryFiles = inputDirectory.GetFiles("*", searchOption)
               .Where(x => ImageFormatInfo.ImageExtensions.Contains(x.Extension.ToUpperInvariant())).ToArray();
            if (inputDirectoryFiles.Count() == 0)
            {
                Logger.Write("No files in input folder!", Color.Red);
                return;
            }

            DirectoryInfo lrAlphaDirectory = new DirectoryInfo(LrPath + "_alpha");
            if (lrDirectory.Exists)
            {
                lrDirectory.Delete(true);
                Logger.Write($"'{LrPath}' is cleared", Color.LightBlue);
            }
            else
                lrDirectory.Create();


            if (!lrAlphaDirectory.Exists)
                lrAlphaDirectory.Create();
            else
            {
                lrAlphaDirectory.Delete(true);
                Logger.Write($"'{LrPath + "_alpha"}' is cleared", Color.LightBlue);
            }

            Logger.Write("Creating tiles...");

            if (inputFiles == null)
                inputFiles = inputDirectoryFiles;

            if (!CurrentPreset.InMemoryMode)
            {
                ResetDoneCounter();
                SetTotalCounter(inputFiles.Length);
                ReportProgress();
            }

            batchValues = new BatchValues()
            {
                MaxTileResolution = CurrentPreset.MaxTileResolution,
                MaxTileH = CurrentPreset.MaxTileResolutionHeight,
                MaxTileW = CurrentPreset.MaxTileResolutionWidth,
                OutputMode = CurrentPreset.OutputDestinationMode,
                OverwriteMode = CurrentPreset.OverwriteMode,
                OverlapSize = CurrentPreset.OverlapSize,
                Padding = CurrentProfile.PaddingSize,
                UseModelChain = CurrentPreset.UseModelChain,
                ModelChain = checkedModels
                //Seamless = 
            };

            await Task.Run(() => Parallel.ForEach(inputFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = MaxConcurrency }, file =>
            {
                if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper()))
                    return;
                bool fileSkipped = true;
                List<Rule> rules = new List<Rule>(Ruleset.Values);
                if (DisableRuleSystem)
                    rules = new List<Rule> { new Rule("Simple rule", CurrentProfile, CurrentFilter) };

                foreach (var rule in rules)
                {
                    if (rule.Filter.ApplyFilter(file))
                    {
                        SplitTask(file, rule.Profile);
                        fileSkipped = false;
                        break;
                    }
                }
                if (fileSkipped)
                {
                    IncrementDoneCounter(false);
                    Logger.Write($"{file.Name} is filtered, skipping", Color.HotPink);
                    return;
                }
            }));

            WriteBatchValues(batchValues);
            //if (!InMemoryMode)
            //    Logger.Write("Finished!", Color.LightGreen);
        }
        public async Task Split(FileInfo file)
        {
            Logger.Write($"{file.Name} SPLIT START");

            if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper()))
                return;
            bool fileSkipped = true;
            List<Rule> rules = new List<Rule>(Ruleset.Values);
            if (DisableRuleSystem)
                rules = new List<Rule> { new Rule("Simple rule", CurrentProfile, CurrentFilter) };

            checkedModels = SelectedModelsItems;             

            foreach (var rule in rules)
            {
                if (rule.Filter.ApplyFilter(file))
                {                    
                    if (rule.Profile.Model.UpscaleFactor == 0)
                        continue;                   
                    await Task.Run(() => SplitTask(file, rule.Profile));
                    fileSkipped = false;
                    break;
                }
            }
            if (fileSkipped)
            {
                IncrementDoneCounter(false);
                Logger.Write($"{file.Name} is filtered, skipping", Color.HotPink);
                return;
            }
        }

        [Category("Exposed")]
        public double whiteAlphaNoiseThreshold { get; set; } = 0.0001;
        void CreateTiles(FileInfo file, MagickImage inputImage, bool imageHasAlpha, Profile HotProfile)
        {
            var values = new ImageValues();
            values.Path = file.FullName;
            values.Dimensions = new int[] { inputImage.Width, inputImage.Height };
            values.UseAlpha = imageHasAlpha && !HotProfile.IgnoreAlpha && !CurrentProfile.RgbaModel;

            FileInfo fileAlpha = new FileInfo(file.DirectoryName + DirSeparator + Path.GetFileNameWithoutExtension(file.Name) + "_alpha.png");
            string lrPathAlpha = LrPath + "_alpha";
            int imageWidth = inputImage.Width, imageHeight = inputImage.Height;
            MagickImage inputImageRed = null, inputImageGreen = null, inputImageBlue = null, inputImageAlpha = null;

            int[] tiles;
            int[] paddedDimensions = new int[] { imageWidth, imageHeight };

            if (PreciseTileResolution)
            {
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, CurrentPreset.MaxTileResolutionWidth, CurrentPreset.MaxTileResolutionHeight);
                if (tiles[0] == 0 || tiles[1] == 0)
                {
                    Logger.Write(file.Name + " resolution is smaller than specified tile size");
                    return;
                }
            }
            else
            {
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, CurrentPreset.MaxTileResolution);
                if (IsSub) SetTotalCounter(tiles[0] * tiles[1]);
                values.Columns = tiles[0];
                values.Rows = tiles[1];
                values.CropToDimensions = new int[] { imageWidth, imageHeight };
                paddedDimensions = GetPaddedDimensions(imageWidth, imageHeight, tiles, ref values, HotProfile);
                inputImage = ImageOperations.PadImage(inputImage, paddedDimensions[0], paddedDimensions[1]);
            }

            ImageOperations.ImagePreprocess(ref inputImage, ref values, HotProfile, Logger);
            //if (inputImageAlpha != null && imageHasAlpha == true)
            //    ImagePreprocess(ref inputImageAlpha, paddedDimensions, HotProfile);

            imageWidth = inputImage.Width;
            imageHeight = inputImage.Height;
            tiles = Helper.GetTilesSize(imageWidth, imageHeight, CurrentPreset.MaxTileResolution);

            values.FinalDimensions = new int[] { inputImage.Width, inputImage.Height };
            values.Columns = tiles[0];
            values.Rows = tiles[1];

            if (CurrentProfile.PaddingSize > 0)
            {
                Image im = ImageOperations.ConvertToVips(inputImage); //TODO: open from file in the beginning              
                //im = Image.NewFromFile(file.FullName);      
                im = im.Embed(CurrentProfile.PaddingSize, CurrentProfile.PaddingSize, im.Width + 2 * CurrentProfile.PaddingSize, im.Height + 2 * CurrentProfile.PaddingSize, "VIPS_EXTEND_COPY");
               
                inputImage = ImageOperations.ConvertToMagickImage(im);
                values.FinalDimensions = new int[] { inputImage.Width, inputImage.Height };
                values.PaddingSize = CurrentProfile.PaddingSize;
            }

            if (values.UseAlpha)
                inputImageAlpha = (MagickImage)inputImage.Separate(Channels.Alpha).First();

            if (!CurrentProfile.RgbaModel)
                inputImage.HasAlpha = false;

            int seamlessPadding = 0;
            if (HotProfile.SeamlessTexture)
            {
                inputImage = ImageOperations.ExpandTiledTexture(inputImage, ref seamlessPadding);
                values.FinalDimensions = new int[] { inputImage.Width + 2 * seamlessPadding, inputImage.Height + 2 * seamlessPadding };
            }

            if (values.UseAlpha)
            {
                if (HotProfile.UseFilterForAlpha)
                    imageHasAlpha = false;
                else
                {
                    if (HotProfile.IgnoreSingleColorAlphas)
                    {
                        var hist = inputImageAlpha.Histogram();
                        bool isSolidColor = hist.Count == 1;                       

                        if (!isSolidColor)
                        {     
                            var white = new MagickColor("#FFFFFF");
                            var black = new MagickColor("#000000");

                            if (hist.Count == 2 && hist.ContainsKey(white) && hist.ContainsKey(black))
                            {
                                isSolidColor = false;                                
                            }
                            else if (hist.ContainsKey(white))
                                isSolidColor = hist[white] >= inputImageAlpha.Width * inputImageAlpha.Height * (1 - whiteAlphaNoiseThreshold); //margin of error                            
                        }

                        values.AlphaSolidColor = isSolidColor;

                        if (HotProfile.IgnoreSingleColorAlphas && isSolidColor)
                        {
                            inputImageAlpha.Dispose();
                            inputImageAlpha = null;
                            imageHasAlpha = false;
                            values.UseAlpha = false;
                        }
                        else
                        {
                            if (HotProfile.SeamlessTexture)
                                inputImageAlpha = ImageOperations.ExpandTiledTexture(inputImageAlpha, ref seamlessPadding);
                        }
                    }
                    else
                    {
                        if (HotProfile.SeamlessTexture)
                            inputImageAlpha = ImageOperations.ExpandTiledTexture(inputImageAlpha, ref seamlessPadding);
                    }

                }
            }

            if (HotProfile.SplitRGB)
            {
                if (inputImage.ColorSpace == ColorSpace.RGB || inputImage.ColorSpace == ColorSpace.sRGB)
                {
                    inputImageRed = (MagickImage)inputImage.Separate(Channels.Red).FirstOrDefault();
                    inputImageGreen = (MagickImage)inputImage.Separate(Channels.Green).FirstOrDefault();
                    inputImageBlue = (MagickImage)inputImage.Separate(Channels.Blue).FirstOrDefault();
                }
                else
                {
                    //TODO: convert to RGB                    
                    Logger.Write($"{file.Name}: not RGB");
                    return;
                }
            }

            #region CREATE TILES

            int tileWidth = imageWidth / tiles[0];
            int tileHeight = imageHeight / tiles[1];

            values.TileW = tileWidth;
            values.TileH = tileHeight;

            bool addColumn = false, addRow = false;
            int rows = tiles[1], columns = tiles[0];
            if (PreciseTileResolution)
            {
                tileWidth = CurrentPreset.MaxTileResolutionWidth;
                tileHeight = CurrentPreset.MaxTileResolutionHeight;
                if (imageWidth % tileWidth >= 16)
                {
                    addColumn = true;
                    columns++;
                }
                if (imageHeight % tileHeight >= 16)
                {
                    addRow = true;
                    rows++;
                }
            }
            int rightOverlap, bottomOverlap;

            Directory.CreateDirectory($"{LrPath}{DirSeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}");
            Dictionary<string, string> lrImages = new Dictionary<string, string>();
            if (CurrentPreset.InMemoryMode)
                lrDict.Add(file.FullName, lrImages);

            int lastIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (row < rows - 1)
                        bottomOverlap = 1;
                    else
                        bottomOverlap = 0;
                    if (col < columns - 1)
                        rightOverlap = 1;
                    else
                        rightOverlap = 0;

                    int tileIndex = row * columns + col;
                    int xOffset = rightOverlap * CurrentPreset.OverlapSize;
                    int yOffset = bottomOverlap * CurrentPreset.OverlapSize;
                    int tile_X1 = col * tileWidth;
                    int tile_Y1 = row * tileHeight;

                    if (addColumn && col == columns - 1)
                        tile_X1 = imageWidth - tileWidth;
                    if (addRow && row == rows - 1)
                        tile_Y1 = imageHeight - tileHeight;

                    var cropRectangle = new MagickGeometry(tile_X1, tile_Y1, tileWidth + xOffset + (CurrentProfile.PaddingSize + seamlessPadding) * 2, tileHeight + yOffset + (CurrentProfile.PaddingSize + seamlessPadding) * 2);

                    if (values.UseAlpha) //crop alpha
                    {
                        MagickImage outputImageAlpha = (MagickImage)inputImageAlpha.Clone();
                        outputImageAlpha.Crop(cropRectangle);
                        string lrAlphaFolderPath = $"{lrPathAlpha}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}";
                        if (CurrentPreset.InMemoryMode)
                        {
                            if (HotProfile.UseDifferentModelForAlpha)
                            {
                                var outPath = $"{lrAlphaFolderPath}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}{file.Extension}";
                                lrImages.Add(outPath, outputImageAlpha.ToBase64());
                            }
                            else
                            {
                                var outPath = $"{LrPath}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}{file.Extension}";
                                lrImages.Add(outPath, outputImageAlpha.ToBase64());
                            }
                        }
                        else
                        {
                            if (HotProfile.UseDifferentModelForAlpha)
                            {
                                Directory.CreateDirectory(lrAlphaFolderPath);
                                if (SkipEsrgan)
                                    outputImageAlpha.Resize(new Percentage(checkedModels[0].UpscaleFactor * 100));
                                outputImageAlpha.Write($"{lrAlphaFolderPath}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}.png");
                            }
                            else
                            {
                                if (SkipEsrgan)
                                    outputImageAlpha.Resize(new Percentage(checkedModels[0].UpscaleFactor * 100));
                                outputImageAlpha.Write($"{LrPath}{DirSeparator}{Path.GetDirectoryName(fileAlpha.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}{Path.GetFileNameWithoutExtension(fileAlpha.Name)}_tile-{tileIndex:D2}.png");
                            }
                        }
                    }
                    if (HotProfile.SplitRGB)
                    {
                        var pathBase = $"{LrPath}{DirSeparator}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{Path.GetFileNameWithoutExtension(file.Name)}";
                        if (CurrentPreset.OutputDestinationMode == 3)
                            pathBase = $"{LrPath}" + file.FullName.Replace(InputDirectoryPath, "").Replace(file.Name, Path.GetFileNameWithoutExtension(file.Name));

                        var pathR = $"{pathBase}_R_tile-{tileIndex:D2}.png";
                        var pathG = $"{pathBase}_G_tile-{tileIndex:D2}.png";
                        var pathB = $"{pathBase}_B_tile-{tileIndex:D2}.png";

                        MagickImage outputImageRed = (MagickImage)inputImageRed.Clone();
                        outputImageRed.Crop(cropRectangle);
                        MagickImage outputImageGreen = (MagickImage)inputImageGreen.Clone();
                        outputImageGreen.Crop(cropRectangle);
                        MagickImage outputImageBlue = (MagickImage)inputImageBlue.Clone();
                        outputImageBlue.Crop(cropRectangle);

                        if (CurrentPreset.InMemoryMode)
                        {
                            lrImages.Add(Path.ChangeExtension(pathR, file.Extension), outputImageRed.ToBase64());
                            lrImages.Add(Path.ChangeExtension(pathG, file.Extension), outputImageGreen.ToBase64());
                            lrImages.Add(Path.ChangeExtension(pathB, file.Extension), outputImageBlue.ToBase64());
                        }
                        else
                        {
                            outputImageRed.Write(pathR);
                            outputImageGreen.Write(pathG);
                            outputImageBlue.Write(pathB);
                        }
                    }
                    else
                    {
                        MagickImage outputImage = (MagickImage)inputImage.Clone();

                        outputImage.Crop(cropRectangle);
                        MagickFormat format = MagickFormat.Png24;
                        if (CurrentProfile.RgbaModel) format = MagickFormat.Png32;                        
                        var dirpath = Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "");
                        string outPath = $"{LrPath}{dirpath}{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}_tile-{tileIndex:D2}.png";
                        if (!CurrentPreset.InMemoryMode)
                        {
                            if (SkipEsrgan)
                                outputImage.Resize(new Percentage(checkedModels[0].UpscaleFactor * 100));
                            outputImage.Write(outPath, format);
                        }
                        else
                        {
                            if (SkipEsrgan)
                                outputImage.Resize(new Percentage(checkedModels[0].UpscaleFactor * 100));
                            outPath = $"{LrPath}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}_tile-{tileIndex:D2}{file.Extension}";
                            lrImages.Add(outPath, outputImage.ToBase64());
                        }
                        lastIndex = tileIndex;
                    }
                }
            }
            #endregion

            var files = Directory.GetFiles(ResultsPath, "*", SearchOption.AllDirectories);
            var basename = Path.GetFileNameWithoutExtension(file.Name);

            foreach (var f in files)
            {
                //remove leftover old tiles from results
                var match = Regex.Match(Path.GetFileNameWithoutExtension(f), $"({Regex.Escape(basename)}_tile-)([0-9]*)");
                string t = match.Groups[2].Value;
                if (t == "") continue;
                if (Int32.Parse(t) > lastIndex)
                    File.Delete(f);
            }

            string basePath = "";

            if (checkedModels == null || checkedModels.Count == 0)
                if (HotProfile.UseModel && HotProfile.Model != null && !IsSub)
                    checkedModels = new List<ModelInfo>() { HotProfile.Model };

            if (CurrentPreset.UseModelChain)
            {
                basePath = DirSeparator + Path.GetFileNameWithoutExtension(file.Name);
                ModelInfo biggestModel = checkedModels[0];
                foreach (var model in checkedModels)
                    if (model.UpscaleFactor > biggestModel.UpscaleFactor)
                        biggestModel = model;
                values.results.Add(new UpscaleResult(basePath, biggestModel));
            }
            else
                foreach (var model in checkedModels)
                {
                    if (CurrentPreset.OutputDestinationMode == 0)
                        basePath = DirSeparator + Path.GetFileNameWithoutExtension(file.Name);

                    if (CurrentPreset.OutputDestinationMode == 3)
                        basePath = file.FullName.Replace(InputDirectoryPath, "").Replace(file.Name, Path.GetFileNameWithoutExtension(file.Name));

                    if (CurrentPreset.OutputDestinationMode == 1)
                    {
                        if (HotProfile.SplitRGB) //search for initial tiles in _R folder                    
                            basePath = $"{DirSeparator}Images{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}_ChannelChar{DirSeparator}" +
                                      $"{DirSeparator}[{Path.GetFileNameWithoutExtension(model.Name)}]_{Path.GetFileNameWithoutExtension(file.Name)}";
                        else
                            basePath = $"{DirSeparator}Images{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}" +
                                    $"{DirSeparator}[{Path.GetFileNameWithoutExtension(model.Name)}]_{Path.GetFileNameWithoutExtension(file.Name)}";
                    }
                    if (CurrentPreset.OutputDestinationMode == 2)
                    {
                        if (HotProfile.SplitRGB) //search for initial tiles in _R folder                    
                            basePath = $"{DirSeparator}Models{DirSeparator}{Path.GetFileNameWithoutExtension(model.Name)}" +
                                      $"{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}";
                        else
                            basePath = $"{DirSeparator}Models{DirSeparator}{Path.GetFileNameWithoutExtension(model.Name)}" +
                                $"{DirSeparator}{Path.GetFileNameWithoutExtension(file.Name)}";
                    }
                    values.results.Add(new UpscaleResult(basePath, model));
                }

            values.profile1 = HotProfile;
            if (!batchValues.images.ContainsKey(values.Path))
                batchValues.images.Add(values.Path, values);

            inputImage.Dispose();

            Logger.Write($"{file.Name} SPLIT DONE", Color.LightGreen);
        }        
        void SplitTask(FileInfo file, Profile HotProfile)
        {
            MagickImage image;
            bool imageHasAlpha = false;

            try
            {
                image = ImageOperations.LoadImage(file);                      
                imageHasAlpha = image.HasAlpha;
            }
            catch (Exception ex)
            {
                Logger.Write($"Failed to read file {file.Name}!", Color.Red);
                Logger.Write(ex.Message);
                return;
            }

            CreateTiles(file, image, imageHasAlpha, HotProfile);
            if (!CurrentPreset.InMemoryMode)
            {
                IncrementDoneCounter();
                ReportProgress();
            }
            GC.Collect();
        }       
    }
}
