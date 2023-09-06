﻿using MessagingToolkit.Barcode;
using MessagingToolkit.Barcode.Common;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using TournamentAssistantShared;
using Size = System.Drawing.Size;

namespace TournamentAssistantUI.Misc
{
    public class QRUtils
    {
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public static Bitmap ReadPrimaryScreenBitmap(int sourceX, int sourceY, Size size)
        {
            var bmpScreenshot = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bmpScreenshot))
            {
                graphics.CopyFromScreen(sourceX,
                                        sourceY,
                                        0,
                                        0,
                                        size,
                                        CopyPixelOperation.SourceCopy);
            }

            return bmpScreenshot;
        }

        public static Result[] ReadQRsFromScreen(int sourceX, int sourceY, Size size)
        {
            Logger.Info("Scanning for barcodes");
            using (var bitmap = ReadPrimaryScreenBitmap(sourceX, sourceY, size))
            {
                var hBitmap = bitmap.GetHbitmap();
                var decoder = new BarcodeDecoder();
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hBitmap);
                var writeableBitmap = new WriteableBitmap(bitmapSource);

                Result[] results = decoder.DecodeMultiple(writeableBitmap);
                Logger.Info("Done!");

                return results;
            }
        }

        public static string[] ReadQRsFromScreenIntoUserIds(int sourceX, int sourceY, Size size)
        {
            return ReadQRsFromScreen(sourceX, sourceY, size).Select(x => x.Text).ToArray();
        }

        public static byte[] GenerateQRCodePngBytes(string data)
        {
            using (var bitmap = GenerateQRCode(data))
            {
                return ConvertBitmapToPngBytes(bitmap);
            }
        }

        public static byte[] ConvertBitmapToPngBytes(Bitmap bitmap)
        {
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        public static Bitmap GenerateColoredBitmap(int width = 1920, int height = 1080, Color? color = null)
        {
            Bitmap bitmap = new Bitmap(width, height);
            using (Graphics gfx = Graphics.FromImage(bitmap))
            {
                using (SolidBrush brush = new SolidBrush(color ?? Color.FromArgb(0, 128, 0)))
                {
                    gfx.FillRectangle(brush, 0, 0, width, height);
                }
            }
            return bitmap;
        }

        public static Bitmap GenerateQRCode(string data, int width = 1920, int height = 1080)
        {
            var encoder = new MessagingToolkit.Barcode.QRCode.QRCodeEncoder();
            return BitMatrixToBitmap(encoder.Encode(data, BarcodeFormat.QRCode, width, height));
        }

        private static Bitmap BitMatrixToBitmap(BitMatrix matrix)
        {
            int height = matrix.Height;
            int width = matrix.Width;
            Bitmap bitmap = new Bitmap(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bitmap.SetPixel(x, y, matrix.Get(x, y) ? Color.Black : Color.White);
                }
            }
            return bitmap;
        }
    }
}