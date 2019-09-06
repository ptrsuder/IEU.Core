using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace ImageEnhancingUtility
{
    public static class EmbeddedResource
    {
        public static string GetFileText(string namespaceAndFileName)
        {
            try
            {
                using (var stream = typeof(EmbeddedResource).GetTypeInfo().Assembly.GetManifestResourceStream(namespaceAndFileName))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch (Exception exception)
            {
                throw new Exception($"Failed to read Embedded Resource {namespaceAndFileName}");
            }
        }

        //public static WindowIcon GetIcon()
        //{
        //    try
        //    {
        //        using (var stream = typeof(EmbeddedResource).GetTypeInfo().Assembly.GetManifestResourceStream("CropUpscaleMergeAvalonia.icon.ico"))
        //            return new WindowIcon(new Avalonia.Media.Imaging.Bitmap(stream));
        //    }
        //    catch (Exception exception)
        //    {
        //        throw new Exception($"Failed to load icon");
        //    }
        //}
    }
}
