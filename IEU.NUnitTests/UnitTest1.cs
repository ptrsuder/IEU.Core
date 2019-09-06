using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using ImageEnhancingUtility.Core;
using System.Linq;

namespace ImageEnhancingUtility.Tests
{
    [TestFixture]
    public class Tests
    {
        IEU ieu;
        FileInfo testFile1;
        string DirectorySeparator = Path.DirectorySeparatorChar.ToString();

        [SetUp]
        public void Init()
        {
            ieu = new IEU();
            ieu.EsrganPath = @"S:\ESRGAN-master";
            string workingDirectory = Environment.CurrentDirectory;
            string projectDirectory = Directory.GetParent(workingDirectory).Parent.Parent.FullName;
            ieu.InputDirectoryPath = projectDirectory + "\\Test_IEU_input";
            ieu.OutputDirectoryPath = projectDirectory + "\\Test_IEU_output";
            ieu.LrPath = projectDirectory + "\\Test_LR";
            ieu.ResultsPath = projectDirectory + "\\Test_results";
            testFile1 = new FileInfo(ieu.InputDirectoryPath + "\\test_564x432_jpg_noalpha.jpg");
        }

        [Test]
        public async Task SplitSpecificFiles()
        {
            FileInfo testImage = new FileInfo(ieu.InputDirectoryPath + "\\test_564x432_jpg_noalpha.jpg");
            await ieu.Split(new FileInfo[] { testImage, testImage, testImage});
            bool lrHasCorrectTiles = false;
            int[] tiles = Helper.GetTilesSize(564, 432, ieu.MaxTileResolution);
            lrHasCorrectTiles = Directory.GetFiles(ieu.LrPath).Length == tiles[0] * tiles[1];
            Assert.IsTrue(lrHasCorrectTiles);
        }

        [Test]
        public async Task SplitDefault()
        {
            ieu.OutputDestinationMode = 0;
            ieu.MaxTileResolution = 2256 * 864;
            ieu.OverlapSize = 64;
            await ieu.Split();
            Assert.Pass();          
        }

        [Test]
        public async Task MergeSingleResultMulipleTimes()
        {
            DirectoryInfo outputDirectory = new DirectoryInfo(ieu.OutputDirectoryPath);
            outputDirectory.GetFiles("*", SearchOption.TopDirectoryOnly).ToList().ForEach(x => x.Delete());
            List<Task> tasks = new List<Task>();
            FileInfo file = testFile1;
            int resultsNumber = 120;
            string outputFilename = Path.GetFileNameWithoutExtension(file.Name);
            for (int i = 0; i < resultsNumber; i++)
            {
                int temp = i;
                //ieu.MergeTask(file, DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name), 0, outputFilename + $"_{i}");
                tasks.Add(Task.Run(() => ieu.MergeTask(file, DirectorySeparator + Path.GetFileNameWithoutExtension(file.Name), 0, outputFilename + $"_{temp}")));
            }
            await Task.WhenAll(tasks.ToArray());
            tasks.Clear();
            GC.Collect();
            Assert.IsTrue(outputDirectory.GetFiles().Count() == resultsNumber);
        }

        [Test]
        public async Task MergeMultipleResults()
        {
            DirectoryInfo outputDirectory = new DirectoryInfo(ieu.OutputDirectoryPath);
            DirectoryInfo inputDirectory = new DirectoryInfo(ieu.InputDirectoryPath);
            outputDirectory.GetFiles("*", SearchOption.TopDirectoryOnly).ToList().ForEach(x => x.Delete());
            int resultsNumber = inputDirectory.GetFiles().Count();
            ieu.OutputDestinationMode = 0;
            ieu.MaxTileResolution = 2256 * 864;
            ieu.OverlapSize = 64;
            await ieu.Merge();            
            Assert.IsTrue(outputDirectory.GetFiles().Count() == resultsNumber);
        }

        [Test]
        public void TilesSize()
        {
            int width = 512;
            int height = 512;
            int maxTileResolution = 100 * 100;
            int[] tiles = Helper.GetTilesSize(width, height, maxTileResolution);
            bool dimensionsAreOK = width % tiles[0] == 0 && height % tiles[1] == 0;
            if (!dimensionsAreOK)
                Assert.Fail();
            int difference = maxTileResolution - (width / tiles[0]) * (height / tiles[1]);
            TestContext.WriteLine($"Difference is {difference}");
        }
    }
}