using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI;
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

        private RichTextBlock tb;
        private Paragraph ph;
        private bool cut = false;

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            Debug.WriteLine("Press");
        }

        private void AddListViewItem_Click(object sender, RoutedEventArgs e)
        {
            ListView list = new ListView() { IsItemClickEnabled = true, HorizontalAlignment = HorizontalAlignment.Stretch };
            list.Items.Add(new ListViewItem() { Content = "Hello", IsSelected = false });
            list.ItemClick += ListView_ItemClick;
            tCollection.Add(list);

            cut = true;
        }

        private void AddText_Click(object sender, RoutedEventArgs e)
        {
            if (tb == null || cut)
            {
                cut = false;
                tb = new RichTextBlock();
                ph = new Paragraph();
                tb.Blocks.Add(ph);
                tCollection.Add(tb);
            }

            Run run = new Run() { Text = "HeeeHeeeee", FontSize = 30 };
            ph.Inlines.Add(run);
            ph.Inlines.Add(new LineBreak());
        }

        private void AddView_Click(object sender, RoutedEventArgs e)
        { 
            Grid YoutuGrid = new Grid() { Background = new SolidColorBrush(Colors.DarkRed), HorizontalAlignment = HorizontalAlignment.Stretch };
            ColumnDefinition c1, c2, c3;
            double space = 0.2;
            c1 = new ColumnDefinition { Width = new GridLength(space / 2.0, GridUnitType.Star) };
            c2 = new ColumnDefinition { Width = new GridLength((1 - space), GridUnitType.Star) };
            c3 = new ColumnDefinition { Width = new GridLength(space / 2.0, GridUnitType.Star) };

            double w = 800;
            double h = 400;

            WebView wv = new WebView() { Tag = "YoutubeWebView", Width = w, Height = h, DefaultBackgroundColor = Colors.Gray };

            Grid grid = new Grid() { Width = w, Height = h, Tag = "Youtube", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Background = new SolidColorBrush(Colors.Gray) };
            ProgressRing progress = new ProgressRing() { IsActive = true, Width = 50, Height = 50, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Colors.Red) };
            string script = GetYoutubeScript("oXp2oE0xQcE", w, h);

            wv.ContentLoading += (a, b) =>
            {
                wv.Visibility = Visibility.Collapsed;
            };

            wv.FrameDOMContentLoaded += (a, b) =>
            {
                progress.IsActive = false;
                wv.Visibility = Visibility.Visible;
            };

            wv.DOMContentLoaded += async (a, b) =>
            {
                try
                {
                    string returnStr = await wv.InvokeScriptAsync("eval", new string[] { script });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Script Error" + ex.ToString() + script);
                }
            };

            grid.Children.Add(wv);
            grid.Children.Add(progress);
            grid.SetValue(Grid.ColumnProperty, 1);
            YoutuGrid.Children.Add(grid);

            tCollection.Add(YoutuGrid);

            cut = true;

            wv.Navigate(new Uri("ms-appx-web:///Templates/youtube.html"));
        }
    }

    public class TestCollection : ObservableCollection<object>, ISupportIncrementalLoading
    {
        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            throw new NotImplementedException();
        }

        private bool more;

        public bool HasMoreItems
        {
            get
            {
                return false;
            }
            set
            {
                more = value;
            }
        }
    }
}
