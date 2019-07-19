using DdsFileTypePlus;

namespace ImageEnhancingUtility
{
    public class DdsFileFormatSetting
    {
        public string Name { get; set; }
        public DdsFileFormat DdsFileFormat { get; set; }

        public DdsFileFormatSetting(string name, DdsFileFormat fileFormat)
        {
            Name = name;
            DdsFileFormat = fileFormat;
        }
    }
}
