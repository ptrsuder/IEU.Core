using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using ImageMagick;
using System.Reflection;
using PaintDotNet;
using System.Runtime.InteropServices;
using Color = System.Drawing.Color;

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

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\""
                }
            };

            process.Start();
            process.WaitForExit();

            Exec("chmod 644 /path/to/file.txt");
        }

        public static MagickImage ConvertToMagickImage(Surface surface)
        {
            MagickImage result;
            System.Drawing.Bitmap bitmap = surface.CreateAliasedBitmap();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                memoryStream.Position = 0;
                result = new MagickImage(memoryStream, new MagickReadSettings() { Format = MagickFormat.Png00 });
            }
            return result;
        }

        public static Surface ConvertToSurface(MagickImage image)
        {
            System.Drawing.Bitmap processedBitmap;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                image.Write(memoryStream);
                memoryStream.Position = 0;
                processedBitmap = System.Drawing.Image.FromStream(memoryStream) as System.Drawing.Bitmap;
            }
            return Surface.CopyFromBitmap(processedBitmap);
        }

        public static int[] GetTilesSize(int width, int height, int maxTileResolution)
        {
            int tilesHeight = 1, tilesWidth = 1;
            while ((height / tilesHeight) * (width / tilesWidth) > maxTileResolution)
            {
                int oldTilesHeight = tilesHeight, oldTilesWidth = tilesWidth;
                if (tilesHeight <= tilesWidth)
                {
                    for (int i = 2; i < 4; i++)
                    {
                        if (height % i == 0)
                        {
                            tilesHeight *= i;
                            break;
                        }
                    }
                    if (tilesHeight == oldTilesHeight)
                        tilesHeight++;
                    continue;
                }
                else
                {
                    for (int i = 2; i < 4; i++)
                    {
                        if (width % i == 0)
                        {
                            tilesWidth *= i;
                            break;
                        }
                    }
                    if (tilesWidth == oldTilesWidth)
                        tilesWidth++;
                    continue;
                }
            }
            return new int[] { tilesWidth, tilesHeight };
        }
    }

    public class LogMessage
    {
        public static string Text { get; internal set; }
        public static Color Color { get; internal set; }

        public LogMessage(string text, Color color)
        {
            Text = text; Color = color;
        }
        public LogMessage(string text)
        {
            Text = text; Color = Color.White;
        }

    }
}
