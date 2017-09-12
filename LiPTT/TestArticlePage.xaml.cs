using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Navigation;

using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;
using System.Diagnostics;

namespace LiPTT
{
    public sealed partial class TestArticlePage : Page
    {

        public TestArticlePage()
        {
            this.InitializeComponent();
        }

        List<object> list = new List<object>();

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            

            MyControl.ItemsSource = list;
        }

        public async Task<Image> GetImage(string url, double width, double height)
        {
            BitmapImage bmp = await LiPTT.ImageCache.GetFromCacheAsync(new Uri(url));

            return new Image() { Source = bmp, Width = width, Height = height };
        }

        public WebView GetYoutubeView(string youtubeID, double width = 0, double height = 0)
        {
            WebView wv = new WebView() { Width = width == 0 ? 800 : width, Height = height == 0 ? width * 0.5625 : height, DefaultBackgroundColor = Windows.UI.Colors.Black };

            wv.DOMContentLoaded += async (a, b) => {

                string script = GetYoutubeScript(youtubeID, wv.ActualWidth, wv.ActualHeight);
                try
                {
                    //執行Youtube Iframe API的腳本                   
                    string returnStr = await wv.InvokeScriptAsync("eval", new string[] { script });
                }
                catch (Exception ex)
                {
                    //腳本寫錯了?
                    Debug.WriteLine("Script Running Error" + ex.ToString() + script);
                }
            };

            return wv;
        }

        private string GetYoutubeScript(string YoutubeID, double width, double height)
        {
            string script = "function onYouTubeIframeAPIReady() { var player = new YT.Player('player', { height: '@Height', width: '@Width', videoId: '@YoutubeID'}); }";
            script = script.Replace("@YoutubeID", YoutubeID);
            script = script.Replace("@Width", ((int)Math.Round(width)).ToString());
            script = script.Replace("@Height", ((int)Math.Round(height)).ToString());
            return script;
        }

        public async void SaveFile(IBuffer buffer, string filename)
        {
            var cache_folder = ApplicationData.Current.LocalCacheFolder;
            StorageFile file = await cache_folder.CreateFileAsync(filename);
            var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            using (var outputStream = stream.GetOutputStreamAt(0))
            {
                await outputStream.WriteAsync(buffer);
                await outputStream.FlushAsync();
            }
            stream.Dispose();
        }
     

        public async Task<BitmapImage> LoadImage(StorageFile file)
        {
            BitmapImage bitmapImage = new BitmapImage();

            using (FileRandomAccessStream fras = (FileRandomAccessStream)await file.OpenAsync(FileAccessMode.Read))
            {
                //把圖像複製到記憶體中，脫離對StorageFile的依賴
                Stream stream = fras.AsStream();
                MemoryStream memStream = new MemoryStream();
                await stream.CopyToAsync(memStream);
                memStream.Position = 0;
                bitmapImage.SetSource(memStream.AsRandomAccessStream());
            }

            return bitmapImage;
        }

        private void AddPara(object sender, RoutedEventArgs e)
        {
            RichTextBlock tb1 = new RichTextBlock() { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.NoWrap, Width = 800 };
            Paragraph para1 = new Paragraph();

            for (int i = 0; i < 100; i++)
            {
                Run run = new Run() { Text = "提升大約6.8%左右的效能，即使在AMD AM4平台時脈表現還是能在3000上下穩定           " };

                para1.Inlines.Add(run);
                para1.Inlines.Add(new LineBreak());
            }
            tb1.Blocks.Add(para1);
            stackpanel.Children.Add(tb1);


        }


        private async void AddImage(object sender, RoutedEventArgs e)
        {
            HyperlinkButton button = new HyperlinkButton() { HorizontalAlignment = HorizontalAlignment.Center };

            
            Image img = await GetImage("http://i.imgur.com/kbEKrBm.jpg", 400, 400);
            button.Content = img;
            button.NavigateUri = new Uri("http://i.imgur.com/kbEKrBm.jpg");
            stackpanel.Children.Add(button);
        }

        private async void AddYoutube(object sender, RoutedEventArgs e)
        {
            List<object> list = new List<object>();

            Uri uri2 = new Uri("http://youtu.be/P0bmkDJBnZU");
            HyperlinkButton hyper = new HyperlinkButton() { Content = uri2.OriginalString, NavigateUri = uri2, FontSize=28, HorizontalAlignment= HorizontalAlignment.Stretch };
            list.Add(hyper);
            


            WebView myWebView = GetYoutubeView("P0bmkDJBnZU", 888);
            list.Add(myWebView);
            myWebView.DefaultBackgroundColor = Windows.UI.Colors.Black;
            myWebView.Navigate(new Uri("ms-appx-web:///Templates/youtube.html"));



            MyControl.ItemsSource = list;

            
            
        }


    }
}
