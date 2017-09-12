using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.System;

using System.Windows.Input;
using Windows.Web.Http;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Networking.Sockets;
using Windows.Graphics.Imaging;
using Windows.Storage;
using System.IO;

using Windows.UI.ViewManagement;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=234238

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class TestPage : Page
    {

        public TestPage()
        {
            InitializeComponent();
            Init();
        }

        public void Init()
        {
            TSF.TSFWapper.GetCurrentLang(out string[] langs);
            short[] ids = TSF.TSFWapper.GetLangIDs();

            foreach (short s in ids)
            {
                string[] ss = TSF.TSFWapper.GetInputMethodList(s);

                foreach (var m in ss)
                {
                    combo.Items.Add(m);
                }
                //http://i.imgur.com/QX6CQxM.png
                //http://i.imgur.com/qcndahE.gif
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //System.Windows.Input.ICommand

            //BitmapImage bbb = new BitmapImage(new Uri("ms-appx:///qcndahE.gif"));
            //image1.Source = bbb;

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var cache_folder = ApplicationData.Current.LocalCacheFolder;

            StorageFile file;
            try
            { 
                file = await cache_folder.GetFileAsync("qcndahE.gif");
            }
            catch (FileNotFoundException)
            {
                file = await cache_folder.CreateFileAsync("qcndahE.gif");
            }
            catch (Exception ex)
            {
                file = null;
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }

            
            IBuffer buffer = await GetBufferAsync("http://i.imgur.com/kbEKrBm.jpg");

            var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
            //IRandomAccessStream memStream = new InMemoryRandomAccessStream();

            using (var outputStream = stream.GetOutputStreamAt(0))
            {
                await outputStream.WriteAsync(buffer);
                await outputStream.FlushAsync();
            }
            stream.Dispose();

            BitmapImage bbb = await LoadImage(file);

            image1.Source = bbb;
        }

        private static async Task<BitmapImage> LoadImage(StorageFile file)
        {
            BitmapImage bitmapImage = new BitmapImage();
            FileRandomAccessStream stream = (FileRandomAccessStream)await file.OpenAsync(FileAccessMode.Read);
            bitmapImage.SetSource(stream);
            return bitmapImage;
        }

        async public Task<IInputStream> GetStreamAsync(string url)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                var response = await httpClient.GetInputStreamAsync(new Uri(url));
                return response;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return null;
            }
            
            
        }

        async public Task<IBuffer> GetBufferAsync(string url)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                var buffer = await httpClient.GetBufferAsync(new Uri(url));
                return buffer;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task SaveImageAsync(WriteableBitmap image, string filename)
        {
            var cache_folder = ApplicationData.Current.LocalCacheFolder;

            try
            {
                if (image == null)
                {
                    return;
                }
                Guid BitmapEncoderGuid = BitmapEncoder.JpegEncoderId;
                if (filename.EndsWith("jpg"))
                    BitmapEncoderGuid = BitmapEncoder.JpegEncoderId;
                else if (filename.EndsWith("png"))
                    BitmapEncoderGuid = BitmapEncoder.PngEncoderId;
                else if (filename.EndsWith("bmp"))
                    BitmapEncoderGuid = BitmapEncoder.BmpEncoderId;
                else if (filename.EndsWith("tiff"))
                    BitmapEncoderGuid = BitmapEncoder.TiffEncoderId;
                else if (filename.EndsWith("gif"))
                    BitmapEncoderGuid = BitmapEncoder.GifEncoderId;
                var folder = await cache_folder.CreateFolderAsync("images_cache", CreationCollisionOption.OpenIfExists);
                var file = await folder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);

                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoderGuid, stream);
                    Stream pixelStream = image.PixelBuffer.AsStream();
                    byte[] pixels = new byte[pixelStream.Length];
                    await pixelStream.ReadAsync(pixels, 0, pixels.Length);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                              (uint)image.PixelWidth,
                              (uint)image.PixelHeight,
                              96.0,
                              96.0,
                              pixels);
                    await encoder.FlushAsync();
                }
            }
            catch
            {

            }
        }

        public async Task<SoftwareBitmap> GetSoftwareBitmapAsync(string url)
        {
            try
            {
                IInputStream inputStream = await GetStreamAsync(url);
                IRandomAccessStream memStream = new InMemoryRandomAccessStream();
                await RandomAccessStream.CopyAsync(inputStream, memStream);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(memStream);
                SoftwareBitmap sb = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                return sb;
            }
            catch
            {
                return null;
            }
        }

        public async Task<WriteableBitmap> GetWriteableBitmapAsync(string url)
        {
            try
            {
                IBuffer buffer = await GetBufferAsync(url);
                if (buffer != null)
                {
                    BitmapImage bi = new BitmapImage();
                    WriteableBitmap wb = null; Stream stream2Write;
                    using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                    {

                        stream2Write = stream.AsStreamForWrite();

                        await stream2Write.WriteAsync(buffer.ToArray(), 0, (int)buffer.Length);

                        await stream2Write.FlushAsync();
                        stream.Seek(0);

                        await bi.SetSourceAsync(stream);

                        wb = new WriteableBitmap(bi.PixelWidth, bi.PixelHeight);
                        stream.Seek(0);
                        await wb.SetSourceAsync(stream);

                        return wb;
                    }
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private async void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs e)
        {
            if (e.VirtualKey == VirtualKey.Escape)
            {
                var view = ApplicationView.GetForCurrentView();
                if (view.IsFullScreenMode)
                {
                    string[] args = {
                        @"if (document.exitFullscreen) {
                            document.exitFullscreen();
                        } else if (document.msExitFullscreen) {
                            document.msExitFullscreen();
                        } else if(document.webkitExitFullscreen) {
                            document.webkitExitFullscreen();
                        }"
                    };

                    await LiPTT.CurrentWebView?.InvokeScriptAsync("eval", args);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Measure((new Size(double.PositiveInfinity, double.PositiveInfinity)));
        }
    }
}
