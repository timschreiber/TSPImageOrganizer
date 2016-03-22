using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;

namespace TPS.ImageOrganizer
{
    class Program
    {
        static void Main(string[] args)
        {
            var srcDir = args[0];
            var dstDir = args[1];

            var srcHashDictionary = new Dictionary<string, string>();
            var srcErrDictionary = new Dictionary<string, string>();
            var dstHashDictionary = new Dictionary<string, string>();
            var dstErrDictionary = new Dictionary<string, string>();

            var ts = DateTime.Now;
            Console.Write("Getting Source Image Hashes... ");
            hashDirectory(srcHashDictionary, srcErrDictionary, srcDir);
            Console.WriteLine("{0} ms", (DateTime.Now - ts).TotalMilliseconds);

            ts = DateTime.Now;
            Console.Write("Getting Destination Image Hashes... ");
            hashDirectory(dstHashDictionary, dstErrDictionary, dstDir);
            Console.WriteLine("{0} ms", (DateTime.Now - ts).TotalMilliseconds);

            ts = DateTime.Now;
            Console.Write("Copying Files... ");
            copyFiles(dstDir, srcHashDictionary, srcErrDictionary, dstHashDictionary, dstErrDictionary);
            Console.WriteLine("{0} ms", (DateTime.Now - ts).TotalMilliseconds);

            ts = DateTime.Now;
            Console.Write("Writing Log... ");
            writeLog(dstDir, srcErrDictionary, dstErrDictionary);
            Console.WriteLine("{0} ms", (DateTime.Now - ts).TotalMilliseconds);

            Console.WriteLine("Done.");
            Console.ReadKey();
        }

        static void hashDirectory(IDictionary<string, string> hashDictionary, IDictionary<string, string> errDictionary, string path)
        {
            if (Directory.Exists(path))
            {
                var dir = new DirectoryInfo(path);

                var fileInfos = dir.GetFilesByExtensions(".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff");
                
                Parallel.ForEach(fileInfos, fil =>
                {
                    try
                    {
                        var hash = getHash(fil.FullName);
                        hashDictionary.Add(hash, fil.FullName);
                    }
                    catch (Exception ex)
                    {
                        errDictionary.Add(fil.FullName, ex.Message);
                    }
                });

                var dirInfos = dir.GetDirectories();
                Parallel.ForEach(dirInfos, subDir =>
                {
                    hashDirectory(hashDictionary, errDictionary, subDir.FullName);
                });
            }
        }

        static string getHash(string fileName)
        {
            var hash = readHashFromExif(fileName);
            if (!string.IsNullOrWhiteSpace(hash))
                return hash;

            using(var img = Image.FromFile(fileName))
            using(var ms = new MemoryStream())
            using(var sw = new StreamWriter(ms))
            {
                var bmp = new Bitmap(img);
                for(var x = 0; x < bmp.Width; x++)
                {
                    for(var y = 0; y < bmp.Height; y++)
                    {
                        var pixel = bmp.GetPixel(x, y);
                        sw.Write(BitConverter.GetBytes(pixel.ToArgb()));
                    }
                }
                sw.Flush();
                ms.Seek(0, SeekOrigin.Begin);
                hash = Convert.ToBase64String(SHA256.Create().ComputeHash(ms));
            }
            writeHashToExif(fileName, hash);
            return hash;
        }

        static string readHashFromExif(string fileName)
        {
            PictureMetaInformation instance;
            if (!PictureMetaInformation.TryGet(fileName, out instance))
                return string.Empty;
            return instance.Caption;
        }

        static void writeHashToExif(string fileName, string hash)
        {
            PictureMetaInformation instance;
            if (PictureMetaInformation.TryGet(fileName, out instance))
            {
                instance.Caption = hash;
                instance.Write();
            }
        }

        static void copyFiles(string dstDir, IDictionary<string, string> srcHashDictionary, IDictionary<string, string> srcErrDictionary, IDictionary<string, string> dstHashDictionary, IDictionary<string, string> dstErrDictionary)
        {
            Parallel.ForEach(srcHashDictionary.Keys, hash =>
            {
                var srcFile = srcHashDictionary[hash];
                if (dstHashDictionary.ContainsKey(hash))
                {
                    var dstFile = dstHashDictionary[hash];
                    var msg = string.Format("SKIPPED (Identical image found at {0})", dstFile);
                    srcErrDictionary.Add(srcFile, msg);
                }
                else
                {
                    var imgName = Path.GetFileName(srcFile);
                    var imgDate = getImageDate(srcFile);
                    var imgPath = Path.Combine(dstDir, imgDate.Year.ToString(), imgDate.ToString("yyyy-MM-dd"));
                    var imgPathFile = Path.Combine(imgPath, imgName);
                    var imgFi = new FileInfo(srcFile);

                    if (!Directory.Exists(imgPath))
                    {
                        Directory.CreateDirectory(imgPath);
                        imgFi.CopyTo(imgPathFile);
                    }
                    else if (!File.Exists(imgPathFile))
                    {
                        imgFi.CopyTo(imgPathFile);
                    }
                    else
                    {
                        var count = 1;
                        var imgNameNoExt = Path.GetFileNameWithoutExtension(srcFile);
                        var imgExt = Path.GetExtension(srcFile);
                        imgPathFile = Path.Combine(imgPath, string.Format("{0} ({1}){2}", imgNameNoExt, count, imgExt));
                        while (File.Exists(imgPathFile))
                        {
                            imgPathFile = Path.Combine(imgPath, string.Format("{0} ({1}){2}", imgNameNoExt, ++count, imgExt));
                        }
                        imgFi.CopyTo(imgPathFile);
                    }
                }
            });
        }

        static DateTime getImageDate(string path)
        {
            PictureMetaInformation instance;
            if (PictureMetaInformation.TryGet(path, out instance))
            {
                var result = instance.DateTimeOriginal;
                if (result.HasValue)
                    return result.Value;
            }
            return new FileInfo(path).CreationTime.Date;
        }

        static void writeLog(string dstDir, IDictionary<string, string> srcErrDictionary, IDictionary<string, string> dstErrDictionary)
        {
            var logFileName = string.Format("{0}.txt", DateTime.Now.ToString("yyyyMMddHHmmssFFFFFFF"));
            using (var sw = File.CreateText(Path.Combine(dstDir, logFileName)))
            {
                sw.WriteLine("Source Errors");
                sw.WriteLine("=============");
                if (srcErrDictionary.Keys.Count > 0)
                {
                    foreach (var key in srcErrDictionary.Keys)
                    {
                        sw.WriteLine("{0}\t{1}", key, srcErrDictionary[key]);
                    }
                }
                else
                {
                    sw.WriteLine("No Source Errors");
                }
                sw.WriteLine();
                sw.WriteLine("Destination Errors");
                sw.WriteLine("==================");
                if (dstErrDictionary.Keys.Count > 0)
                {
                    foreach (var key in dstErrDictionary.Keys)
                    {
                        sw.WriteLine("{0}\t{1}", key, dstErrDictionary[key]);
                    }
                }
                else
                {
                    sw.WriteLine("No Destination Errors");
                }
                sw.Flush();
                sw.Close();
            }
        }
    }
}
