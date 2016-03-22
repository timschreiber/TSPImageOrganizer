using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;

namespace TPS.ImageOrganizer
{
    class Program
    {
        static void Main(string[] args)
        {
            var operation = args[0];
            var fileName = args[1];
            var hash = generateHash(fileName);

            switch(operation.ToLower())
            {
                case "write":
                    writeHashToExif(fileName, hash);
                    break;
                case "read":
                    Console.WriteLine(readHashFromExif(fileName));
                    break;
                case "match":
                    Console.WriteLine(hashesMatch(fileName));
                    break;
            }

            Console.WriteLine("OK");
            Console.ReadKey();
        }

        public static string generateHash(string fileName)
        {
            using(var @in = Image.FromFile(fileName))
            using(var @out = new MemoryStream())
            {
                @in.Save(@out, ImageFormat.Bmp);
                return Convert.ToBase64String(SHA256.Create().ComputeHash(@out));
            }
        }

        public static string readHashFromExif(string fileName)
        {
            PictureMetaInformation instance;
            if (!PictureMetaInformation.TryGet(fileName, out instance))
                return string.Empty;
            return instance.Caption;
        }

        public static void writeHashToExif(string fileName, string hash)
        {
            PictureMetaInformation instance;
            if(PictureMetaInformation.TryGet(fileName, out instance))
            {
                instance.Caption = hash;
                instance.Write();
            }
        }

        public static bool hashesMatch(string fileName)
        {
            var hash1 = generateHash(fileName);
            var hash2 = readHashFromExif(fileName);
            return string.Compare(hash1, hash2) == 0;
        }
    }
}
