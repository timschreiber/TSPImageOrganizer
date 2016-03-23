using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TPS.ImageOrganizer
{
    public static class Extensions
    {
        public static IEnumerable<FileInfo> GetFilesByExtensions(this DirectoryInfo dir, params string[] extensions)
        {
            if (extensions == null || extensions.Length == 0)
                return dir.GetFiles();
            var files = dir.EnumerateFiles();
            return files.Where(f => extensions.Select(x => x.ToLower()).Contains(f.Extension.ToLower()));
        }
    }
}
