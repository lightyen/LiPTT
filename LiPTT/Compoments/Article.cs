using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using System.Collections.ObjectModel;
using Windows.Foundation;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml;
using Windows.UI.Core;
using Windows.ApplicationModel.Core;
using System.Windows.Input;

namespace LiPTT
{
    public class ArticleContentCollection : ObservableCollection<object>, ISupportIncrementalLoading
    {
        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
        }

        private SemaphoreSlim Semaphore;

        private bool more;

        public bool HasMoreItems
        {
            get
            {
                if (ScrollEnd) return more;
                else return false;
            }
            private set
            {
                more = value;
            }
        }

        private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint count)
        {
            await Semaphore.WaitAsync();
            LiPTT.PttEventEchoed += PttUpdated;
            LiPTT.PageDown();
            return new LoadMoreItemsResult { Count = (uint)this.RawLines.Count };
        }

        private void PttUpdated(PTTProvider sender, LiPttEventArgs e)
        {
            if (e.State == PttState.Article)
            {
                bound = ReadLineBound(e.Screen.ToString(23));

                int o = header ? 1 : 0;

                for (int i = line - bound.Begin + 1 + (bound.Begin < 5 ? o : 0); i < 23; i++, line++)
                {
                    RawLines.Add(LiPTT.Copy(e.Screen[i]));
                }

                var action = LiPTT.RunInUIThread( () =>
                {
                    Parse();
                    Semaphore.Release();
                    ScrollEnd = false;
                });

                if (bound.Percent == 100) HasMoreItems = false;
                else HasMoreItems = true;

                LiPTT.PttEventEchoed -= PttUpdated;
            }
        }

        public Article ArticleTag
        {
            get; set;
        }

        public double ViewWidth { get; set; }
        public double ViewHeight { get; set; }

        private static HashSet<string> ShortCutUrlSet = new HashSet<string>()
        {
            "youtu.be",
            //"goo.gl",
            //"bit.ly",
            //"ppt.cc",
        };

        public List<Block[]> RawLines { get; set; } //文章生肉串

        public List<Task<DownloadResult>> DownloadPictureTasks { get; set; }

        private bool header;

        private int line;

        private int ParsedLine;

        private const double ArticleFontSize = 24.0;

        private FontFamily ArticleFontFamily;

        private RichTextBlock RichTB;

        private Paragraph Paragraph;

        private Bound bound;

        public bool ScrollEnd;

        public ArticleContentCollection()
        {
            ScrollEnd = true;
            Semaphore = new SemaphoreSlim(1, 1);
            header = false;
            line = 0;
            RawLines = new List<Block[]>();
            DownloadPictureTasks = new List<Task<DownloadResult>>();

            //this.CollectionChanged += ArticleContentCollection_CollectionChanged;

            var action = LiPTT.RunInUIThread(() =>
            {
                ArticleFontFamily = new FontFamily("Noto Sans Mono CJK TC");
            });
        }

        private void ArticleContentCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, e.NewItems);
                OnCollectionChanged(args);
            }
        }

        public void BeginLoad(Article article)
        {
            ArticleTag = article;

            IAsyncAction action = null;

            ScreenBuffer screen = LiPTT.Screen;

            bound = ReadLineBound(screen.ToString(23));

            RawLines.Clear();
            

            Regex regex;
            Match match;
            string tmps;

            if (bound.Begin == 1)
            {
                tmps = screen.ToString(3);

                if (tmps.StartsWith("───────────────────────────────────────"))
                {
                    header = true;
                }

                if (header)
                {
                    //作者
                    tmps = screen.ToString(0);
                    regex = new Regex(@"作者  [A-Za-z0-9]+ ");
                    match = regex.Match(tmps);
                    if (match.Success)
                    {
                        ArticleTag.Author = tmps.Substring(match.Index + 4, match.Length - 5);
                    }

                    //匿稱
                    ArticleTag.AuthorNickname = "";
                    regex = new Regex(@"\([\S\s^\(^\)]+\)");
                    match = regex.Match(tmps);
                    if (match.Success)
                    {
                        ArticleTag.AuthorNickname = tmps.Substring(match.Index + 1, match.Length - 2);
                    }

                    //標題
                    //已讀過 這裡不再Parse

                    //時間
                    //https://msdn.microsoft.com/zh-tw/library/8kb3ddd4(v=vs.110).aspx
                    System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                    tmps = screen.ToString(2, 7, 24);
                    if (tmps[8] == ' ') tmps = tmps.Remove(8, 1);

                    try
                    {
                        ArticleTag.Date = DateTimeOffset.ParseExact(tmps, "ddd MMM d HH:mm:ss yyyy", provider);
                    }
                    catch (FormatException)
                    {
                        Debug.WriteLine("時間格式有誤? " + tmps);
                    }

                    line = 3;
                }
                else
                {
                    line = 0;
                    Debug.WriteLine("沒有文章標頭? " + tmps);
                }

                //////////////////////////////////////////////////////////////////////////////////////////
                //第一頁文章內容
                //////////////////////////////////////////////////////////////////////////////////////////

                int o = bound.End - bound.Begin + 1;
                if (o < 23)
                {
                    if (header) o = bound.End + 1;
                    else o = bound.End;
                }

                for (int i = header ? 4 : 0; i < o; i++, line++)
                {
                    RawLines.Add(LiPTT.Copy(screen[i]));
                }

                action = LiPTT.RunInUIThread(() => 
                {
                    ArticleTag.LoadCompleted = false;
                    this.Clear();
                    Parse();
                    ArticleTag.LoadCompleted = true;
                });

                if (bound.Percent < 100) HasMoreItems = true;
            }

            ArticleTag.LoadCompleted = true;
        }

        public async void Parse()
        {
            for (int row = ParsedLine; row < RawLines.Count; row++, ParsedLine++)
            {
                string str = LiPTT.GetString(RawLines[row]);

                if (str.StartsWith("※"))
                {
                    PrepareAddText();
                    Run run = new Run()
                    {
                        Text = str,
                        FontSize = ArticleFontSize - 8,
                        FontFamily = ArticleFontFamily,
                        Foreground = new SolidColorBrush(Colors.Green),
                    };
                    Paragraph.Inlines.Add(run);
                    Paragraph.Inlines.Add(new LineBreak());
                    RichTB.Height = Paragraph.Inlines.Count * ArticleFontSize;
                }
                else if (IsEchoes(str))
                {
                    AddEcho(str);
                }
                else
                {
                    Match match = new Regex(LiPTT.http_regex).Match(str);

                    if (match.Success)
                    {
                        PrepareAddText();
                        AddUriTextLine(match, str, RawLines[row]);
                    }
                    else
                    {
                        PrepareAddText();
                        AddTextLine(RawLines[row]);
                    }
                }
            }

            if (DownloadPictureTasks.Count > 0)
            {
                while (DownloadPictureTasks.Count > 0)
                {
                    var firstFinishedTask = await Task.WhenAny(DownloadPictureTasks);

                    this[firstFinishedTask.Result.Index] = firstFinishedTask.Result.Item;
                    DownloadPictureTasks.Remove(firstFinishedTask);
                }
            }
        }

        private void PrepareAddText()
        {
            if (RichTB == null)
            {
                RichTB = new RichTextBlock() { };
                Paragraph = new Paragraph();
                RichTB.Blocks.Add(Paragraph);
                Add(RichTB);
            }
        }

        private void AddTextLine(Block[] blocks)
        {
            int color = blocks[0].ForegroundColor;
            int index = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                Block b = blocks[i];
                if (color != b.ForegroundColor || i == blocks.Length - 1)
                {
                    string text = LiPTT.GetString(blocks, index, i - index);

                    /***
                    InlineUIContainer container = new InlineUIContainer
                    {
                        Child = new Border()
                        {
                            Background = GetBackgroundBrush(blocks[index]),
                            Child = new TextBlock()
                            {
                                IsTextSelectionEnabled = true,
                                Text = text.Replace('\0', ' '),
                                FontSize = ArticleFontSize,
                                FontFamily = ArticleFontFamily,
                                Foreground = GetForegroundBrush(blocks[index]),
                            }
                        }
                    };
                    /***/
                    //***
                    Run container = new Run()
                    {
                        Text = text.Replace('\0', ' '),
                        FontSize = ArticleFontSize,
                        FontFamily = ArticleFontFamily,
                        Foreground = GetForegroundBrush(blocks[index]),
                    };
                    /***/
                    Paragraph.Inlines.Add(container);
                    index = i;
                    color = b.ForegroundColor;
                }
            }

            Paragraph.Inlines.Add(new LineBreak());
            RichTB.Height = Paragraph.Inlines.Count * ArticleFontSize;
        }

        private void AddUriTextLine(Match match, string msg, Block[] blocks)
        {
            Uri uri = new Uri(msg.Substring(match.Index, match.Length));

            //假使Uri前面有文章內容
            if (match.Index > 0)
            {
                int count = CountBlocks(msg, 0, match.Index);
                int index = 0;
                int color = blocks[index].ForegroundColor;

                for (int i = 0; i < count; i++)
                {
                    Block b = blocks[i];

                    if (color != b.ForegroundColor || i == count - 1)
                    {
                        string text = LiPTT.GetString(blocks, index, i - index).Replace('\0', ' ');

                        Run container = new Run()
                        {
                            Text = text,
                            FontSize = ArticleFontSize,
                            FontFamily = ArticleFontFamily,
                            Foreground = GetForegroundBrush(blocks[index]),
                        };

                        Paragraph.Inlines.Add(container);
                        index = i;
                        color = b.ForegroundColor;
                    }
                }
            }

            //判斷Uri是否要顯示出來
            bool hyperlinkVisible = true;

            if (IsPictureUri(uri))
            {
                hyperlinkVisible = false;
            }
            else if (IsYoutubeUri(uri))
            {
                hyperlinkVisible = false;
            }
            else if (uri.Host == "imgur.com" && uri.OriginalString.IndexOf("imgur.com/a") == -1)
            {
                hyperlinkVisible = false;
            }

            //插入超連結
            if (hyperlinkVisible)
            {
                Hyperlink hyperlink = new Hyperlink() { NavigateUri = uri, UnderlineStyle = UnderlineStyle.Single };
                hyperlink.Inlines.Add(new Run()
                {
                    Text = uri.OriginalString,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontFamily = ArticleFontFamily,
                    FontSize = ArticleFontSize
                });

                Paragraph.Inlines.Add(hyperlink);
            }

            //假使Uri後面有文章內容
            if (match.Index + match.Length < msg.Length)
            {
                int begin_x = CountBlocks(msg, 0, match.Index + match.Length);
                int index = begin_x;
                int color = blocks[begin_x].ForegroundColor;
                
                for (int i = begin_x; i < blocks.Length; i++)
                {
                    Block b = blocks[i];

                    if (color != b.ForegroundColor || i == blocks.Length - 1)
                    {
                        string text = LiPTT.GetString(blocks, index, i - index).Replace('\0', ' ');

                        Run container = new Run()
                        {
                            Text = text,
                            FontSize = ArticleFontSize,
                            FontFamily = ArticleFontFamily,
                            Foreground = GetForegroundBrush(blocks[index]),
                        };

                        Paragraph.Inlines.Add(container);
                        RichTB.Height = RichTB.ActualHeight;
                        index = i;
                        color = b.ForegroundColor;
                    }
                }
            }
            Paragraph.Inlines.Add(new LineBreak());
            RichTB.Height = Paragraph.Inlines.Count * ArticleFontSize;

            if (hyperlinkVisible)
            {
                //截斷RuchTextBlock
                RichTB = null;
                CreateUriView(uri.OriginalString);
            }             
        }

        private void AddEcho(string msg)
        {
            /***
            Echo echo = new Echo();

            str = LiPTT.GetString(RawLines[row], 0, RawLines[row].Length - 13);

            int index = 2;
            int end = index;
            while (str[end] != ':') end++;

            string auth = str.Substring(index, end - index);

            echo.Author = auth.Trim();

            echo.Content = str.Substring(end + 1);

            string time = LiPTT.GetString(RawLines[row], 67, 11);
            //https://msdn.microsoft.com/zh-tw/library/8kb3ddd4(v=vs.110).aspx
            try
            {
                System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                echo.Date = DateTimeOffset.ParseExact(time, "MM/dd HH:mm", provider);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            if (str.StartsWith("推")) echo.Evaluation = Evaluation.推;
            else if (str.StartsWith("噓")) echo.Evaluation = Evaluation.噓;
            else echo.Evaluation = Evaluation.箭頭;
            
            
            /***/
            RichTB = null;
        }

        private void CreateUriView(string url)
        {
            Uri uri = new Uri(url);

            Debug.WriteLine("request: " + uri.OriginalString);
            //http://www.cnblogs.com/jesse2013/p/async-and-await.html
            //***
            if (IsShortCut(uri.Host))
            {
                WebRequest webRequest = WebRequest.Create(url);
                WebResponse webResponse = webRequest.GetResponseAsync().Result;
                uri = webResponse.ResponseUri;
            }
            /***/

            if (IsPictureUri(uri))
            {
                ProgressRing ring = new ProgressRing() { IsActive = true, Width = 55, Height = 55 };
                Grid grid = new Grid() { Width = ViewWidth * 0.8, Height = 0.5625 * ViewWidth * 0.8, Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)) };
                grid.Children.Add(ring);
                Add(grid);

                DownloadPictureTasks.Add(CreateImageView(this.Count - 1, uri));
            }
            else if (uri.Host == "imgur.com")
            {
                string str = uri.OriginalString;

                if (str.IndexOf("imgur.com/a") == -1)
                {
                    Match match = new Regex("imgur.com").Match(str);

                    if (match.Success)
                    {
                        str = str.Insert(match.Index, "i.");
                        str += ".png";
                        Uri new_uri = new Uri(str);

                        ProgressRing ring = new ProgressRing() { IsActive = true, Width = 55, Height = 55 };
                        Grid grid = new Grid() { Width = ViewWidth * 0.8, Height = 0.5625 * ViewWidth * 0.8, Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)) };
                        grid.Children.Add(ring);
                        Add(grid);

                        DownloadPictureTasks.Add(CreateImageView(this.Count - 1, new_uri));
                    }
                }
            }
            else if (IsYoutubeUri(uri))
            {
                //取出youtube的video ID
                string[] query = uri.Query.Split(new char[] { '?', '&' }, StringSplitOptions.RemoveEmptyEntries);
                string youtubeID = "";
                foreach (string s in query)
                {
                    if (s.StartsWith("v"))
                    {
                        youtubeID = s.Substring(s.IndexOf("=") + 1);
                        break;
                    }
                }
                AddYoutubeView(youtubeID);
            }
        }

        private async Task<DownloadResult> CreateImageView(int index, Uri uri)
        {
            Task<BitmapImage> task = LiPTT.ImageCache.GetFromCacheAsync(uri);

            BitmapImage bmp = await task;

            Image img = new Image() { Source = bmp, HorizontalAlignment = HorizontalAlignment.Stretch };

            double ratio = (double)bmp.PixelWidth / bmp.PixelHeight;

            ColumnDefinition c1, c2, c3;

            double space = 0.2; //也就是佔總寬的80%

            if (bmp.PixelWidth < ViewWidth * (1 - space))
            {
                c1 = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
                c2 = new ColumnDefinition { Width = new GridLength(bmp.PixelWidth, GridUnitType.Pixel) };
                c3 = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
            }
            else if (ratio >= 1.0)
            {
                c1 = new ColumnDefinition { Width = new GridLength(space / 2.0, GridUnitType.Star) };
                c2 = new ColumnDefinition { Width = new GridLength((1 - space), GridUnitType.Star) };
                c3 = new ColumnDefinition { Width = new GridLength(space / 2.0, GridUnitType.Star) };
            }
            else
            {
                double x = ratio * (1 - space) / 2.0;
                c1 = new ColumnDefinition { Width = new GridLength(space / 2.0 + x, GridUnitType.Star) };
                c2 = new ColumnDefinition { Width = new GridLength((1 - space) * ratio, GridUnitType.Star) };
                c3 = new ColumnDefinition { Width = new GridLength(space / 2.0 + x, GridUnitType.Star) };
            }


            Grid ImgGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };

            ImgGrid.ColumnDefinitions.Add(c1);
            ImgGrid.ColumnDefinitions.Add(c2);
            ImgGrid.ColumnDefinitions.Add(c3);

            HyperlinkButton button = new HyperlinkButton()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Content = img,
                NavigateUri = uri,
            };

            button.SetValue(Grid.ColumnProperty, 1);
            ImgGrid.Children.Add(button);

            return new DownloadResult() { Index = index, Item = ImgGrid };
        }

        private void AddYoutubeView(string youtubeID, double width = 0, double height = 0)
        {
            double w = width == 0 ? ViewWidth : width;
            double h = height == 0 ? w * 0.5625 : height;

            WebView wv = new WebView() { Tag = "YoutubeWebView", Width = w, Height = h, DefaultBackgroundColor = Colors.Black };

            Grid grid = new Grid() { Width = w, Height = h, Tag = "Youtube", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            ProgressRing progress = new ProgressRing() { IsActive = true, Width = 50, Height = 50, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Colors.Red) };
            string script = GetYoutubeScript(youtubeID, w, h);

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
            Add(grid);
            wv.Navigate(new Uri("ms-appx-web:///Templates/youtube.html"));
        }


        private bool IsPictureUri(Uri uri)
        {
            string origin = uri.OriginalString;
            if (origin.EndsWith(".jpg") ||
                origin.EndsWith(".png") ||
                origin.EndsWith(".png") ||
                origin.EndsWith(".gif") ||
                origin.EndsWith(".bmp") ||
                origin.EndsWith(".tiff") ||
                origin.EndsWith(".ico"))
            {
                return true;
            }

            return false;
        }

        private bool IsYoutubeUri(Uri uri)
        {
            if (uri.Host == "www.youtube.com" || uri.Host == "youtu.be")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool IsShortCut(string host)
        {
            if (ShortCutUrlSet.Contains(host)) return true;
            else return false;
        }

        private bool IsEchoes(string msg)
        {
            if (msg.StartsWith("推") || msg.StartsWith("噓") || msg.StartsWith("→"))
            {
                Match match = new Regex(@"[\u63a8\u5653\u2192]{1}\s+[A-Za-z0-9 ]+:").Match(msg);

                if (match.Success)
                {
                    
                    match = new Regex(@"\d{2}/\d{2} \d{2}:\d{2} *\Z").Match(msg.Replace('\0', ' ').Trim());
                    if (match.Success) return true;
                }
            }
            return false;
        }

        private string GetYoutubeScript(string YoutubeID, double width, double height)
        {
            string script = "function onYouTubeIframeAPIReady() { var player = new YT.Player('player', { height: '@Height', width: '@Width', videoId: '@YoutubeID'}); }";
            script = script.Replace("@YoutubeID", YoutubeID);
            script = script.Replace("@Width", ((int)Math.Round(width)).ToString());
            script = script.Replace("@Height", ((int)Math.Round(height)).ToString());
            return script;
        }

        private int CountBlocks(string msg, int index, int length)
        {
            int b = 0;
            for (int k = index; k < index + length; k++)
            {
                if (msg[k] < 0x7F) b++;
                else b += 2;
            }
            return b;
        }

        private Bound ReadLineBound(string msg)
        {
            Bound bound = new Bound();
            Regex regex = new Regex(@"\([\d\s]+%\)");
            Match match = regex.Match(msg);

            if (match.Success)
            {
                string percent = msg.Substring(match.Index + 1, match.Length - 3);
                bound.Percent = Convert.ToInt32(percent);
            }

            regex = new Regex(@"第\s[\d~]+\s行");
            match = regex.Match(msg, match.Length);

            if (match.Success)
            {
                string s = msg.Substring(match.Index + 2, match.Length - 4);
                string[] a = s.Split('~');
                bound.Begin = Convert.ToInt32(a[0]);
                bound.End = Convert.ToInt32(a[1]);
            }

            return bound;
        }

        private SolidColorBrush GetForegroundBrush(Block b)
        {
            switch (b.ForegroundColor)
            {
                case 30:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0)) :
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
                case 31:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0x00)) :
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0x00, 0x00));
                case 32:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0x00)) :
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xC0, 0x00));
                case 33:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00)) :
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0x00));
                case 34:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0xFF)) :
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0xC0));
                case 35:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0xFF)) :
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0x00, 0xC0));
                case 36:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0xFF)) :
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xC0, 0xC0));
                case 37:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)) :
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xD0, 0xD0, 0xD0));
                default:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0));
            }
        }
    }
}
