using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace ImageEnhancingUtility.Core
{
    public static class opencvTest
    {
        public static void Stitch()
        {
            Mat tile00 = new Mat(@"S:\ESRGAN-master\IEU_preview\opencv\image1.jpg");
            Mat tile01 = new Mat(@"S:\ESRGAN-master\IEU_preview\opencv\image2.jpg");
            Mat mask = new Mat(@"S:\ESRGAN-master\IEU_preview\opencv\mask.png");

            List<Mat> gpA = new List<Mat>();
            List<Mat> gpB = new List<Mat>();
            List<Mat> gpMask = new List<Mat>();

            int num = 4;

            gpA.Add(tile00);
            Mat G = tile00.Clone();
            for (int i = 0; i <= num; i++)
            {
                G = G.PyrDown();
                gpA.Add(G);
            }

            gpMask.Add(mask);
            G = mask.Clone();
            for (int i = 0; i <= num; i++)
            {
                G =G.PyrDown();
                gpMask.Add(G);
            }
            gpMask.Reverse();
            
            gpB.Add(tile01);
            G = tile01.Clone();
            for (int i = 0; i <= num; i++)
            {
                G = G.PyrDown();
                gpB.Add(G);
            }

            List<Mat> lpA = new List<Mat>();
            List<Mat> lpB = new List<Mat>();            

            lpA.Add(gpA[num+1]);
            for (int i = num + 1; i > 0; i--)
            {
                var GE = gpA[i].PyrUp();
                Mat L = new Mat();
                Cv2.Subtract(gpA[i - 1], GE, L);
                lpA.Add(L);
            }

            lpB.Add(gpB[num+1]);
            for (int i = num + 1; i > 0; i--)
            {
                var GE = gpB[i].PyrUp();
                Mat L = new Mat();
                Cv2.Subtract(gpB[i - 1], GE, L);
                lpB.Add(L);
            }

            //Window.ShowImages(lpA.ToArray());
            //Window.ShowImages(lpB.ToArray());

            List<Mat> LS = new List<Mat>();
            
            for (int i = 0; i <= num; i++)
            {
                //ls = lb * mask + la * (1.0 - mask)

                Mat a = new Mat(), b = new Mat(), c = new Mat(), m = new Mat();
                               
                lpA[i].CvtColor(ColorConversionCodes.RGB2GRAY).ConvertTo(a, MatType.CV_64FC1);
                lpB[i].CvtColor(ColorConversionCodes.RGB2GRAY).ConvertTo(b, MatType.CV_64FC1);
                gpMask[i].CvtColor(ColorConversionCodes.RGB2GRAY).ConvertTo(m, MatType.CV_64FC1);
                Mat ones = Mat.Ones(MatType.CV_64FC1, lpB[i].Rows, lpB[i].Cols);
                var test = (ones - m).ToMat();
                
                c = (b * m + a * (ones - m)).ToMat();

                //Cv2.Multiply(lpA[i], gpMask[i], a);      
                //Mat ones = Mat.Ones(MatType.CV_8UC3, lpB[i].Rows, lpB[i].Cols);
                //Cv2.Subtract(ones, gpMask[i], b);
                //Cv2.Multiply(lpB[i], b, b);
                //Cv2.Add(a, b, a);

                LS.Add(c);
            }

            Window.ShowImages(LS[4]);

            var laplacian_top = LS[0];
            var laplacian_lst = new List<Mat> { laplacian_top };

            for (int i = 0; i < num; i++)
            {
                Size size = new Size(LS[i + 1].Cols, LS[i + 1].Rows);
                var laplacian_expanded = laplacian_top.PyrUp(size);
                Cv2.Add(LS[i + 1], laplacian_expanded, laplacian_top);
                laplacian_lst.Add(laplacian_top);
            }

            Window.ShowImages(laplacian_lst[num]);

            /*
            laplacian_top = laplacian_pyr[0]
            laplacian_lst = [laplacian_top]
            num_levels = len(laplacian_pyr) - 1
            for i in range(num_levels):
                size = (laplacian_pyr[i + 1].shape[1], laplacian_pyr[i + 1].shape[0])
                laplacian_expanded = cv2.pyrUp(laplacian_top, dstsize=size)
                laplacian_top = cv2.add(laplacian_pyr[i+1], laplacian_expanded)
                laplacian_lst.append(laplacian_top)
             */

        }
    }
}
