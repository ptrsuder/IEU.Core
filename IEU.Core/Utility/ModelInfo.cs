namespace ImageEnhancingUtility.Core
{
    public class ModelInfo
    {
        public string Name
        { get; set; }

        public string FullName
        { get; set; }

        public string ParentFolder
        { get; set; }

        public int UpscaleFactor
        { get; set; }

        public ModelInfo(string name, string path)
        {
            Name = name;
            FullName = path;
            ParentFolder = "";
        }

        public ModelInfo(string name, string path, string folder)
        {
            Name = name;
            FullName = path;
            ParentFolder = folder;
        }
    }

}
