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
using Windows.UI.Xaml.Media;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;
using System.Diagnostics;

using System.ComponentModel;
using System.Runtime.CompilerServices;

using System.Collections.ObjectModel;

namespace LiPTT
{
    public sealed partial class TestArticlePage : Page, INotifyPropertyChanged
    {

        public TestArticlePage()
        {
            this.InitializeComponent();
        }

        CollectionViewSource source;

        CollectionViewSource ViewSource
        {
            get
            {
                return source;
            }
            set
            {
                value = source;
                NotifyPropertyChanged("ViewSource");
            }
        }

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            source = new CollectionViewSource();

            ObservableCollection<RichTextBlock> collection1 = new ObservableCollection<RichTextBlock>();

            for (int i = 0; i < 3; i++)
            {
                RichTextBlock tb = new RichTextBlock();

                Paragraph para = new Paragraph();

                para.Inlines.Add(new Run() { Text = "Hello world" });
                para.Inlines.Add(new LineBreak());
                tb.Blocks.Add(para);


                collection1.Add(tb);
            }

            ObservableCollection<HyperlinkButton> collection2 = new ObservableCollection<HyperlinkButton>();

            for (int i = 0; i < 3; i++)
            {
                HyperlinkButton hyper = new HyperlinkButton();

                hyper.Content = "hello world";
                hyper.NavigateUri = new Uri("https://www.google.com.tw/");
                collection2.Add(hyper);
            }

            ViewSource.IsSourceGrouped = true;

            //ViewSource.View.Add(collection1);


            this.DataContext = this;

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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ListViewItem item1 = new ListViewItem() { IsHitTestVisible = false, Content = new Windows.UI.Xaml.Shapes.Rectangle() { Width = 1000, Height = 20, Fill = new SolidColorBrush(Windows.UI.Colors.Red) } };
            TestView.Items.Add(item1);

            RichTextBlock rb = new RichTextBlock();
            Paragraph pa = new Paragraph();
            Run run = new Run() { Text = "helloheooo", FontSize = 30 };
            Run run2 = new Run() { Text = "helloheooo", FontSize = 22 };
            Run run3 = new Run() { Text = "helloheooo", FontSize = 18 };
            pa.Inlines.Add(run);
            pa.Inlines.Add(new LineBreak());
            pa.Inlines.Add(run2);
            pa.Inlines.Add(new LineBreak());
            pa.Inlines.Add(run3);
            pa.Inlines.Add(new LineBreak());
            rb.Blocks.Add(pa);

            ListViewItem item2 = new ListViewItem()
            {
                IsHitTestVisible = true,
                Content = rb,
            };
            TestView.Items.Add(item2);

            TestView.Items.Add(new ListViewItem() { IsHitTestVisible = true, Content = new Windows.UI.Xaml.Shapes.Rectangle() { Width = 1000, Height = 20, Fill = new SolidColorBrush(Windows.UI.Colors.Gray) } });

        }
    }
}
