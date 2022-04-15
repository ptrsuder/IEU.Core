using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetVips;

namespace ImageEnhancingUtility.Core
{
    public class histMatchTest
    {
        public static void MatchHist()
        {
            Image input = Image.NewFromFile(@"S:\ESRGAN-master\IEU_histmatch\index2.tif");
            //input = input + new int[] { 0, 128, 128 };
            //input = input * new double[] { 255.0 / 100, 1, 1 };            
            input = input.Cast("uchar");
            Image reference = Image.NewFromFile(@"S:\ESRGAN-master\IEU_histmatch\index.tif");

            var inHist = input.HistFind();
            //inHist.WriteToFile(@"S:\ESRGAN-master\IEU_histmatch\inHist.png");
            inHist = inHist.HistCum().HistNorm();
            //inHist.WriteToFile(@"S:\ESRGAN-master\IEU_histmatch\inHistCum.png");
            var refHist = reference.HistFind().HistCum().HistNorm();

            var lut = inHist.HistMatch(refHist);
            //lut.WriteToFile(@"S:\ESRGAN-master\IEU_histmatch\lut.png");

            var result = input.Maplut(lut);
            result.WriteToFile(@"S:\ESRGAN-master\IEU_histmatch\result.png");
        }
    }
}
