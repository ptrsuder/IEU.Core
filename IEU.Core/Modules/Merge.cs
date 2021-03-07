using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ImageEnhancingUtility.Core.Utility;
using ImageMagick;
using NetVips;
using Color = System.Drawing.Color;
using Image = NetVips.Image;
using Path = System.IO.Path;

namespace ImageEnhancingUtility.Core
{
    public partial class IEU
    {
        async public Task Merge()
        {
            if (!IsSub)
                SaveSettings();

            if (batchValues == null)
                batchValues = ReadBatchValues();

            //histMatchTest.MatchHist();
            //return;
            ////opencvTest.Stitch();
            ////return;

            DirectoryInfo di = new DirectoryInfo(InputDirectoryPath);

            ResetDoneCounter();
            ResetTotalCounter();

            int tempOutMode = OutputDestinationMode;
            if (UseModelChain) tempOutMode = 0;

            FileInfo[] inputFiles = batchValues.images.Keys.Select(x => new FileInfo(x)).ToArray();

            int totalFiles = 0;
            foreach (var image in batchValues.images.Values)
                totalFiles += image.results.Count;

            SetTotalCounter(totalFiles);

            Logger.Write("Merging tiles...");
            await Task.Run(() => Parallel.ForEach(inputFiles, parallelOptions: new ParallelOptions() { MaxDegreeOfParallelism = MaxConcurrency }, file =>
            //foreach(var file in inputFiles)
            {
                var values = batchValues.images[file.FullName];

                if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper()))
                    return;

                MagickImage inputImage = ImageOperations.LoadImage(file);

                Tuple<string, MagickImage> pathImage = new Tuple<string, MagickImage>(file.FullName, inputImage);

                Profile profile = new Profile();

                List<Rule> rules = new List<Rule>(Ruleset.Values);
                if (DisableRuleSystem)
                    rules = new List<Rule> { new Rule("Simple rule", CurrentProfile, CurrentFilter) };

                bool fileSkipped = true;
                foreach (var rule in rules)
                {
                    if (rule.Filter.ApplyFilter(file))
                    {
                        profile = rule.Profile;
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

                foreach (var result in values.results)
                    MergeTask(
                        pathImage,
                        values,
                        result,
                        profile);
            }
            ));

            GC.Collect();
            Logger.Write("Finished!", Color.LightGreen);

            string pathToMergedFiles = OutputDirectoryPath;
            if (tempOutMode == 1)
                pathToMergedFiles += $"{DirSeparator}Images";
            if (tempOutMode == 2)
                pathToMergedFiles += $"{DirSeparator}Models";
        }
        async Task Merge(string path, UpscaleResult result)
        {
            if (!IsSub)
                SaveSettings();

            if (batchValues == null)
                batchValues = ReadBatchValues();

            var values = batchValues.images[path];

            DirectoryInfo di = new DirectoryInfo(InputDirectoryPath);

            //ResetDoneCounter();
            //ResetTotalCounter();     

            FileInfo file = new FileInfo(path);

            Logger.Write($"{file.Name} MERGE START");

            if (!file.Exists || !ImageFormatInfo.ImageExtensions.Contains(file.Extension.ToUpper()))
                return;

            MagickImage inputImage = ImageOperations.LoadImage(file);

            Tuple<string, MagickImage> pathImage = new Tuple<string, MagickImage>(file.FullName, inputImage);

            Profile profile = new Profile();

            List<Rule> rules = new List<Rule>(Ruleset.Values);
            if (DisableRuleSystem)
                rules = new List<Rule> { new Rule("Simple rule", CurrentProfile, CurrentFilter) };

            bool fileSkipped = true;
            foreach (var rule in rules)
            {
                if (rule.Filter.ApplyFilter(file))
                {
                    profile = rule.Profile;
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

            await Task.Run(() =>
            {
                if (InMemoryMode)
                {
                    MergeTask(pathImage, values, result, profile);
                }
                else
                    foreach (var res in values.results)
                        MergeTask(
                            pathImage,
                            values,
                            res,
                            profile);
            });

            if (InMemoryMode)
            {
                lrDict.Remove(path);
                hrDict.Remove(path);
            }
            GC.Collect();

            string pathToMergedFiles = OutputDirectoryPath;
            if (OutputDestinationMode == 1)
                pathToMergedFiles += $"{DirSeparator}images";
            if (OutputDestinationMode == 2)
                pathToMergedFiles += $"{DirSeparator}models";
        }

        internal void MergeTask(Tuple<string, MagickImage> pathImage, ImageValues values, UpscaleResult result, Profile HotProfile, string outputFilename = "")
        {
            FileInfo file = new FileInfo(pathImage.Item1);
            Logger.WriteDebug($"Image path: {pathImage.Item1}");
            Logger.WriteDebug($"Base path: {result.BasePath}");

            #region IMAGE READ

            string resultSuffix = "";

            if (UseResultSuffix)
                resultSuffix = ResultSuffix;

            int imageWidth = values.Dimensions[0], imageHeight = values.Dimensions[1];

            int[] tiles = new int[] { values.Columns, values.Rows };
            int tileWidth = values.TileW, tileHeight = values.TileH;
            double resizeMod = values.ResizeMod;
            int upscaleMod = result.Model.UpscaleFactor;

            MagickImage inputImage = pathImage.Item2;
            if (inputImage.HasAlpha && !HotProfile.IgnoreAlpha && HotProfile.IgnoreSingleColorAlphas)
                if (values.AlphaSolidColor)
                    inputImage.HasAlpha = false;

            int expandSize = SeamlessExpandSize;
            if (imageHeight <= 32 || imageWidth <= 32)
                expandSize = 8;

            //if (HotProfile.SeamlessTexture)
            //{
            //    WriteToLogDebug($"Seamless texture, expand size: {expandSize}");
            //    imageWidth += expandSize * 2;
            //    imageHeight += expandSize * 2;
            //}

            List<FileInfo> tileFilesToDelete = new List<FileInfo>();
            #endregion          

            MagickImage finalImage = null;
            Image imageResult = null;

            ImageFormatInfo outputFormat;
            if (HotProfile.UseOriginalImageFormat)
                outputFormat = HotProfile.FormatInfos
                    .Where(x => x.Extension.Equals(file.Extension, StringComparison.InvariantCultureIgnoreCase)).First();
            else
                outputFormat = HotProfile.selectedOutputFormat;
            if (outputFormat == null)
                outputFormat = new ImageFormatInfo(file.Extension);

            Logger.WriteDebug($"Output format: {outputFormat.Extension}");

            string destinationPath = OutputDirectoryPath + result.BasePath.Replace("_ChannelChar", "") + outputFormat;

            if (outputFilename != "")
                destinationPath =
                    OutputDirectoryPath + result.BasePath.Replace(Path.GetFileNameWithoutExtension(file.Name), outputFilename) + outputFormat;

            if (OutputDestinationMode == 3)
                destinationPath = $"{OutputDirectoryPath}{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}" +
                    $"{Path.GetFileNameWithoutExtension(file.Name)}{outputFormat}";

            Logger.WriteDebug($"Destination path: {destinationPath}");

            int mergedWidth = 0, mergedHeight = 0;
            if (UseImageMagickMerge)
            {
                //finalImage = MergeTilesNew(pathImage, tiles, new int[] { tileWidth, tileHeight }, result.BasePath, basePathAlpha, resultSuffix, tileFilesToDelete, imageHasAlpha, HotProfile);
                //mergedWidth = finalImage.Width;
                //mergedHeight = finalImage.Height;
            }
            else
            {
                imageResult = MergeTiles(pathImage, values, result, tileFilesToDelete);
                mergedWidth = imageResult.Width;
                mergedHeight = imageResult.Height;
            }

            if (HotProfile.SeamlessTexture)
            {
                Logger.WriteDebug($"Extrating seamless texture. Upscale modificator: {upscaleMod}");
                if (UseImageMagickMerge)
                    finalImage = ExtractTiledTexture(finalImage, upscaleMod, expandSize);
                else
                    ExtractTiledTexture(ref imageResult, upscaleMod, expandSize);
            }
            else
            {
                if (UseImageMagickMerge)
                    finalImage.Crop(values.CropToDimensions[0] * upscaleMod, values.CropToDimensions[1] * upscaleMod, Gravity.Northwest);
                else
                    imageResult = imageResult.Crop(0, 0, values.CropToDimensions[0] * upscaleMod, values.CropToDimensions[1] * upscaleMod);
            }

            #region SAVE IMAGE

            if (!UseImageMagickMerge)
            {
                if (imageResult == null)
                    return;
                if (outputFormat.VipsNative &&
                    (!HotProfile.ThresholdEnabled || (HotProfile.ThresholdBlackValue == 0 && HotProfile.ThresholdWhiteValue == 100)) &&
                    (!HotProfile.ThresholdAlphaEnabled || (HotProfile.ThresholdBlackValue == 0 && HotProfile.ThresholdWhiteValue == 100)) &&
                      HotProfile.ResizeImageAfterScaleFactor == 1.0) //no need to convert to MagickImage, save faster with vips
                {
                    Logger.WriteDebug($"Saving with vips");

                    if (OverwriteMode == 2)
                    {
                        Logger.WriteDebug($"Overwriting file");
                        file.Delete();
                        destinationPath = Path.GetDirectoryName(file.FullName) + Path.GetFileNameWithoutExtension(file.FullName) + outputFormat;
                    }
                    else
                    {
                        string a = Path.GetDirectoryName(destinationPath);
                        if (!Directory.Exists(a))
                            Directory.CreateDirectory(a);
                    }

                    if (!WriteToFileVipsNative(imageResult, outputFormat, destinationPath))
                        return;
                    imageResult.Dispose();
                    //pathImage.Item2.Dispose();
                    IncrementDoneCounter();
                    //ReportProgress();
                    Logger.Write($"<{file.Name}> MERGE DONE", Color.LightGreen);

                    if (HotProfile.DeleteResults)
                        tileFilesToDelete.ForEach(x => x.Delete());
                    GC.Collect();
                    return;
                }

                byte[] imageBuffer = imageResult.PngsaveBuffer(compression: 0);

                var readSettings = new MagickReadSettings()
                {
                    Format = MagickFormat.Png00,
                    Compression = CompressionMethod.NoCompression,
                    Width = imageResult.Width,
                    Height = imageResult.Height
                };
                finalImage = new MagickImage(imageBuffer, readSettings);
            }

            ImageOperations.ImagePostrpocess(ref finalImage, HotProfile, Logger);

            if (OverwriteMode == 2)
            {
                file.Delete();
                destinationPath = $"{OutputDirectoryPath}{DirSeparator}" +
                    $"{Path.GetDirectoryName(file.FullName).Replace(InputDirectoryPath, "")}{DirSeparator}{file.Name}";
            }
            else
            {
                string a = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(a))
                    Directory.CreateDirectory(a);
            }

            if (outputFormat.Extension == ".dds")
                WriteToFileDds(finalImage, destinationPath, HotProfile);
            else
                finalImage.Write(destinationPath);
            imageResult?.Dispose();
            finalImage.Dispose();
            //pathImage.Item2.Dispose();
            IncrementDoneCounter();
            ReportProgress();
            Logger.Write($"{file.Name} DONE", Color.LightGreen);
            if (HotProfile.DeleteResults)
                tileFilesToDelete.ForEach(x => x.Delete());
            GC.Collect();
            #endregion
        }

        Image MergeTiles(Tuple<string, MagickImage> pathImage, ImageValues values, UpscaleResult result, List<FileInfo> tileFilesToDelete)
        {
            Profile HotProfile = values.profile1;
            int upMod = result.Model.UpscaleFactor;
            bool rgbaModel = values.Rgba;

            bool alphaReadError = false, cancelRgbGlobalbalance = false, cancelAlphaGlobalbalance = false;
            Image imageResult = null, imageAlphaResult = null;
            FileInfo file = new FileInfo(pathImage.Item1);
            Image imageAlphaRow = null;
            int tileWidth = values.TileW * upMod, tileHeight = values.TileH * upMod;

            string basePathAlpha = result.BasePath;
            if (OutputDestinationMode == 1) // grab alpha tiles from different folder
            {
                string fileName = Path.GetFileNameWithoutExtension(file.Name);
                basePathAlpha = basePathAlpha.Replace(
                    $"{DirSeparator}Images{DirSeparator}{fileName}",
                    $"{DirSeparator}Images{DirSeparator}{fileName}_alpha");
            }

            Dictionary<string, MagickImage> hrTiles = null;
            if (InMemoryMode)
            {
                hrTiles = hrDict[pathImage.Item1];
            }

            for (int i = 0; i < values.Rows; i++)
            {
                Image imageRow = null;

                for (int j = 0; j < values.Columns; j++)
                {
                    int tileIndex = i * values.Columns + j;

                    Image imageNextTile, imageAlphaNextTile;
                    try
                    {
                        if (values.profile1.SplitRGB)
                            imageNextTile = JoinRGB(pathImage, result, tileIndex, ResultSuffix, ref tileFilesToDelete);
                        else
                        {
                            string newTilePath = $"{ResultsPath + result.BasePath}_tile-{tileIndex:D2}{ResultSuffix}.png";
                            if (InMemoryMode)
                                imageNextTile = ImageOperations.ConvertToVips(hrTiles[newTilePath]);
                            else
                            {
                                imageNextTile = Image.NewFromFile(newTilePath, false, Enums.Access.Sequential);
                                tileFilesToDelete.Add(new FileInfo($"{ResultsPath + result.BasePath}_tile-{tileIndex:D2}{ResultSuffix}.png"));
                            }
                        }
                    }
                    catch (VipsException ex)
                    {
                        Logger.WriteOpenError(file, ex.Message);
                        return null;
                    }

                    if (values.UseAlpha && !alphaReadError && !HotProfile.UseFilterForAlpha)
                    {
                        try
                        {
                            var newAlphaTilePath = $"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{ResultSuffix}.png";
                            if (InMemoryMode)
                                imageAlphaNextTile = ImageOperations.ConvertToVips(hrTiles[newAlphaTilePath]);
                            else
                            {
                                imageAlphaNextTile = Image.NewFromFile(newAlphaTilePath, false, Enums.Access.Sequential);
                                tileFilesToDelete.Add(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{ResultSuffix}.png"));
                            }

                            //remove padding
                            if (PaddingSize > 0)
                                imageAlphaNextTile = imageAlphaNextTile.Crop(PaddingSize * upMod, PaddingSize * upMod, imageAlphaNextTile.Width - PaddingSize * upMod * 2, imageAlphaNextTile.Height - PaddingSize * upMod * 2);

                            if (j == 0)
                            {
                                imageAlphaRow = imageAlphaNextTile;
                            }
                            else
                            {
                                if (UseOldVipsMerge)
                                    JoinTiles(ref imageAlphaRow, imageAlphaNextTile, Enums.Direction.Horizontal, -tileWidth * j, 0);
                                else
                                    JoinTilesWithMask(ref imageAlphaRow, imageAlphaNextTile, false, Enums.Direction.Horizontal, -tileWidth * j, 0);
                                if (HotProfile.BalanceAlphas)
                                    UseGlobalbalance(ref imageAlphaRow, ref cancelAlphaGlobalbalance, $"{file.Name} alpha");
                            }

                        }
                        catch (VipsException ex)
                        {
                            alphaReadError = true;
                            if (!HotProfile.IgnoreSingleColorAlphas)
                                Logger.WriteOpenError(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{ResultSuffix}.png"), ex.Message);
                        }
                    }

                    //remove padding
                    if (PaddingSize > 0)
                        imageNextTile = imageNextTile.Crop(PaddingSize * upMod, PaddingSize * upMod, imageNextTile.Width - PaddingSize * upMod * 2, imageNextTile.Height - PaddingSize * upMod * 2);

                    if (j == 0)
                    {
                        imageRow = imageNextTile;
                        continue;
                    }
                    else
                    {
                        if (UseOldVipsMerge)
                            JoinTiles(ref imageRow, imageNextTile, Enums.Direction.Horizontal, -tileWidth * j, 0);
                        else
                            JoinTilesWithMask(ref imageRow, imageNextTile, false, Enums.Direction.Horizontal, -tileWidth * j, 0);
                    }

                    if (HotProfile.BalanceRgb)
                        UseGlobalbalance(ref imageRow, ref cancelRgbGlobalbalance, $"{file.Name}");
                    imageNextTile.Dispose();
                }

                if (i == 0)
                {
                    imageResult = imageRow;
                    if (values.UseAlpha && !alphaReadError && !HotProfile.UseFilterForAlpha)
                        imageAlphaResult = imageAlphaRow;
                }
                else
                {
                    if (UseOldVipsMerge)
                        JoinTiles(ref imageResult, imageRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                    else
                        JoinTilesWithMask(ref imageResult, imageRow, true, Enums.Direction.Vertical, 0, -tileHeight * i);
                    imageRow.Dispose();

                    if (HotProfile.BalanceRgb)
                        UseGlobalbalance(ref imageResult, ref cancelRgbGlobalbalance, file.Name);

                    if (values.UseAlpha && !alphaReadError && !HotProfile.UseFilterForAlpha)
                    {
                        if (UseOldVipsMerge)
                            JoinTiles(ref imageAlphaResult, imageAlphaRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                        else
                            JoinTilesWithMask(ref imageAlphaResult, imageAlphaRow, true, Enums.Direction.Vertical, 0, -tileHeight * i);

                        if (HotProfile.BalanceAlphas)
                            UseGlobalbalance(ref imageAlphaResult, ref cancelAlphaGlobalbalance, $"{file.Name} alpha");
                    }
                }
                GC.Collect();
            }

            if (values.UseAlpha && HotProfile.UseFilterForAlpha)
            {
                MagickImage image = new MagickImage(pathImage.Item2);
                MagickImage inputImageAlpha = (MagickImage)image.Separate(Channels.Alpha).First();
                MagickImage upscaledAlpha = null;

                if (upMod != 1)
                    upscaledAlpha = ImageOperations.ResizeImage(inputImageAlpha, upMod, (FilterType)HotProfile.AlphaFilterType);
                else
                    upscaledAlpha = inputImageAlpha;
                byte[] buffer = upscaledAlpha.ToByteArray(MagickFormat.Png00);
                imageAlphaResult = Image.NewFromBuffer(buffer);
                alphaReadError = true;
            }

            if (((values.UseAlpha && !alphaReadError) || HotProfile.UseFilterForAlpha) && !rgbaModel)
            {
                imageResult = imageResult.Bandjoin(imageAlphaResult.ExtractBand(0));
                imageResult = imageResult.Copy(interpretation: "srgb").Cast("uchar");
                imageAlphaResult.Dispose();
            }
            return imageResult;
        }
       
        void JoinTiles(ref Image imageRow, Image imageNextTile, string direction, int dx, int dy)
        {
            Logger.WriteDebug("Merging with old vips method");
            int mblendSize = EnableBlend ? OverlapSize : 0;
            Logger.WriteDebug($"mblend: {EnableBlend}");
            imageRow = imageRow.Merge(imageNextTile, direction, dx, dy, mblendSize);
        }

        void JoinTilesWithMask(ref Image imageRow, Image imageNextTile, bool Copy, string direction, int dx, int dy)
        {
            Logger.WriteDebug("Merging with new vips method");
            int overlap, resultW = imageRow.Width, resultH = imageRow.Height;

            if (direction == Enums.Direction.Horizontal)
            {
                overlap = imageRow.Width + dx;
                resultW += imageNextTile.Width - overlap;
            }
            else
            {
                overlap = imageRow.Height + dy;
                resultH += imageNextTile.Height - overlap;
            }

            Image result = imageRow.Merge(imageNextTile, direction, dx, dy);
            result = result.Resize(4);

            Image maskVips; //= CreateMask(imageRow.Width, imageRow.Height, overlap / 2, direction);           

            int offset = 1;
            Bitmap mask;
            Rectangle brushSize, gradientRectangle;
            LinearGradientMode brushDirection;
            if (direction == Enums.Direction.Horizontal)
            {
                brushSize = new Rectangle(-dx, -dy, overlap, imageRow.Height);
                brushDirection = LinearGradientMode.Horizontal;
                gradientRectangle = new Rectangle(-dx + offset, -dy, overlap - offset, imageNextTile.Height);
            }
            else
            {
                brushSize = new Rectangle(-dx, -dy + offset, imageRow.Width, overlap);
                brushDirection = LinearGradientMode.Vertical;
                gradientRectangle = new Rectangle(-dx, -dy + offset, imageRow.Width, overlap);
            }
            mask = new Bitmap(imageRow.Width, imageRow.Height);

            using (Graphics graphics = Graphics.FromImage(mask))
            using (LinearGradientBrush brush = new LinearGradientBrush(brushSize, Color.White, Color.Black, brushDirection))
            {
                graphics.FillRectangle(Brushes.White, 0, 0, imageRow.Width, imageRow.Height);
                graphics.FillRectangle(brush, gradientRectangle);
                //graphics.FillRectangle(Brushes.Black, 0, 0, imageRow.Width, imageRow.Height);
                //graphics.FillRectangle(Brushes.Black, imageRow.Width - 5, 0, 5, imageRow.Height);
                //graphics.FillRectangle(Brushes.Black, -dx + offset + overlap / 2, 0, overlap / 2, imageRow.Height);
            }
            //mask.Save(@"S:\ESRGAN-master\IEU_preview\mask1.png");
            var buffer = ImageOperations.ImageToByte(mask);
            maskVips = Image.NewFromBuffer(buffer).ExtractBand(0);
            //maskVips.WriteToFile(@"S:\ESRGAN-master\IEU_preview\mask1.png");

            //mask2
            Bitmap mask2;
            Rectangle brushSize2, gradientRectangle2;
            LinearGradientMode brushDirection2;
            if (direction == Enums.Direction.Horizontal)
            {
                brushSize2 = new Rectangle(0, 0, overlap / 2, imageNextTile.Height);
                brushDirection2 = LinearGradientMode.Horizontal;
                gradientRectangle2 = new Rectangle(0, 0, overlap / 2, imageNextTile.Height);
            }
            else
            {
                brushSize2 = new Rectangle(-dx, -dy + offset, imageNextTile.Width, overlap / 2);
                brushDirection2 = LinearGradientMode.Vertical;
                gradientRectangle2 = new Rectangle(-dx, -dy + offset, imageNextTile.Width, overlap / 2);
            }
            mask2 = new Bitmap(imageNextTile.Width, imageNextTile.Height);

            using (Graphics graphics = Graphics.FromImage(mask2))
            using (LinearGradientBrush brush = new LinearGradientBrush(brushSize2, Color.Black, Color.White, brushDirection2))
            {
                graphics.FillRectangle(Brushes.White, 0, 0, imageNextTile.Width, imageNextTile.Height);
                graphics.FillRectangle(brush, gradientRectangle2);
            }
            mask2.Save(@"S:\ESRGAN-master\IEU_preview\mask2.png");
            var buffer2 = ImageOperations.ImageToByte(mask2);
            Image maskVips2 = Image.NewFromBuffer(buffer2).ExtractBand(0);
            //imageNextTile = imageNextTile.Bandjoin(new Image[] { maskVips2 });
            //imageNextTile.WriteToFile(@"S:\ESRGAN-master\IEU_preview\nextTile.png");
            Image expandedImage;

            expandedImage = imageRow.Bandjoin(new Image[] { maskVips });

            //if (Copy)
            //    expandedImage = imageRow.Bandjoin(new Image[] { maskVips }).Copy();            
            //else
            //    expandedImage = imageRow.Bandjoin(new Image[] { maskVips }).CopyMemory();

            //expandedImage.WriteToFile(@"S:\ESRGAN-master\IEU_preview\expandedImage.png");
            //imageNextTile.WriteToFile(@"S:\ESRGAN-master\IEU_preview\nextTile.png");

            //result = Image.Black(resultW, resultH).Invert();
            //result = result.Insert(imageNextTile, -dx, -dy);

            result = result.Composite2(imageNextTile, "atop", -dx, -dy).Flatten();
            result = result.Composite2(expandedImage, "atop", 0, 0);
            //result.WriteToFile(@"S:\ESRGAN-master\IEU_preview\result1.png");

            imageRow = result.Flatten();
        }

        Image CreateMask(int w, int h, int overlap, string direction)
        {
            var black = Image.Black(w, h, 1);
            var white = black.Invert();
            Image mask;
            if (direction == Enums.Direction.Horizontal)
                mask = white.Insert(black, w - overlap, 0);
            else
                mask = white.Insert(black, 0, h - overlap);
            mask = mask.Gaussblur(4.6, precision: "float");
            return mask;
        }

        int[] GetPaddedDimensions(int imageWidth, int imageHeight, int[] tiles, ref ImageValues values, Profile HotProfile)
        {
            int[] paddedDimensions = new int[] { imageWidth, imageHeight };

            if (HotProfile.ResizeImageBeforeScaleFactor != 1.0)
            {
                //make sure that after downscale no pixels will be lost
                double scale = HotProfile.ResizeImageBeforeScaleFactor;
                int divider = scale < 1 ? (int)(1 / scale) : 1;
                bool dimensionsAreOK = imageWidth % divider == 0 && imageHeight % divider == 0;
                if (!dimensionsAreOK && !HotProfile.SeamlessTexture)
                    paddedDimensions = Helper.GetGoodDimensions(imageWidth, imageHeight, divider, divider);

                imageWidth = (int)(paddedDimensions[0] * HotProfile.ResizeImageBeforeScaleFactor);
                imageHeight = (int)(paddedDimensions[1] * HotProfile.ResizeImageBeforeScaleFactor);
                tiles = Helper.GetTilesSize(imageWidth, imageHeight, MaxTileResolution);
                values.CropToDimensions = new int[] { paddedDimensions[0], paddedDimensions[1] };
            }

            if (imageWidth * imageHeight > MaxTileResolution)
            {
                //make sure that image can be tiled without leftover pixels
                bool dimensionsAreOK = imageWidth % tiles[0] == 0 && imageHeight % tiles[1] == 0;

                if (!dimensionsAreOK && !HotProfile.SeamlessTexture)
                {
                    paddedDimensions = Helper.GetGoodDimensions(paddedDimensions[0], paddedDimensions[1], tiles[0], tiles[1]);
                }
            }
            return paddedDimensions;
        }

        void ExtractTiledTexture(ref Image imageResult, int upscaleModificator, int expandSize)
        {
            int edgeSize = upscaleModificator * expandSize;
            Image temp = imageResult.Copy();
            imageResult = temp.ExtractArea(edgeSize, edgeSize, imageResult.Width - edgeSize * 2, imageResult.Height - edgeSize * 2);
            temp.Dispose();
        }

        MagickImage ExtractTiledTexture(MagickImage imageResult, int upscaleModificator, int expandSize)
        {
            int edgeSize = upscaleModificator * expandSize;
            MagickImage tempImage = new MagickImage(imageResult);
            tempImage.Crop(imageResult.Width - edgeSize * 2, imageResult.Height - edgeSize * 2, Gravity.Center);
            //(edgeSize, edgeSize, imageResult.Width - edgeSize * 2, imageResult.Height - edgeSize * 2);
            tempImage.RePage();
            return tempImage;
        }

        Image JoinRGB(Tuple<string, MagickImage> pathImage, UpscaleResult result, int tileIndex, string resultSuffix, ref List<FileInfo> tileFilesToDelete)
        {
            Image imageNextTileR, imageNextTileG, imageNextTileB;
            FileInfo tileR, tileG, tileB;
            if (OutputDestinationMode == 1)
            {
                tileR = new FileInfo($"{ResultsPath + result.BasePath.Replace("_ChannelChar", "_R")}_R_tile-{tileIndex:D2}{resultSuffix}.png");
                tileG = new FileInfo($"{ResultsPath + result.BasePath.Replace("_ChannelChar", "_G")}_G_tile-{tileIndex:D2}{resultSuffix}.png");
                tileB = new FileInfo($"{ResultsPath + result.BasePath.Replace("_ChannelChar", "_B")}_B_tile-{tileIndex:D2}{resultSuffix}.png");
            }
            else
            {
                tileR = new FileInfo($"{ResultsPath + result.BasePath}_R_tile-{tileIndex:D2}{resultSuffix}.png");
                tileG = new FileInfo($"{ResultsPath + result.BasePath}_G_tile-{tileIndex:D2}{resultSuffix}.png");
                tileB = new FileInfo($"{ResultsPath + result.BasePath}_B_tile-{tileIndex:D2}{resultSuffix}.png");
            }
            if (InMemoryMode)
            {
                var hrTiles = hrDict[pathImage.Item1];
                imageNextTileR = ImageOperations.ConvertToVips(hrTiles[tileR.FullName])[0];
                imageNextTileG = ImageOperations.ConvertToVips(hrTiles[tileG.FullName])[0];
                imageNextTileB = ImageOperations.ConvertToVips(hrTiles[tileB.FullName])[0];
            }
            else
            {
                imageNextTileR = Image.NewFromFile(tileR.FullName, false, Enums.Access.Sequential)[0];
                tileFilesToDelete.Add(tileR);
                imageNextTileG = Image.NewFromFile(tileG.FullName, false, Enums.Access.Sequential)[0];
                tileFilesToDelete.Add(tileG);
                imageNextTileB = Image.NewFromFile(tileB.FullName, false, Enums.Access.Sequential)[0];
                tileFilesToDelete.Add(tileB);
            }
            return imageNextTileR.Bandjoin(new Image[] { imageNextTileG, imageNextTileB }).Copy(interpretation: "srgb").Cast("uchar"); ;
        }

        void UseGlobalbalance(ref Image imageResult, ref bool cancelGlobalbalance, string filename)
        {
            try
            {
                //Image hist = imageResult.HistFind();
                //bool histIsmonotonic = hist.HistIsmonotonic();                
                //if (!cancelGlobalbalance && histIsmonotonic)
                //{
                //    cancelGlobalbalance = true;
                //    WriteToLogsThreadSafe($"{filename} is monotonic, globalbalance is canceled", Color.LightYellow);
                //}

                if (!cancelGlobalbalance)
                {
                    //Image tempImage = imageResult.CopyMemory();
                    ////imageResult = tempImage.Globalbalance().Copy(interpretation: "srgb").Cast("uchar");
                    //tempImage = tempImage.Globalbalance();
                    //imageResult = tempImage.CopyMemory();
                    imageResult = imageResult.Globalbalance();
                }
            }
            catch (Exception ex)
            {
                cancelGlobalbalance = true;
                if (ex.HResult == -2146233088)
                    Logger.Write($"{filename}: globabalance is canceled", Color.LightYellow);
                else
                    Logger.Write($"{filename}: {ex.Message}", Color.Red);
            }
        }
               
        #region IM MERGE
        void JoinTilesNew(ref MagickImage imageRow, MagickImage imageNextTile, string direction, int dx, int dy)
        {
            Logger.WriteDebug("Merging with IM method");
            MagickImage expandedImage = new MagickImage(imageRow);
            int overlap = expandedImage.Width + dx;
            Bitmap mask;
            Rectangle brushSize, gradientRectangle;
            LinearGradientMode brushDirection;
            Gravity tileG = Gravity.East;
            Gravity rowG = Gravity.West;
            int resultW = imageRow.Width, resultH = imageRow.Height;
            int offset = 1;
            if (direction == Enums.Direction.Horizontal)
            {
                overlap = expandedImage.Width + dx;
                brushSize = new Rectangle(-dx, -dy, overlap, imageRow.Height);
                brushDirection = LinearGradientMode.Horizontal;
                gradientRectangle = new Rectangle(-dx + offset, -dy, overlap - offset, imageNextTile.Height);
                resultW += imageNextTile.Width - overlap;
            }
            else
            {
                overlap = expandedImage.Height + dy;
                brushSize = new Rectangle(-dx, -dy, imageRow.Width, overlap);
                brushDirection = LinearGradientMode.Vertical;
                gradientRectangle = new Rectangle(-dx, -dy, imageRow.Width, overlap);
                resultH += imageNextTile.Height - overlap;
                tileG = Gravity.South;
                rowG = Gravity.North;
            }
            mask = new Bitmap(imageRow.Width, imageRow.Height);
            using (Graphics graphics = Graphics.FromImage(mask))
            using (LinearGradientBrush brush = new LinearGradientBrush(brushSize, Color.White, Color.Black, brushDirection))
            {
                graphics.FillRectangle(Brushes.White, 0, 0, imageRow.Width, imageRow.Height);
                graphics.FillRectangle(brush, gradientRectangle);
            }
            mask.Save(@"S:\ESRGAN-master\IEU_preview\mask1.png");

            var buffer = ImageOperations.ImageToByte(mask);
            MagickImage alphaMask = new MagickImage(buffer);
            //alphaMask.Write(@"S:\ESRGAN-master\IEU_preview\alpha_test.png");

            //var readSettings = new MagickReadSettings()
            //{
            //    Width = imageRow.Width,
            //    Height = imageRow.Height
            //};
            //readSettings.SetDefine("gradient:direction", "east");
            //readSettings.SetDefine("gradient:vector", $"{-dx},{0}, {-dx + 16},{0}");
            //var image = new MagickImage("gradient:black-white", readSettings);           

            using (MagickImageCollection images = new MagickImageCollection())
            {
                images.Add(new MagickImage(alphaMask));
                images.Add(new MagickImage(alphaMask));
                images.Add(new MagickImage(alphaMask));
                images.Add(new MagickImage(alphaMask));
                alphaMask = (MagickImage)images.Combine();
            };
            expandedImage.HasAlpha = true;
            expandedImage.Composite(alphaMask, CompositeOperator.CopyAlpha);

            //buffer = ImageOperations.ImageToByte2(new Bitmap(resultW, resultH));
            MagickImage result = new MagickImage(MagickColor.FromRgb(0, 0, 0), resultW, resultH);

            result.Composite(imageNextTile, tileG);
            result.Composite(expandedImage, rowG, CompositeOperator.Over);
            imageRow = new MagickImage(result);
        }
        MagickImage MergeTilesNew(Tuple<string, MagickImage> pathImage, int[] tiles, int[] tileSize, string basePath, string basePathAlpha, string resultSuffix,
                                                                                              List<FileInfo> tileFilesToDelete, bool imageHasAlpha, Profile HotProfile)
        {
            bool alphaReadError = false;
            MagickImage imageResult = null, imageAlphaResult = null;
            FileInfo file = new FileInfo(pathImage.Item1);
            MagickImage imageAlphaRow = null;
            int tileWidth = tileSize[0], tileHeight = tileSize[1];

            Dictionary<string, MagickImage> hrTiles = null;
            if (InMemoryMode)
            {
                hrTiles = hrDict[pathImage.Item1];
            }

            for (int i = 0; i < tiles[1]; i++)
            {
                MagickImage imageRow = null;

                for (int j = 0; j < tiles[0]; j++)
                {
                    int tileIndex = i * tiles[0] + j;

                    MagickImage imageNextTile = null, imageAlphaNextTile;
                    try
                    {
                        if (HotProfile.SplitRGB)
                            Logger.Write("RGB split is unsupported with IM merge, sorry!");
                        //imageNextTile = JoinRGB(pathImage, basePath, Path.GetFileNameWithoutExtension(file.Name), tileIndex, resultSuffix, tileFilesToDelete);
                        else
                        {
                            string newTilePath = $"{ResultsPath + basePath}_tile-{tileIndex:D2}{resultSuffix}.png";
                            if (InMemoryMode)
                                imageNextTile = hrTiles[newTilePath];
                            else
                            {
                                imageNextTile = new MagickImage(newTilePath);
                                tileFilesToDelete.Add(new FileInfo($"{ResultsPath + basePath}_tile-{tileIndex:D2}{resultSuffix}.png"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteOpenError(file, ex.Message);
                        return null;
                    }

                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
                    {
                        try
                        {
                            if (HotProfile.UseFilterForAlpha)
                            {
                                MagickImage image = new MagickImage(pathImage.Item2);
                                MagickImage inputImageAlpha = (MagickImage)image.Separate(Channels.Alpha).First();

                                int inputTileWidth = image.Width / tiles[0];
                                int upscaleMod = tileWidth / inputTileWidth;
                                Logger.Write($"Upscaling alpha x{upscaleMod} with {HotProfile.AlphaFilterType} filter", Color.LightBlue);
                                if (upscaleMod != 1)
                                    imageAlphaResult = ImageOperations.ResizeImage(inputImageAlpha, upscaleMod, (FilterType)HotProfile.AlphaFilterType);
                                else
                                    imageAlphaResult = inputImageAlpha;
                                alphaReadError = true;
                            }
                            else
                            {
                                var newAlphaTilePath = $"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{resultSuffix}.png";

                                if (InMemoryMode)
                                    imageAlphaNextTile = hrTiles[newAlphaTilePath];
                                else
                                {
                                    imageAlphaNextTile = new MagickImage(newAlphaTilePath);
                                    tileFilesToDelete.Add(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{resultSuffix}.png"));
                                }

                                if (j == 0)
                                {
                                    imageAlphaRow = imageAlphaNextTile;
                                }
                                else
                                {
                                    JoinTilesNew(ref imageAlphaRow, imageAlphaNextTile, Enums.Direction.Horizontal, -tileWidth * j, 0);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            alphaReadError = true;
                            if (!HotProfile.IgnoreSingleColorAlphas)
                                Logger.WriteOpenError(new FileInfo($"{ResultsPath + basePathAlpha}_alpha_tile-{tileIndex:D2}{resultSuffix}.png"), ex.Message);
                        }
                    }

                    if (j == 0)
                    {
                        imageRow = imageNextTile;
                        continue;
                    }
                    else
                        JoinTilesNew(ref imageRow, imageNextTile, Enums.Direction.Horizontal, -tileWidth * j, 0);

                    imageNextTile.Dispose();
                }

                if (i == 0)
                {
                    imageResult = imageRow;
                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
                        imageAlphaResult = imageAlphaRow;
                }
                else
                {
                    JoinTilesNew(ref imageResult, imageRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                    imageRow.Dispose();

                    if (imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError)
                    {
                        JoinTilesNew(ref imageAlphaResult, imageAlphaRow, Enums.Direction.Vertical, 0, -tileHeight * i);
                    }
                }
                GC.Collect();
            }
            bool alphaIsUpscaledWithFilter = imageAlphaResult != null && imageAlphaResult.Width == imageResult.Width && imageAlphaResult.Height == imageResult.Height;
            if ((imageHasAlpha && !HotProfile.IgnoreAlpha && !alphaReadError) || alphaIsUpscaledWithFilter)
            {
                imageResult.Composite(imageAlphaResult, CompositeOperator.CopyAlpha);
                imageAlphaResult.Dispose();
            }
            return imageResult;
        }
        #endregion        
    }
}
