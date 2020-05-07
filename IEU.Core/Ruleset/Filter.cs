using DdsFileTypePlus;
using ImageMagick;
using PaintDotNet;
using ProtoBuf;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    public class Filter: ReactiveObject
    {
        [ProtoMember(1)]
        public string Name { get; set; }
        [ProtoMember(2)]
        public string Description;    

        private bool _filenameContainsEnabled = false;
        [ProtoMember(3)]
        public bool FilenameContainsEnabled
        {
            get => _filenameContainsEnabled;
            set => this.RaiseAndSetIfChanged(ref _filenameContainsEnabled, value);
        }
        private bool _filenameNotContainsEnabled = false;
        [ProtoMember(4)]
        public bool FilenameNotContainsEnabled
        {
            get => _filenameNotContainsEnabled;
            set => this.RaiseAndSetIfChanged(ref _filenameNotContainsEnabled, value);
        }
        private bool _filenameCaseSensitive = false;
        [ProtoMember(5)]
        public bool FilenameCaseSensitive
        {
            get => _filenameCaseSensitive;
            set => this.RaiseAndSetIfChanged(ref _filenameCaseSensitive, value);
        }
        private string _filenameContainsPattern = "";
        [ProtoMember(6)]
        public string FilenameContainsPattern
        {
            get => _filenameContainsPattern;
            set => this.RaiseAndSetIfChanged(ref _filenameContainsPattern, value);
        }
        private string _filenameNotContainsPattern = "";
        [ProtoMember(7)]
        public string FilenameNotContainsPattern
        {
            get => _filenameNotContainsPattern;
            set => this.RaiseAndSetIfChanged(ref _filenameNotContainsPattern, value);
        }

        public static List<string> FiltersAlpha = new List<string>() { "None", "Contains alpha", "Doesn't contain alpha" };
        
        private int _alpha = 0;
        [ProtoMember(8)]
        public int Alpha
        {
            get => _alpha;
            set => this.RaiseAndSetIfChanged(ref _alpha, value);
        }
        private bool _imageResolutionEnabled = false;
        [ProtoMember(9)]
        public bool ImageResolutionEnabled
        {
            get => _imageResolutionEnabled;
            set => this.RaiseAndSetIfChanged(ref _imageResolutionEnabled, value);
        }
        private bool _imageResolutionOr = true;
        [ProtoMember(10, IsRequired = true)]
        public bool ImageResolutionOr
        {
            get => _imageResolutionOr;
            set => this.RaiseAndSetIfChanged(ref _imageResolutionOr, value);
        }
        private int _imageResolutionMaxWidth = 4096;
        [ProtoMember(11)]
        public int ImageResolutionMaxWidth
        {
            get => _imageResolutionMaxWidth;
            set => this.RaiseAndSetIfChanged(ref _imageResolutionMaxWidth, value);
        }
        private int _imageResolutionMaxHeight = 4096;
        [ProtoMember(12)]
        public int ImageResolutionMaxHeight
        {
            get => _imageResolutionMaxHeight;
            set => this.RaiseAndSetIfChanged(ref _imageResolutionMaxHeight, value);
        }
        
        public static readonly List<string> ExtensionsList = new List<string>() {
            ".PNG", ".TGA", ".DDS", ".JPG",
            ".JPEG", ".BMP", ".TIFF", ".WEBP"};

        [ProtoMember(13)]
        public List<string> SelectedExtensionsList { get; set; } = new List<string>();


        private bool _folderNameContainsEnabled = false;
        [ProtoMember(14)]
        public bool FolderNameContainsEnabled
        {
            get => _folderNameContainsEnabled;
            set => this.RaiseAndSetIfChanged(ref _folderNameContainsEnabled, value);
        }
        private bool _folderNameNotContainsEnabled = false;
        [ProtoMember(15)]
        public bool FolderNameNotContainsEnabled
        {
            get => _folderNameNotContainsEnabled;
            set => this.RaiseAndSetIfChanged(ref _folderNameNotContainsEnabled, value);
        }
        private string _folderNameContainsPattern = "";
        [ProtoMember(16)]
        public string FolderNameContainsPattern
        {
            get => _folderNameContainsPattern;
            set => this.RaiseAndSetIfChanged(ref _folderNameContainsPattern, value);
        }
        private string _folderNameNotContainsPattern = "";
        [ProtoMember(17)]
        public string FolderNameNotContainsPattern
        {
            get => _folderNameNotContainsPattern;
            set => this.RaiseAndSetIfChanged(ref _folderNameNotContainsPattern, value);
        }
        private bool _folderNameCaseSensitive = false;
        [ProtoMember(18)]
        public bool FolderNameCaseSensitive
        {
            get => _folderNameCaseSensitive;
            set => this.RaiseAndSetIfChanged(ref _folderNameCaseSensitive, value);
        }

        public Filter(string name)
        {
            Name = name;
        }

        public Filter() {}

        public bool ApplyFilter(FileInfo file)
        {
            bool alphaFilter = true, filenameFilter = true, sizeFilter, resultsFilter = true;
            string[] patternsFilenameContains = FilenameContainsPattern.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            string[] patternsFilenameNotContains = FilenameNotContainsPattern.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

            string[] patternsFoldernameContains = FolderNameContainsPattern.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            string[] patternsFoldernameNotContains = FolderNameNotContainsPattern.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

            string filename = file.Name;
            if (!FilenameCaseSensitive)
            {
                filename = filename.ToUpper();
                for (int i = 0; i < patternsFilenameContains.Length; i++)
                    patternsFilenameContains[i] = patternsFilenameContains[i].ToUpper();
                for (int i = 0; i < patternsFilenameNotContains.Length; i++)
                    patternsFilenameNotContains[i] = patternsFilenameNotContains[i].ToUpper();
            }
            if (FilenameContainsEnabled)
            {
                bool matchPattern = false;
                foreach (string pattern in patternsFilenameContains)
                    matchPattern = matchPattern || filename.Contains(pattern);
                filenameFilter = filenameFilter && matchPattern;
                if (!filenameFilter) return false;
            }
            if (FilenameNotContainsEnabled)
            {
                bool matchPattern = true;
                foreach (string pattern in patternsFilenameNotContains)
                    matchPattern = matchPattern && !filename.Contains(pattern);
                if (!matchPattern) return false;
            }

            string folderName = file.DirectoryName;

            if (!FolderNameCaseSensitive)
            {
                folderName = folderName.ToUpper();
                for (int i = 0; i < patternsFoldernameContains.Length; i++)
                    patternsFoldernameContains[i] = patternsFoldernameContains[i].ToUpper();
                for (int i = 0; i < patternsFoldernameNotContains.Length; i++)
                    patternsFoldernameNotContains[i] = patternsFoldernameNotContains[i].ToUpper();
            }
            if (FolderNameContainsEnabled)
            {
                bool folderNameFilter = true;
                bool matchPattern = false;
                foreach (string pattern in patternsFoldernameContains)
                    matchPattern = matchPattern || folderName.Contains(pattern);
                folderNameFilter = folderNameFilter && matchPattern;
                if (!folderNameFilter) return false;
            }
            if (FolderNameNotContainsEnabled)
            {
                bool matchPattern = true;
                foreach (string pattern in patternsFoldernameNotContains)
                    matchPattern = matchPattern && !folderName.Contains(pattern);
                if (!matchPattern) return false;
            }

            if (SelectedExtensionsList?.Count > 0 &&
                !SelectedExtensionsList.Contains(file.Extension.ToUpper()))
                return false;

            if (Alpha != 0 || ImageResolutionEnabled) //need to load Magick image
            {
                int imgWidth, imgHeight;

                if (file.Extension.ToLower() == ".dds")
                {
                    using (Surface image = DdsFile.Load(file.FullName))
                    {
                        if (Alpha != 0)
                        {
                            switch (Alpha) // switch alpha filter type
                            {
                                case 1:
                                    alphaFilter = DdsFile.HasTransparency(image);
                                    break;
                                case 2:
                                    alphaFilter = !DdsFile.HasTransparency(image);
                                    break;
                            }
                            if (!alphaFilter) return false;
                        }
                        imgWidth = image.Width;
                        imgHeight = image.Height;
                    }

                }
                else
                {
                    using (MagickImage image = new MagickImage(file.FullName))
                    {
                        if (Alpha != 0)
                        {
                            switch (Alpha) // switch alpha filter type
                            {
                                case 1:
                                    alphaFilter = image.HasAlpha;
                                    break;
                                case 2:
                                    alphaFilter = !image.HasAlpha;
                                    break;
                            }

                            if (!alphaFilter) return false;
                        }
                        imgWidth = image.Width;
                        imgHeight = image.Height;
                    }
                }

                if (ImageResolutionEnabled)
                {
                    if (!ImageResolutionOr) // OR
                        sizeFilter = imgWidth <= ImageResolutionMaxWidth || imgHeight <= ImageResolutionMaxHeight;
                    else // AND
                        sizeFilter = imgWidth <= ImageResolutionMaxWidth && imgHeight <= ImageResolutionMaxHeight;
                    if (!sizeFilter) return false;
                }
            }
            return true;
        }

        public Filter Clone()
        {
            MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, this);
            stream.Position = 0;
            Filter clone = Serializer.Deserialize<Filter>(stream);
            stream.Close();
            return clone;
        }
    }    
}

