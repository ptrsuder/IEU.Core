using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DdsFileTypePlus;
using ImageMagick;
using PaintDotNet;
using Path = System.IO.Path;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using NetVips;
using Image = System.Drawing.Image;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace ImageEnhancingUtility.Core.Utility
{
    public static class ImageOperations
    {
        public static MagickImage ConvertToMagickImage(Surface surface)
        {
            MagickImage result;
            Bitmap bitmap = surface.CreateAliasedBitmap();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                memoryStream.Position = 0;
                result = new MagickImage(memoryStream, new MagickReadSettings() { Format = MagickFormat.Png00 });
            }
            return result;
        }

        public static MagickImage ConvertToMagickImage(Bitmap bitmap, string format = "PNG")
        {
            MagickImage result;
            ImageFormatConverter formatConv = new ImageFormatConverter();            
            ImageFormat imageFormat = (ImageFormat)formatConv.ConvertFromString(format);
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, imageFormat);
                memoryStream.Position = 0;
                result = new MagickImage(memoryStream, new MagickReadSettings() { Format = MagickFormat.Png00 });
            }
            return result;
        }

        public static Surface ConvertToSurface(MagickImage image)
        {
            Bitmap processedBitmap;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                image.Write(memoryStream);
                memoryStream.Position = 0;
                processedBitmap = Image.FromStream(memoryStream) as Bitmap;
            }
            return Surface.CopyFromBitmap(processedBitmap);
        }

        public static Bitmap ConvertToBitmap(MagickImage image)
        {
            Bitmap processedBitmap;
            Bitmap test;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                image.Write(memoryStream, MagickFormat.Png32);
                memoryStream.Position = 0;
                Image temp = Image.FromStream(memoryStream);
                test = (Bitmap)temp.Clone();

            }
            return test;
        }

        public static NetVips.Image ConvertToVips(MagickImage image)
        {
            byte[] imageBuffer = image.ToByteArray(MagickFormat.Png00);        
            return NetVips.Image.NewFromBuffer(imageBuffer);           
        }

        public static NetVips.Image ConvertToVips(string base64String)
        {
            byte[] imageBuffer = Convert.FromBase64String(base64String);
            return NetVips.Image.NewFromBuffer(imageBuffer);
        }
        public static MagickImage LoadImage(FileInfo file)
        {
            MagickImage image;
            if (file.Extension.ToLower() == ".dds" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Surface surface = DdsFile.Load(file.FullName);
                image = ConvertToMagickImage(surface);
                image.HasAlpha = DdsFile.HasTransparency(surface);
            }
            else
                image = new MagickImage(file.FullName);
            return image;
        }

        public static Image LoadImageToBitmap(string fullname)
        {
            string extension = Path.GetExtension(fullname).ToUpper();
            if (!Filter.ExtensionsList.Contains(extension))
                return null;
            Image image = null;
            string[] simpleFormats = new string[] { "*.BMP", ".DIB", ".RLE", ".GIF", ".JPG", ".PNG", ".JPEG" };

            if (simpleFormats.Contains(extension))
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    using (FileStream fs = new FileStream(fullname, FileMode.Open, FileAccess.ReadWrite))
                    {
                        image = Image.FromStream(fs);                       
                    }
                }
               // image = Image.FromFile(fullname);                
            }
            else
            {
                using (var img = LoadImage(new FileInfo(fullname)))
                    image = ConvertToBitmap(img);
            }
            return image;
        }

        public static Bitmap Base64ToBitmap(string base64String)
        {
            Bitmap bmpReturn = null;

            byte[] byteBuffer = Convert.FromBase64String(base64String);
            MemoryStream memoryStream = new MemoryStream(byteBuffer);

            memoryStream.Position = 0;

            bmpReturn = (Bitmap)Bitmap.FromStream(memoryStream);

            memoryStream.Close();
            memoryStream = null;
            byteBuffer = null;

            return bmpReturn;
        }

        public static MagickImage ResizeImage(MagickImage image, double resizeFactor, FilterType filterType)
        {
            MagickImage newImage = image.Clone() as MagickImage;
            //image.VirtualPixelMethod = VirtualPixelMethod.
            //image.Interpolate = interpolateMethod;
            newImage.FilterType = filterType;
            //image.Sharpen();
            //image.Scale(new Percentage(resizeFactor));  
            newImage.Resize((int)(resizeFactor * image.Width), (int)(resizeFactor * image.Height));
            return newImage;
        }

        public static MagickImage ExpandTiledTexture(MagickImage image, int seamlessExpandSize = 16)
        {
            int imageHeight = image.Height, imageWidth = image.Width;
            int expandSize = seamlessExpandSize;
            if (imageHeight <= 32 || imageWidth <= 32)
                expandSize = seamlessExpandSize / 2;

            MagickImage expandedImage = (MagickImage)image.Clone();
            MagickImage bottomEdge = (MagickImage)image.Clone();
            bottomEdge.Crop(new MagickGeometry(0, imageHeight - expandSize, imageWidth, expandSize));
            expandedImage.Page = new MagickGeometry($"+0+{expandSize}");
            bottomEdge.Page = new MagickGeometry("+0+0");
            MagickImageCollection edges = new MagickImageCollection() { bottomEdge, expandedImage };
            expandedImage = (MagickImage)edges.Mosaic();

            MagickImage topEdge = (MagickImage)image.Clone();
            topEdge.Crop(new MagickGeometry(0, 0, imageWidth, expandSize));
            topEdge.Page = new MagickGeometry($"+0+{expandedImage.Height}");
            expandedImage.Page = new MagickGeometry("+0+0");
            edges = new MagickImageCollection() { expandedImage, topEdge };
            expandedImage = (MagickImage)edges.Mosaic();

            image = (MagickImage)expandedImage.Clone();

            MagickImage rightEdge = (MagickImage)image.Clone();
            edges = new MagickImageCollection() { rightEdge, expandedImage };
            rightEdge.Crop(new MagickGeometry(image.Width - expandSize, 0, expandSize, image.Height));
            expandedImage.Page = new MagickGeometry($"+{expandSize}+0");
            rightEdge.Page = new MagickGeometry("+0+0");
            expandedImage = (MagickImage)edges.Mosaic();

            MagickImage leftEdge = (MagickImage)image.Clone();
            edges = new MagickImageCollection() { expandedImage, leftEdge };
            leftEdge.Crop(new MagickGeometry(0, 0, expandSize, image.Height));
            leftEdge.Page = new MagickGeometry($"+{expandedImage.Width}+0");
            expandedImage.Page = new MagickGeometry($"+0+0");
            expandedImage = (MagickImage)edges.Mosaic();

            edges.Dispose();

            return expandedImage;
        }

        public static MagickImage PadImage(MagickImage image, int x, int y)
        {
            MagickImage result = (MagickImage)image.Clone();
            int[] newDimensions = Helper.GetGoodDimensions(image.Width, image.Height, x, y);
            result.Extent(newDimensions[0], newDimensions[1]);
            return result;
        }

        public static void MatrixBlend(string image1path, string image2path, byte alpha)
        {
            // for the matrix the range is 0.0 - 1.0
            float alphaNorm = (float)alpha / 255.0F;
            using (Bitmap image1 = (Bitmap)Bitmap.FromFile(image1path))
            {
                using (Bitmap image2 = (Bitmap)Bitmap.FromFile(image2path))
                {
                    // just change the alpha
                    ColorMatrix matrix = new ColorMatrix(new float[][]{
                new float[] {1F, 0, 0, 0, 0},
                new float[] {0, 1F, 0, 0, 0},
                new float[] {0, 0, 1F, 0, 0},
                new float[] {0, 0, 0, alphaNorm, 0},
                new float[] {0, 0, 0, 0, 1F}});

                    ImageAttributes imageAttributes = new ImageAttributes();
                    imageAttributes.SetColorMatrix(matrix);

                    using (Graphics g = Graphics.FromImage(image1))
                    {
                        g.CompositingMode = CompositingMode.SourceOver;
                        g.CompositingQuality = CompositingQuality.HighQuality;

                        g.DrawImage(image2,
                            new Rectangle(0, 0, image1.Width, image1.Height),
                            0,
                            0,
                            image2.Width,
                            image2.Height,
                            GraphicsUnit.Pixel,
                            imageAttributes);
                    }
                }
            }
        }
    }
}
