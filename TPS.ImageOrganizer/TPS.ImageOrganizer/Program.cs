using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;

namespace TPS.ImageOrganizer
{
    class Program
    {
        static int _threads = Environment.ProcessorCount > 1 ? Convert.ToInt32(Math.Floor(Environment.ProcessorCount * 0.5)) : 1;

        static void Main(string[] args)
        {
            var action = args[0];
            switch(action.ToLower())
            {
                case "hash":
                    doHash(args[1]);
                    break;
                case "copy":
                    doCopy(args[1], args[2]);
                    break;
            }

            //var srcDir = args[0];
            //var dstDir = args[1];

            //var srcHashDictionary = new Dictionary<string, string>();
            //var srcErrDictionary = new Dictionary<string, string>();
            //var dstHashDictionary = new Dictionary<string, string>();
            //var dstErrDictionary = new Dictionary<string, string>();

            //var ts = DateTime.Now;
            //Console.Write("Getting Source Image Hashes... ");
            //hashDirectory(srcHashDictionary, srcErrDictionary, srcDir);
            //Console.WriteLine("{0} ms", (DateTime.Now - ts).TotalMilliseconds);

            //ts = DateTime.Now;
            //Console.Write("Getting Destination Image Hashes... ");
            //hashDirectory(dstHashDictionary, dstErrDictionary, dstDir);
            //Console.WriteLine("{0} ms", (DateTime.Now - ts).TotalMilliseconds);

            //ts = DateTime.Now;
            //Console.Write("Copying Files... ");
            //copyFiles(dstDir, srcHashDictionary, srcErrDictionary, dstHashDictionary, dstErrDictionary);
            //Console.WriteLine("{0} ms", (DateTime.Now - ts).TotalMilliseconds);

            //ts = DateTime.Now;
            //Console.Write("Writing Log... ");
            //writeLog(dstDir, srcErrDictionary, dstErrDictionary);
            //Console.WriteLine("{0} ms", (DateTime.Now - ts).TotalMilliseconds);

            Console.WriteLine("Done.");
            Console.ReadKey();
        }

        static void hashDirectory(Dictionary<string, string> hashDictionary, Dictionary<string, string> errDictionary, string path)
        {
            if (Directory.Exists(path))
            {
                var dir = new DirectoryInfo(path);

                var fileInfos = dir.GetFilesByExtensions(".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff");

                Parallel.ForEach(fileInfos, new ParallelOptions { MaxDegreeOfParallelism = _threads }, fil =>
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
                for(var i = 0; i < dirInfos.Length; i++)
                {
                    hashDirectory(hashDictionary, errDictionary, dirInfos[i].FullName);
                }
            }
        }

        static string getHash(string fileName)
        {
            try
            {
                var pixels = default(int[]);
                using (var bmp = new Bitmap(fileName))
                {
                    pixels = new int[bmp.Width * bmp.Height];
                    for (var x = 0; x < bmp.Width; x++)
                        for (var y = 0; y < bmp.Height; y++)
                            pixels[x + y * bmp.Width] = bmp.GetPixel(x, y).ToArgb();
                }

                var bytes = new byte[pixels.Length * sizeof(int)];
                Buffer.BlockCopy(pixels, 0, bytes, 0, bytes.Length);

                return Convert.ToBase64String(SHA256.Create().ComputeHash(bytes));
            }
            catch(Exception ex)
            {
                Console.WriteLine("{0}\t{1}", fileName, ex.Message);
                return null;
            }
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
                    dstHashDictionary.Add(hash, imgPathFile);
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

        static void doHash(string srcDir)
        {
            var ts = DateTime.Now;
            Console.WriteLine("\nFinding images to Hash...");
            _srcFiles = new List<string>();
            findFiles(srcDir);
            Console.WriteLine("Found {0} images in {1} seconds", _srcFiles.Count, (DateTime.Now - ts).TotalMilliseconds / 1000);

            ts = DateTime.Now;
            Console.WriteLine("\nHashing images using {0} threads... ", _threads);
            _srcHashes = new Dictionary<string, string>();
            _srcErrors = new Dictionary<string, string>();
            Parallel.ForEach(_srcFiles, new ParallelOptions { MaxDegreeOfParallelism = _threads }, fileName => {
                var hash = getHash(fileName);
                if (!string.IsNullOrWhiteSpace(hash))
                {
                    if (!_srcHashes.ContainsKey(hash))
                    {
                        _srcHashes.Add(hash, fileName);
                        File.AppendAllText(Path.Combine(srcDir, "hashes.csv"), string.Format("\"{0}\",\"{1}\"", hash, fileName));
                    }
                    else
                    {
                        Console.WriteLine("{0} is a duplicate of {1}", fileName, _srcHashes[hash]);
                    }
                }
            });
            File.WriteAllText(Path.Combine(srcDir, "hashes.txt"), JsonConvert.SerializeObject(_srcHashes));
            Console.WriteLine("Hashed {0} images in {1} seconds", _srcFiles.Count, (DateTime.Now - ts).TotalMilliseconds / 1000);

            Console.WriteLine("\nCleaning up...");
            _srcHashes = null;
            _srcFiles = null;
        }

        static List<string> _srcFiles;
        static IDictionary<string, string> _srcHashes;
        static IDictionary<string, string> _srcErrors;
        static IDictionary<string, string> _dstHashes;

        static void findFiles(string path)
        {
            var dir = new DirectoryInfo(path);
            _srcFiles.AddRange(dir.GetFilesByExtensions(".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff").Select(x => x.FullName));
            foreach(var subDir in dir.GetDirectories())
                findFiles(subDir.FullName);
        }

        static Dictionary<string, string> tryReadHashFile(string dir)
        {
            var json = default(string);
            try
            {
                json = File.ReadAllText(Path.Combine(dir, "hashes.txt"));
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch(Exception ex)
            {
                return new Dictionary<string, string>();
            }
        }

        static void doCopy(string srcDir, string dstDir)
        {
            var ts = DateTime.Now;
            Console.WriteLine("Loading Source and Destination Hash Files...");
            _srcHashes = tryReadHashFile(srcDir);
            _dstHashes = tryReadHashFile(dstDir);
            Console.WriteLine("Loaded {0} hashes in {1} seconds.", _srcHashes.Count + _dstHashes.Count(), (DateTime.Now - ts).TotalMilliseconds / 1000);

            ts = DateTime.Now;
            Console.WriteLine("Copying Files from Source to Destination...");
            Console.WriteLine("0%");
            var totalCount = 0;
            foreach(var hash in _srcHashes.Keys)
            {
                if(!_dstHashes.ContainsKey(hash))
                {
                    var srcFullName = _srcHashes[hash];
                    var srcName = Path.GetFileName(srcFullName);
                    var srcDate = getImageDate(srcFullName);
                    var dstPath = Path.Combine(dstDir, srcDate.Year.ToString(), srcDate.ToString("yyyy-MM-dd"));
                    var dstFullName = Path.Combine(dstPath, srcName);
                    var srcFi = new FileInfo(srcFullName);

                    if (!Directory.Exists(dstPath))
                    {
                        Directory.CreateDirectory(dstPath);
                        srcFi.CopyTo(dstFullName);
                    }
                    else if(!File.Exists(dstFullName))
                    {
                        srcFi.CopyTo(dstFullName);
                    }
                    else
                    {
                        var count = 1;
                        var srcNameNoExt = Path.GetFileNameWithoutExtension(srcName);
                        var srcExt = Path.GetExtension(srcName);
                        dstFullName = Path.Combine(dstPath, string.Format("{0} ({1}){2}", srcNameNoExt, count, srcExt));
                        while(File.Exists(dstFullName))
                        {
                            dstFullName = Path.Combine(dstPath, string.Format("{0} ({1}){2}", srcNameNoExt, ++count, srcExt));
                        }
                        srcFi.CopyTo(dstFullName);
                    }
                    _dstHashes.Add(hash, dstFullName);
                    Console.Write("\r{0:P}                      ", (double)++totalCount / (double)_srcHashes.Count);
                    totalCount++;
                }
                else
                {
                    Console.WriteLine("\n{0} is a duplicate of {1}", _srcHashes[hash], _dstHashes[hash]);
                }
                File.WriteAllText(Path.Combine(dstDir, "hashes.txt"), JsonConvert.SerializeObject(_dstHashes));
            }
            Console.WriteLine("Copied {0} files in {1} seconds.", _srcHashes.Count, (DateTime.Now - ts).TotalMilliseconds / 1000);
        }
    }
}
