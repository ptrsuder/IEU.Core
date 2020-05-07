using System;
using System.Drawing;
using ImageMagick;
using ImageEnhancingUtility.Core.Utility;
using System.IO;

namespace ImageEnhancingUtility.Core
{
    public class ImageInterpolation
    {       
        public static Tuple<bool, string> Interpolate(string pathA, string pathB, string destinationPath, double alpha)
        {
            MagickImage imageA, imageB;
            try
            {
                imageA = ImageOperations.LoadImage(new FileInfo(pathA));
                imageB = ImageOperations.LoadImage(new FileInfo(pathB));
            }
            catch(Exception ex)
            {
                return new Tuple<bool, string>(false, ex.Message);
            }
            return Interpolate(imageA, imageB, destinationPath, alpha);

        }

        public static Tuple<bool, string> Interpolate(Image a, Image b, string destinationPath, double alpha)
        {
            MagickImage imageA, imageB;
            try
            {
                //TODO: pass image format somehow
                imageA = ImageOperations.ConvertToMagickImage(a as Bitmap);
                imageB = ImageOperations.ConvertToMagickImage(b as Bitmap);
               
            }
            catch (Exception ex)
            {
                return new Tuple<bool, string>(false, ex.Message);
            }
            return Interpolate(imageA, imageB, destinationPath, alpha);
        }

        static Tuple<bool, string> Interpolate(MagickImage imageA, MagickImage imageB, string destinationPath, double alpha)
        {            
            if (alpha >= 1 || alpha <=0)
            {
                return new Tuple<bool, string>(false, "Alpha value must be less than 1.0 and more than 0.0");
            }               
            try
            {                
                imageB.HasAlpha = true;
                imageB.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, alpha);
                imageA.Composite(imageB, CompositeOperator.Over);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    imageA.Write(memoryStream, MagickFormat.Png32);
                    ImageOptimizer optimizer = new ImageOptimizer();
                    optimizer.OptimalCompression = true;
                    memoryStream.Position = 0;
                    optimizer.LosslessCompress(memoryStream);                   
                    FileStream fileStream = new FileStream(destinationPath, FileMode.Create);
                    memoryStream.WriteTo(fileStream);
                }
                //imageA.Write(destinationPath, MagickFormat.Png24);
               
            }
            catch (Exception ex)
            {
                return new Tuple<bool, string>(false, ex.Message);
            }
            return new Tuple<bool, string>(true, "Success");
        }

        public static MagickImage Interpolate(string pathA, string pathB, double alpha)
        {
            MagickImage imageA, imageB;
            try
            {
                imageA = ImageOperations.LoadImage(new FileInfo(pathA));
                imageB = ImageOperations.LoadImage(new FileInfo(pathB));
            }
            catch (Exception ex)
            {
                return null;
            }
            return Interpolate(imageA, imageB, alpha);

        }

        static MagickImage Interpolate(MagickImage imageA, MagickImage imageB, double alpha)
        {
            if (alpha >= 1 || alpha <= 0)
            {
                return null;
            }
            try
            {
                imageB.HasAlpha = true;
                imageB.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, alpha);
                imageA.Composite(imageB, CompositeOperator.Over);
                MagickImage result;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    imageA.Write(memoryStream, MagickFormat.Png32);
                    ImageOptimizer optimizer = new ImageOptimizer();
                    //optimizer.OptimalCompression = false;
                    memoryStream.Position = 0;
                    //optimizer.LosslessCompress(memoryStream);
                    //FileStream fileStream = new FileStream(destinationPath, FileMode.Create);
                    //memoryStream.WriteTo(fileStream);
                    result = new MagickImage(memoryStream);
                }
                return result;
            }
            catch (Exception ex)
            {
                return null;
            }            
        }
    }
}
