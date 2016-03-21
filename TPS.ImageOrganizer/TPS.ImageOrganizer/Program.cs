using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var hash = generateHash(args[0]);
            Console.WriteLine(hash);

//            writeHashToExif(args[0], hash);
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

        public static void writeHashToExif(string fileName, string hash)
        {
        }

        private static void SetProperty(ref System.Drawing.Imaging.PropertyItem prop, int iId, string sTxt)
        {
            int iLen = sTxt.Length + 1;
            byte[] bTxt = new Byte[iLen];
            for (int i = 0; i < iLen - 1; i++)
                bTxt[i] = (byte)sTxt[i];
            bTxt[iLen - 1] = 0x00;
            prop.Id = iId;
            prop.Type = 2;
            prop.Value = bTxt;
            prop.Len = iLen;
        }
    }
}
