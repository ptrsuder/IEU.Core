using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ImageEnhancingUtility.Core;
using Path = System.IO.Path;

namespace ImageEnhancingUtility
{
    public static class Helper
    {
        public static string GetApplicationRoot()
        {
            var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);

            Regex appPathMatcher = new Regex($@"(?<!fil)[A-Za-z]:{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}+[\S\s]*");
            var appRoot = appPathMatcher.Match(exePath).Value;
            return appRoot;
        }

        public static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = url.Replace("&", "^&"),
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    //Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        public static void Exec(string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            Process process;
            using (var proc = new Process())
            {
                proc.StartInfo = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\""
                };
                process = proc;
            }; 

            process.Start();
            process.WaitForExit();

            Exec("chmod 644 /path/to/file.txt");
        }        

        public static int[] GetGoodDimensions(int width, int height, int x, int y)
        {
            if (width % x != 0)
                width += (x - width % x);

            if (height % y != 0)
                height += (y - height % y);

            return new int[] { width, height };
        }

        public static int[] GetTilesSize_(int width, int height, int maxTileResolution)
        {
            int tilesHeight = 1, tilesWidth = 1;
            while ((height / tilesHeight) * (width / tilesWidth) > maxTileResolution)
            {
                int oldTilesHeight = tilesHeight, oldTilesWidth = tilesWidth;
                if (height/tilesHeight >= width/tilesWidth)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        tilesHeight++;
                        if (height % tilesHeight == 0)                                                    
                            break;                        
                    }
                    if (oldTilesHeight == tilesHeight)
                    {                      
                        tilesHeight = oldTilesHeight + 1;
                    }
                    continue;
                }
                else
                {
                    for (int i = 0; i < 5; i++)
                    {
                        tilesWidth++;
                        if (width % tilesWidth == 0)
                            break;
                    }
                    if (oldTilesWidth == tilesWidth)
                    {                       
                        tilesWidth = oldTilesWidth + 1;
                    }
                    continue;
                }
            }
            return new int[] { tilesWidth, tilesHeight };
        }

        public static int[] GetTilesSize(int width, int height, int maxTileResolution)
        {
            //width = 4096; height = 4096; maxTileResolution = 512 * 256;
            int[] tiles_A = coverRectangleWithTiles(width, height, maxTileResolution);
            int[] tiles_B = GetTilesSize_(width, height, maxTileResolution);
            int maxError = 50;
            int tilesHeight = 1, tilesWidth = 1;
            while ((height / tilesHeight) * (width / tilesWidth) - maxError > maxTileResolution)
            {
                int oldTilesHeight = tilesHeight, oldTilesWidth = tilesWidth;
                if (height / tilesHeight >= width / tilesWidth)
                {
                    tilesHeight*=2;
                }
                else
                {
                    tilesWidth*=2;
                }
            }
            return new int[] { tilesWidth, tilesHeight };
        }

        public static int[] GetTilesSize(int width, int height, int tileWidth, int tileHeight)
        {            
            int tilesWidth = width / tileWidth;
            int tilesHeight = height / tileHeight;
            return new int[] { tilesWidth, tilesHeight };
        }

        public static void RenameModelFile(ModelInfo model, int scaleSize)
        {
            string newName = $"{scaleSize}x_" + model.Name;
            string newFullname = model.FullName.Replace(model.Name, newName);
            File.Move(model.FullName, newFullname);
            model.FullName = newFullname;
            model.Name = newName;           
        }

        public static KeyValue<bool, double>  CheckAlphaValue(string value)
        {
            double alpha = 0.0;
            KeyValue<bool, double> failResult = new KeyValue<bool, double>(false, 0.0);
            try
            {
                alpha = double.Parse(value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
                if (alpha <= 0.0 || alpha >= 1.0)               
                    return failResult;                
            }
            catch
            {
                return failResult;
            }
            return new KeyValue<bool, double>(true, alpha);
        }

        static int[] coverRectangleWithTiles(int rectWidth, int rectHeight, int maxArea)
        {
            int maxWidth = rectWidth;
            int minWidth = maxArea / maxWidth;
            // How many columns of tiles do we need at least?
            var minColumns = (int)Math.Ceiling((double)rectWidth / maxWidth);
            // ...and how many at most?
            var maxColumns = (int)Math.Floor((double)rectWidth / minWidth);

            // First see what how well we can do with minimum-width tiles:
            var columns = maxColumns;
            int rows = (int)Math.Ceiling((double)rectHeight / maxWidth);
            // Lowest total found so far; we'll try to improve this below:
            var minTiles = (int)columns * rows;

            // Now try it with wider tiles; we only need to try the minimum
            // tile width for each possible number of columns.  The case
            // columns = maxColumns is already tried above, so we can exclude
            // it (and should, because otherwise the formulas below might
            // give tileWidth < minWidth, and thus tileHeight > maxWidth):

            int tileCols=0, tileRows=0;
            for (int col = minColumns; col < maxColumns; col++)
            {
                // Find the minimum tile width for this number of columns:
                var tileWidth = rectWidth / col;
                // ...the maximum height allowed for this width:
                var tileHeight = maxArea / tileWidth;
                // ...and the number of rows needed with this height:
                var rows2 = (int)Math.Ceiling((double)rectHeight / tileHeight);

                // Multiply columns with rows to get total number of tiles:
                var tiles = col * rows2;
                // ...and save it if it's smaller than the minimum so far:
                if (tiles < minTiles)
                {
                    minTiles = tiles;
                    tileCols = col;
                    tileRows = rows2;
                }
                // Could also save the tile dimensions needed to obtain
                // the minimum here.  
                
            }
            return new int[] { tileCols, tileRows };
        }

        public static string GetCondaEnv(bool UseCondaEnv, string CondaEnv)
        {
            if (UseCondaEnv && CondaEnv != "")
                return $" & conda activate {CondaEnv}";
            else
                return "";
        }

    }

}
