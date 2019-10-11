namespace ImageEnhancingUtility
{
    public enum DownscaleType
    {
        Nearest = 0,
        Linear = 1,
        Cubic = 2,
        Area = 3,
        Lancoz = 4,
        LinearExact = 5
    }

    public enum NoiseType
    {
        Jpeg,
        Gaussian,
        Quantize,
        Poisson,
        Dither,
        Speckle,
        SaltPepper,
        Clean
    }

    public enum OverwriteMode
    {
        None,
        OverwriteTiles,
        OverwriteOriginal
    }
    public enum OutputMode
    {
        Default,
        FolderPerImage,
        FolderPerModel,
        FolderStructure
    }

    public enum TiffCompression
    {
        None = 1,
        Jpeg = 2,
        Deflate = 3,
        LZW = 4
    }
    public enum WebpPreset
    {
        Default,
        Picture,
        Photo,
        Drawing,
        Icon,
        Text
    }
}
