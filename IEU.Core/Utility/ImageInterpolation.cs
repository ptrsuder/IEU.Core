using System;
using System.Drawing;
using ImageMagick;

namespace ImageEnhancingUtility.Core
{
    public class ImageInterpolation
    {       
        public static Tuple<bool, string> Interpolate(string pathA, string pathB, string destinationPath, double alpha)
        {
            MagickImage imageA, imageB;
            try
            {
                imageA = new MagickImage(pathA);
                imageB = new MagickImage(pathB);
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
                imageA = Helper.ConvertToMagickImage(a as Bitmap);
                imageB = Helper.ConvertToMagickImage(b as Bitmap);
               
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
                imageA.Write(destinationPath, MagickFormat.Png24);
            }
            catch (Exception ex)
            {
                return new Tuple<bool, string>(false, ex.Message);
            }
            return new Tuple<bool, string>(true, "Success");
        }

    }
}
