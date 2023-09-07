using MessagingToolkit.Barcode;
using MessagingToolkit.Barcode.Common;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using TournamentAssistantShared;
using TournamentAssistantUI.UI.Forms;
using Size = System.Drawing.Size;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for QRPage.xaml
    /// </summary>
    public partial class QRPage : Page
    {
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private PrimaryDisplayHighlighter _primaryDisplayHighlighter;
        //private ResizableLocationSpecifier _resizableLocationSpecifier;

        private int sourceX = Screen.PrimaryScreen.Bounds.X;
        private int sourceY = Screen.PrimaryScreen.Bounds.Y;
        private Size size = Screen.PrimaryScreen.Bounds.Size;

        public QRPage()
        {
            InitializeComponent();
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (_primaryDisplayHighlighter == null || _primaryDisplayHighlighter.IsDisposed)
            {
                _primaryDisplayHighlighter = new PrimaryDisplayHighlighter(Screen.PrimaryScreen.Bounds);
            }

            _primaryDisplayHighlighter.Show();

            /*if (_resizableLocationSpecifier == null || _resizableLocationSpecifier.IsDisposed)
            {
                _resizableLocationSpecifier = new ResizableLocationSpecifier();
                _resizableLocationSpecifier.LocationOrSizeChanged += (startX, startY, newSize) =>
                {
                    sourceX = startX;
                    sourceY = startY;
                    size = newSize;
                };
            }*/

            //_resizableLocationSpecifier.Show();

            /*BitmapImage imageSource;
            using (var displayBitmap = GenerateMTQR(GenerateTextBox.Text))
            {
                imageSource = BitmapToImageSource(displayBitmap);
            }
            QRImage.Source = imageSource;

            await Task.Delay(1000);*/

            //await ContinuouslyScanForQRCodes_MT();

            await DisplayFromScreen();

            _primaryDisplayHighlighter.Close();
            //_resizableLocationSpecifier.Close();
        }

        #region MessagingToolkit
        public Bitmap WriteableBitmapToBitmap(WriteableBitmap writeableBitmap)
        {
            if (writeableBitmap == null)
            {
                return null;
            }

            var width = writeableBitmap.PixelWidth;
            var height = writeableBitmap.PixelHeight;

            var stride = width * ((writeableBitmap.Format.BitsPerPixel + 7) / 8);
            var pixelData = new byte[height * stride];

            writeableBitmap.CopyPixels(pixelData, stride, 0);

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                bitmap.PixelFormat);

            var bitmapPtr = bitmapData.Scan0;
            Marshal.Copy(pixelData, 0, bitmapPtr, pixelData.Length);

            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }

        private Result[] ReadQRLocationsFromScreen()
        {
            using (var bitmap = ReadPrimaryScreenBitmap())
            {
                var hBitmap = bitmap.GetHbitmap();
                Logger.Info("Scanning for barcodes");
                var decoder = new BarcodeDecoder();
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hBitmap);
                var writeableBitmap = new WriteableBitmap(bitmapSource);

                Result[] ret = decoder.DecodeMultiple(WriteableBitmapToBitmap(writeableBitmap));
                Logger.Info("Done!");
                return ret;
            }
        }

        private async Task ContinuouslyScanForQRCodes_MT(int duration = 30 * 1000)
        {
            Action captureFrame = () =>
            {
                var scanResults = ReadQRLocationsFromScreen();
                if (scanResults != null && scanResults.Length > 0)
                {
                    var successMessage = string.Empty;
                    scanResults.ToList().ForEach(x => successMessage += $"{x.Text}, ");
                    Logger.Success(successMessage);
                }
            };

            var captureStart = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            while ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - captureStart < duration)
            {
                await Task.Run(captureFrame);
            }
        }

        private Bitmap GenerateMTQR(string data)
        {
            var encoder = new MessagingToolkit.Barcode.QRCode.QRCodeEncoder();
            return BitMatrixToBitmap(encoder.Encode(data, BarcodeFormat.QRCode, 1920, 1080));
        }

        public Bitmap BitMatrixToBitmap(BitMatrix matrix)
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
        #endregion

        #region Screen
        private async Task DisplayFromScreen(int duration = 10 * 1000)
        {
            Action captureFrame = () =>
            {
                using (var screenBitmap = ReadPrimaryScreenBitmap())
                {
                    Dispatcher.Invoke(() => QRImage.Source = BitmapToImageSource(screenBitmap));
                }
            };

            var captureStart = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            while ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - captureStart < duration)
            {
                await Task.Run(captureFrame);
            }
        }

        Bitmap ReadPrimaryScreenBitmap()
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

        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                bitmapimage.Freeze();

                return bitmapimage;
            }
        }
        #endregion
    }
}
