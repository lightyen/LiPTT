using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using System.Collections.ObjectModel;
using Windows.Foundation;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.IO;
using Windows.System;
using Windows.Web.Http;
using Windows.UI;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml;

namespace LiPTT
{
    public partial class ArticleContentCollection : ObservableCollection<object>, ISupportIncrementalLoading
    {
        PTT ptt;

        public Task DownloadTask;

        public CancellationTokenSource DownloadCancellationTokenSource;

        public bool InitialLoaded { get; private set; }

        public Article ArticleTag
        {
            get; set;
        }

        protected override void ClearItems()
        {
            InitialLoaded = false;
            RichTextBlock = null;
            ParsedLine = 0;
            RawLines.Clear();
            base.ClearItems();
        }

        public List<Block[]> RawLines { get; set; } //文章生肉串

        private List<Task<DownloadResult>> DownloadImageTasks { get; set; }

        /// <summary>
        /// 已過濾的行數(不包含標題頭)
        /// </summary>
        private int ParsedLine;

        private const double ArticleFontSize = 24.0;

        private FontFamily ArticleFontFamily;

        private RichTextBlock RichTextBlock;

        private Paragraph Paragraph;

        public delegate void FullScreenEventHandler(Grid sender, FullScreenEventArgs e);

        public event FullScreenEventHandler FullScreenEntered;
        public event FullScreenEventHandler FullScreenExited;
        public event EventHandler ItemsUpdatd;

        private SemaphoreSlim Semaphore;

        object videorunkey = new object();

        private int videorun;

        public int VideoRun
        {
            get
            {
                return videorun;
            }
            set
            {
                lock (videorunkey)
                {
                    videorun = value;
                }
            }
        }

        private uint Floor = 1;

        private Binding ImageButtonFontSizeBinding;
        private Binding SmallFontSizeBinding;
        private Binding NormalFontSizeBinding;
        private Binding EchoFontSizeBinding;

        public ArticleContentCollection()
        {
            ptt = Application.Current.Resources["PTT"] as PTT;
            Semaphore = new SemaphoreSlim(0, 1);
            RawLines = new List<Block[]>();
            DownloadImageTasks = new List<Task<DownloadResult>>();
            CollectionChanged += (a, b) =>
            {
                ItemsUpdatd?.Invoke(this, new EventArgs());
            };

            ImageButtonFontSizeBinding = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["ArticleFontSizeConverter"] as ArticleFontSizeConverter,
                ConverterParameter = ArticleFontSize - 4,
            };

            SmallFontSizeBinding = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["ArticleFontSizeConverter"] as ArticleFontSizeConverter,
                ConverterParameter = ArticleFontSize - 8,
            };

            NormalFontSizeBinding = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["ArticleFontSizeConverter"] as ArticleFontSizeConverter,
                ConverterParameter = ArticleFontSize,
            };

            EchoFontSizeBinding = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["ArticleFontSizeConverter"] as ArticleFontSizeConverter,
                ConverterParameter = ArticleFontSize - 2,
            };

            var action = PTT.RunInUIThread(() =>
            {
                ArticleFontFamily = new FontFamily("Noto Sans Mono CJK TC");
            });
        }

        uint LoadMoreItemsResult;

        private bool loading;
        private object loadingLocker = new object();
        public bool Loading
        {
            get
            {
                return loading;
            }
            set
            {
                lock(loadingLocker)
                {
                    loading = value;
                }
            }
        }

        public bool HasMoreItems
        {
            get
            {
                if (Loading) return false;

                if (InitialLoaded && ptt.HasMoreArticleContent)
                {
                    return true;
                }   
                else
                    return false;
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
        }

        ///目前已知Fall creators update(16299)的問題，但在15063不會有的現象:
        ///如果RichTextBlock內容積太多會發生記憶體錯誤
        ///目前暫時做法是最高讀取16頁就flush出去 (因為我讀到17頁就GG了)
        const int MaxLoadPage = 16;
        private object MaxLoadPagelocker = new object();
        private int pageCount;
        private int LoadPageCount
        {
            get
            {
                return pageCount;
            }
            set
            {
                lock (MaxLoadPagelocker)
                {
                    pageCount = value;
                }
            }
}
        private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint count)
        {
            ptt.ArticleContentUpdated += ArticleContentUpdated;
            Loading = true;
            LoadMoreItemsResult = 0;
            ptt.GetArticleContent();
            await Semaphore.WaitAsync();
            Loading = false;

            return new LoadMoreItemsResult { Count = LoadMoreItemsResult };
        }

        private async void ArticleContentUpdated(object sender, ArticleContentUpdatedEventArgs e)
        {
            foreach (var line in e.Lines)
            {
                RawLines.Add(line);
            }

            await PTT.RunInUIThread(() => {

                Parse();
                LoadPageCount++;

                if (LoadPageCount >= MaxLoadPage || e.Bound.Progress == 100)
                {
                    FlushTextBlock();
                }
            });

            if (LoadPageCount >= MaxLoadPage || e.Bound.Progress == 100)
            {
                ptt.ArticleContentUpdated -= ArticleContentUpdated;
                Semaphore.Release();
            }
            else if (LoadMoreItemsResult == 0)
            {
                ptt.GetArticleContent();
            }
            else
            {
                ptt.ArticleContentUpdated -= ArticleContentUpdated;
                Semaphore.Release();
            }
        }

        public void BeginLoad(ArticleContentUpdatedEventArgs e)
        {
            Clear();

            ArticleTag = e.Article;

            if (ArticleTag.HasHeader)
            {
                for (int i = 4; i < e.Lines.Count; i++)
                {
                    RawLines.Add(e.Lines[i]);
                }
            }
            else
            {
                foreach (var line in e.Lines)
                {
                    RawLines.Add(line);
                }
            }

            Parse();

            LoadPageCount = 1;

            if (e.Bound.Progress == 100)
            {
                FlushTextBlock();
            }
            
            InitialLoaded = true;
        }

        public void Parse()
        {
            for (int row = ParsedLine; row < RawLines.Count; row++)
            {
                string str = PTT.GetString(RawLines[row]);

                if (str.StartsWith("※"))
                {
                    PrepareAddText();
                    Run run = new Run()
                    {
                        Text = str,
                        FontFamily = ArticleFontFamily,
                        Foreground = new SolidColorBrush(Colors.Green),
                    };
                    BindingOperations.SetBinding(run, TextElement.FontSizeProperty, SmallFontSizeBinding);

                    Paragraph.Inlines.Add(run);
                    Paragraph.Inlines.Add(new LineBreak());
                }
                else if (IsEchoes(str))
                {
                    AddEcho(RawLines[row]);
                }
                else
                {
                    string eee = str.Trim(new char[] { '\0', ' ' });
                    if (eee.StartsWith("--"))
                    {
                        Floor = 1;
                    }

                    AddLine(RawLines[row], str);
                }
            }

            ParsedLine = RawLines.Count;

            DownloadCancellationTokenSource = new CancellationTokenSource();
            var token = DownloadCancellationTokenSource.Token;


            DownloadTask = Task.Run(async () => {

                if (DownloadImageTasks.Count > 0)
                {
                    while (DownloadImageTasks.Count > 0)
                    {
                        var firstFinishedTask = await Task.WhenAny(DownloadImageTasks);

                        token.ThrowIfCancellationRequested();

                        var item = firstFinishedTask.Result.Item;

                        if (item != null)
                        {
                            await PTT.RunInUIThread(() => {
                                try
                                {
                                    this[firstFinishedTask.Result.Index] = item;
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                    Debug.WriteLine(string.Format("DownloadImageTasks: index {0} OutOfRangeException", firstFinishedTask.Result.Index));
                                }
                            });
                        }
                        DownloadImageTasks.Remove(firstFinishedTask);
                    }
                }
            }, token);
        }

        public void AddLine(Block[] blocks, string str)
        {
            Match match;
            Regex regex = new Regex(PTT.HttpRegex);
            int index = 0; //for string
            int i = 0; //for block
            PrepareAddText();

            List<PTTUri> ViewUri = new List<PTTUri>();

            while ((match = regex.Match(str, index)).Success)
            {
                if (index < match.Index)
                {
                    string text = str.Substring(index, match.Index - index);
                    int _count = PTTEncoding.GetEncoding().GetByteCount(text);
                    AddText(blocks, i, _count);
                    i += _count;
                }

                int count = PTTEncoding.GetEncoding().GetByteCount(match.ToString());

                try
                {
                    PTTUri uri = new PTTUri(match.ToString());

                    if (uri.IsUriVisible)
                    {
                        AddHyperlink(uri);
                    }
                    
                    if (uri.IsMediaUri)
                    {
                        ViewUri.Add(uri);
                    } 
                }
                catch (UriFormatException)
                {
                    AddText(blocks, i, count);
                }

                i += count;

                index = match.Index + match.Length;
            }

            if (index < str.Length)
            {
                string text = str.Substring(index, str.Length - index);
                int _count = PTTEncoding.GetEncoding().GetByteCount(text);
                AddText(blocks, i, _count);
                i += _count;
            }

            Paragraph.Inlines.Add(new LineBreak());

            if (ViewUri.Count > 0)
            {
                FlushTextBlock();

                foreach (var view_uri in ViewUri)
                {
                    AddUriView(view_uri);
                }
            }
        }

        private void PrepareAddText()
        {
            if (RichTextBlock == null)
            {
                LoadPageCount = 0;
                RichTextBlock = new RichTextBlock()
                {
                    IsTextSelectionEnabled = true,
                    IsRightTapEnabled = false,
                };

                SettingProperty setting = Application.Current.Resources["SettingProperty"] as SettingProperty;

                Paragraph = new Paragraph();

                if (setting.LineSpaceDisabled)
                {
                    Paragraph.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                    BindingOperations.SetBinding(Paragraph, Paragraph.LineHeightProperty, NormalFontSizeBinding);
                }
                else
                {
                    //Paragraph.LineStackingStrategy = LineStackingStrategy.MaxHeight;
                }

                RichTextBlock.Blocks.Add(Paragraph);
            }
        }

        private void FlushTextBlock()
        {
            if (RichTextBlock != null)
            {
                Add(RichTextBlock);
                LoadMoreItemsResult++;
            }

            RichTextBlock = null;
        }

        private void AddText(Block[] blocks, int index, int count)
        {
            int color = blocks[index].ForegroundColor;
            int idx = index;
            for (int i = index; i < index + count; i++)
            {
                Block b = blocks[i];
                if (color != b.ForegroundColor)
                {
                    string text = PTT.GetString(blocks, idx, i - idx);
                    //目前UWP沒有可以畫半個字的API，而且也沒有可以把不同景色的文字放在同一個TextBlock的功能
                    //因為選字的時候，只有在同一個TextBlock底下才有可能
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
                    Run run = new Run()
                    {
                        Text = text.Replace('\0', ' '),
                        FontFamily = ArticleFontFamily,
                        Foreground = GetForegroundBrush(blocks[idx]),
                    };
                    BindingOperations.SetBinding(run, TextElement.FontSizeProperty, NormalFontSizeBinding);

                    /***/
                    Paragraph.Inlines.Add(run);
                    idx = i;
                    color = b.ForegroundColor;
                }

                if (i == index + count - 1)
                {
                    string text = PTT.GetString(blocks, idx, index + count - idx);
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
                    Run run = new Run()
                    {
                        Text = text.Replace('\0', ' '),
                        FontFamily = ArticleFontFamily,
                        Foreground = GetForegroundBrush(blocks[idx]),
                    };
                    /***/
                    BindingOperations.SetBinding(run, TextElement.FontSizeProperty, NormalFontSizeBinding);
                    Paragraph.Inlines.Add(run);
                    color = b.ForegroundColor;
                    break;
                }
            }
        }

        private void AddHyperlink(PTTUri uri)
        {
            //插入超連結
            try
            {
                Hyperlink hyperlink = new Hyperlink()
                {
                    NavigateUri = uri,
                    UnderlineStyle = UnderlineStyle.Single,
                    //Foreground = new SolidColorBrush(Colors.Gray),
                };
                Run run = new Run()
                {
                    Text = uri.OriginalString,
                    FontFamily = ArticleFontFamily,
                };
                hyperlink.Inlines.Add(run);
                BindingOperations.SetBinding(run, TextElement.FontSizeProperty, NormalFontSizeBinding);

                if (uri.IsShort && Application.Current.Resources["SettingProperty"] is SettingProperty setting && setting.OpenShortUri)
                {
                    PTTUri u = uri.Expand();
                    AddExpandUriTooltip(hyperlink, u.OriginalString);
                }

                Paragraph.Inlines.Add(hyperlink);
            }
            catch
            {
                Run run = new Run()
                {
                    Text = uri.OriginalString,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontFamily = ArticleFontFamily,
                    TextDecorations = Windows.UI.Text.TextDecorations.Underline,
                };
                Paragraph.Inlines.Add(run);
                BindingOperations.SetBinding(run, TextElement.FontSizeProperty, NormalFontSizeBinding);
            }
        }

        private void AddEcho(Block[] block)
        {
            FlushTextBlock();

            Match match;
            Regex regex;

            Echo echo = new Echo() { Floor = Floor++ };


            //IP
            int ip_start = 51;
            int ip_space = 16;
            string ip = PTT.GetString(block, ip_start, ip_space);
            regex = new Regex(PTT.ValidIpAddressRegex);
            match = regex.Match(ip);

            if (!match.Success)
            {
                ip_start += 6;
                ip = PTT.GetString(block, ip_start, ip_space);
                match = regex.Match(ip);
                if (!match.Success)
                {
                    ip_space = 0;
                }
                else
                {
                    ip = match.ToString();
                }
            }
            else
            {
                ip = match.ToString();
            }

            /***
            if (!ptt.CurrentBoard.IPVisible)
            {
                ip = "";
                ip_space = 0;
            }
            /***/

            //自訂日期和時間格式字串 https://msdn.microsoft.com/zh-tw/library/8kb3ddd4(v=vs.110).aspx
            System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
            int time_start = 67;
            int time_space = 12;
            bool shortTime = false;
            string time = PTT.GetString(block, time_start, time_space).Replace('\0', ' ');

            try
            {
                echo.Date = DateTimeOffset.ParseExact(time, "MM/dd HH:mm ", provider);
                echo.DateFormated = true;
            }
            catch (FormatException)
            {
                try
                {
                    time_start += 6;
                    time_space -= 6;
                    time = PTT.GetString(block, time_start, time_space).Replace('\0', ' ');
                    echo.Date = DateTimeOffset.ParseExact(time, "MM/dd ", provider);
                    shortTime = true;
                    echo.DateFormated = true;
                }
                catch (FormatException)
                {
                    Debug.WriteLine(string.Format("推文時間格式不正確:\"{0}\"", time));
                }
            }

            string str = PTT.GetString(block, 0, ptt.Screen.Width - time_space - ip_space - 1).Replace('\0', ' ').Trim();

            int end = str.IndexOf(':');

            string auth = str.Substring(2, end - 2);

            echo.Author = auth.Trim();

            echo.Content = str.Substring(end + 1);

            if (str.StartsWith("推")) echo.Evaluation = Evaluation.推;
            else if (str.StartsWith("噓")) echo.Evaluation = Evaluation.噓;
            else echo.Evaluation = Evaluation.箭頭;

            //////////////////////////////////////////////
            Binding binding0 = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["EchoGridLengthConverter"] as EchoGridLengthConverter,
                ConverterParameter = 0,
            };

            Binding binding1 = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["EchoGridLengthConverter"] as EchoGridLengthConverter,
                ConverterParameter = 1,
            };

            Binding binding2 = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["EchoGridLengthConverter"] as EchoGridLengthConverter,
                ConverterParameter = 2,
            };

            Binding binding3 = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["EchoGridLengthConverter"] as EchoGridLengthConverter,
                ConverterParameter = 3,
            };

            Grid grid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0) };

            ColumnDefinition c0 = new ColumnDefinition();
            BindingOperations.SetBinding(c0, ColumnDefinition.WidthProperty, binding0);
            ColumnDefinition c1 = new ColumnDefinition();
            BindingOperations.SetBinding(c1, ColumnDefinition.WidthProperty, binding1);
            ColumnDefinition c2 = new ColumnDefinition();
            BindingOperations.SetBinding(c2, ColumnDefinition.WidthProperty, binding2);
            ColumnDefinition c3 = new ColumnDefinition();
            BindingOperations.SetBinding(c3, ColumnDefinition.WidthProperty, binding3);

            grid.ColumnDefinitions.Add(c0);
            grid.ColumnDefinitions.Add(c1);
            grid.ColumnDefinitions.Add(c2);
            grid.ColumnDefinitions.Add(c3);

            Grid g0 = new Grid();
            g0.SetValue(Grid.ColumnProperty, 0);
            Grid g1 = new Grid();
            g1.SetValue(Grid.ColumnProperty, 1);
            Grid g2 = new Grid();
            g2.SetValue(Grid.ColumnProperty, 2);
            Grid g3 = new Grid();
            g3.SetValue(Grid.ColumnProperty, 3);
            
            //推、噓//////////////////////////////////////////
            SolidColorBrush EvalColor;

            switch (echo.Evaluation)
            {
                case Evaluation.推:
                    EvalColor = new SolidColorBrush(Colors.Yellow);
                    break;
                case Evaluation.噓:
                    EvalColor = new SolidColorBrush(Colors.Red);
                    break;
                default:
                    EvalColor = new SolidColorBrush(Colors.Purple);
                    break;
            }

            var tb0 = new TextBlock()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Text = str[0].ToString(),
                Foreground = EvalColor
            };
            BindingOperations.SetBinding(tb0, TextBlock.FontSizeProperty, EchoFontSizeBinding);
            g0.Children.Add(tb0);

            //推文ID////高亮五樓/////高亮原PO/////////////////////
            SolidColorBrush authorColor = new SolidColorBrush(Colors.LightSalmon);

            if (ArticleTag.Author == echo.Author)
            {
                authorColor = new SolidColorBrush(Colors.LightBlue);
            }

            var tb1 = new TextBlock() {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = echo.Author,
                Foreground = authorColor
            };
            BindingOperations.SetBinding(tb1, TextBlock.FontSizeProperty, EchoFontSizeBinding);
            g1.Children.Add(tb1);

            //推文內容////////////////////////////////////////////
            regex = new Regex(PTT.HttpRegex);
            int index = 0; //for string
            StackPanel panel = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
            };
            List<PTTUri> ViewUri = new List<PTTUri>();
            //UIElement element = null;
            
            while ((match = regex.Match(echo.Content, index)).Success)
            {
                if (index < match.Index)
                {
                    string text = echo.Content.Substring(index, match.Index - index);
                    var tb = new TextBlock()
                    {
                        Margin = new Thickness(0),
                        IsTextSelectionEnabled = true,
                        Foreground = new SolidColorBrush(Colors.Gold),
                        Text = text,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    tb.SetBinding(TextBlock.FontSizeProperty, EchoFontSizeBinding);

                    panel.Children.Add(tb);

                    //if (element != null)
                    //    RelativePanel.SetRightOf(tb, element);
                    //element = tb;
                }

                try
                {
                    string text = match.ToString();

                    PTTUri uri = new PTTUri(text);

                    if (uri.IsMediaUri)
                    {
                        ViewUri.Add(uri);
                    }

                    var hb = new HyperlinkButton()
                    {
                        NavigateUri = uri,
                        Padding = new Thickness(0),
                        Content = new TextBlock() { Text = text },
                    };
                    BindingOperations.SetBinding(hb, Control.FontSizeProperty, EchoFontSizeBinding);

                    if (uri.IsShort && Application.Current.Resources["SettingProperty"] is SettingProperty setting && setting.OpenShortUri)
                    {
                        PTTUri u = uri.Expand();
                        AddExpandUriTooltip(hb, u.OriginalString);
                    }

                    panel.Children.Add(hb);

                    //if (element != null)
                    //    RelativePanel.SetRightOf(hb, element);
                    //element = hb;
                }
                catch (UriFormatException)
                {
                    string text = echo.Content.Substring(index, match.Index - index);
                    var tb = new TextBlock()
                    {
                        IsTextSelectionEnabled = true,
                        Foreground = new SolidColorBrush(Colors.Gold),
                        Text = text,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    BindingOperations.SetBinding(tb, TextBlock.FontSizeProperty, EchoFontSizeBinding);

                    panel.Children.Add(tb);
                    //if (element != null)
                    //    RelativePanel.SetRightOf(tb, element);
                    //element = tb;
                    
                }

                index = match.Index + match.Length;
            }

            if (index < echo.Content.Length)
            {
                string text = echo.Content.Substring(index, echo.Content.Length - index);
                var tb = new TextBlock()
                {
                    IsTextSelectionEnabled = true,
                    Foreground = new SolidColorBrush(Colors.Gold),
                    Text = text,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                BindingOperations.SetBinding(tb, TextBlock.FontSizeProperty, EchoFontSizeBinding);
                panel.Children.Add(tb);
                //if (element != null)
                //    RelativePanel.SetRightOf(tb, element);
                //element = tb;
                
            }

            g2.Children.Add(panel);

            //推文時間////////////////////////////////////////////
            if (echo.DateFormated)
            {
                var tb = new TextBlock()
                {
                    Margin = new Thickness(0, 0, 20, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Wheat),
                    Text = shortTime ? echo.Date.ToString("MM/dd") : echo.Date.ToString("MM/dd HH:mm")
                };
                BindingOperations.SetBinding(tb, TextBlock.FontSizeProperty, EchoFontSizeBinding);
                g3.Children.Add(tb);
            }

            //////////////////////////////////////////////
            grid.Children.Add(g0);
            grid.Children.Add(g1);
            grid.Children.Add(g2);
            grid.Children.Add(g3);

            /***
            Button button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
            };
            /***/

            switch (echo.Evaluation)
            {
                case Evaluation.推:
                    grid.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x50, 0x35, 0x00));
                    break;
                case Evaluation.噓:
                    grid.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x50, 0x00, 0x00));
                    break;
                default:
                    grid.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0x37, 0x2D));
                    break;
            }

            //button.Content = grid;
            ListView list = new ListView()
            {
                IsItemClickEnabled = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            
            //***
            list.Items.Add(new ListViewItem()
            {
                Content = grid,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
            });
            /***/
            ToolTip toolTip = new ToolTip() { Content = string.Format("{0}樓", echo.Floor) };
            ToolTipService.SetToolTip(list, toolTip);
            Add(list);
            LoadMoreItemsResult++;

            if (ViewUri.Count > 0)
            {
                foreach (var uri in ViewUri)
                {
                    AddUriView(uri);
                }
            }
        }

        private void AddUriView(PTTUri uri)
        {
            Binding bindingWidth = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["GridWidthConverter"] as GridWidthConverter,
            };

            Binding bindingHeight = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["GridHeightConverter"] as GridHeightConverter,
            };

            Binding ringBinding = new Binding { ElementName = "proxy", Path = new PropertyPath("ActualWidthValue"), Converter = Application.Current.Resources["ImageRingRatioConverter"] as ImageRingRatioConverter };

            if (uri.IsPicture)
            {
                ProgressRing ring = new ProgressRing() { Foreground = new SolidColorBrush(Colors.Aquamarine) };
                ring.SetBinding(FrameworkElement.WidthProperty, ringBinding);
                ring.SetBinding(FrameworkElement.HeightProperty, ringBinding);

                Grid grid = new Grid() { Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)) };
                grid.SetBinding(FrameworkElement.WidthProperty, bindingWidth);
                grid.SetBinding(FrameworkElement.HeightProperty, bindingHeight);
                grid.Children.Add(ring);
                Add(grid);
                LoadMoreItemsResult++;

                if (Application.Current.Resources["SettingProperty"] is SettingProperty setting && setting.AutoLoad)
                {
                    ring.IsActive = true;
                    Debug.WriteLine("request: " + uri.OriginalString);
                    DownloadImageTasks.Add(DownloadImage(Count - 1, uri.PictureUri));
                }
                else
                {
                    ring.IsActive = false;
                    AddImageView(Count - 1, ring, uri);
                }

            }          
            else if (uri.IsYoutube)
            {
                AddVideoView("youtube", uri.YoutubeID, uri);
            }
            else if (uri.IsTwitch)
            {
                AddVideoView("twitch", uri.TwitchID, uri);
            }
        }

        private void AddImageView(int index, ProgressRing ring, PTTUri uri)
        {
            Button button = new Button
            {
                Content = "載入圖片",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            ToolTip toolTip = new ToolTip() { Content = uri.OriginalString };
            ToolTipService.SetToolTip(button, toolTip);

            bool click = false;

            if (this[index] is Grid grid)
            {
                Binding _bindingWidth = new Binding
                {
                    ElementName = "proxy",
                    Path = new PropertyPath("ActualWidthValue"),
                    Converter = Application.Current.Resources["GridWidthConverter"] as GridWidthConverter,
                };

                Binding _bindingHeight = new Binding
                {
                    ElementName = "proxy",
                    Path = new PropertyPath("ActualWidthValue"),
                    Converter = Application.Current.Resources["GridHeightConverter"] as GridHeightConverter,
                };

                Border _border = new Border()
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)),
                };
                _border.SetBinding(FrameworkElement.WidthProperty, _bindingWidth);
                _border.SetBinding(FrameworkElement.HeightProperty, _bindingHeight);

                Binding bindingButtonWidth = new Binding
                {
                    ElementName = "proxy",
                    Path = new PropertyPath("ActualWidthValue"),
                    Converter = Application.Current.Resources["ImageButtonWidthConverter"] as ImageButtonWidthConverter,
                };

                Binding bindingButtonHeight = new Binding
                {
                    ElementName = "proxy",
                    Path = new PropertyPath("ActualWidthValue"),
                    Converter = Application.Current.Resources["ImageButtonHeightConverter"] as ImageButtonHeightConverter,
                };

                button.SetBinding(Button.WidthProperty, bindingButtonWidth);
                button.SetBinding(Button.HeightProperty, bindingButtonHeight);
                button.SetBinding(Button.FontSizeProperty, ImageButtonFontSizeBinding);

                _border.Child = button;
                grid.Children.Add(_border);
            }

            button.Click += async (a, b) =>
            {
                if (click) return;

                click = true;
                ring.IsActive = true;
                button.Visibility = Visibility.Collapsed;
                Debug.WriteLine("request: " + uri.OriginalString);
                Grid ImgGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch, };
                BitmapImage bmp = await LiPTT.ImageCache.GetFromCacheAsync(uri.PictureUri);
                Image img = new Image() { HorizontalAlignment = HorizontalAlignment.Stretch };

                if (bmp != null)
                {
                    img.Source = bmp;
                    Windows.Graphics.Imaging.BitmapSize bitmapSize = new Windows.Graphics.Imaging.BitmapSize { Width = (uint)bmp.PixelWidth, Height = (uint)bmp.PixelHeight };

                    Binding bindingSide = new Binding
                    {
                        ElementName = "proxy",
                        Path = new PropertyPath("ActualWidthValue"),
                        Converter = Application.Current.Resources["GridLengthSideConverter"] as GridLengthSideConverter,
                        ConverterParameter = bitmapSize,
                    };

                    Binding bindingCenter = new Binding
                    {
                        ElementName = "proxy",
                        Path = new PropertyPath("ActualWidthValue"),
                        Converter = Application.Current.Resources["GridLengthCenterConverter"] as GridLengthCenterConverter,
                        ConverterParameter = bitmapSize,
                    };

                    ColumnDefinition c1 = new ColumnDefinition(), c2 = new ColumnDefinition(), c3 = new ColumnDefinition();
                    BindingOperations.SetBinding(c1, ColumnDefinition.WidthProperty, bindingSide);
                    BindingOperations.SetBinding(c2, ColumnDefinition.WidthProperty, bindingCenter);
                    BindingOperations.SetBinding(c3, ColumnDefinition.WidthProperty, bindingSide);

                    ImgGrid.ColumnDefinitions.Add(c1);
                    ImgGrid.ColumnDefinitions.Add(c2);
                    ImgGrid.ColumnDefinitions.Add(c3);

                    HyperlinkButton hyperlinkButton = new HyperlinkButton()
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Content = img,
                        NavigateUri = uri,
                    };

                    hyperlinkButton.SetValue(Grid.ColumnProperty, 1);
                    ImgGrid.Children.Add(hyperlinkButton);
                }
                else
                {
                    Binding bindingWidth = new Binding
                    {
                        ElementName = "proxy",
                        Path = new PropertyPath("ActualWidthValue"),
                        Converter = Application.Current.Resources["GridWidthConverter"] as GridWidthConverter,
                    };

                    Binding bindingHeight = new Binding
                    {
                        ElementName = "proxy",
                        Path = new PropertyPath("ActualWidthValue"),
                        Converter = Application.Current.Resources["GridHeightConverter"] as GridHeightConverter,
                    };

                    Border border = new Border()
                    {
                        Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)),
                    };
                    border.SetBinding(FrameworkElement.WidthProperty, bindingWidth);
                    border.SetBinding(FrameworkElement.HeightProperty, bindingHeight);

                    TextBlock textBlock = new TextBlock()
                    {
                        Text = "圖片下載失敗",
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                    };
                    BindingOperations.SetBinding(textBlock, TextBlock.FontSizeProperty, NormalFontSizeBinding);
                    border.Child = textBlock;
                    ImgGrid.Children.Add(border); 
                }

                this[index] = ImgGrid;
            };
        }

        private async Task<DownloadResult> DownloadImage(int index, Uri uri)
        {
            Task<BitmapImage> task = LiPTT.ImageCache.GetFromCacheAsync(uri);

            BitmapImage bmp = await task;

            Image img = new Image() { HorizontalAlignment = HorizontalAlignment.Stretch };

            Grid ImgGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };

            if (bmp != null)
            {
                img.Source = bmp;
                Windows.Graphics.Imaging.BitmapSize bitmapSize = new Windows.Graphics.Imaging.BitmapSize { Width = (uint)bmp.PixelWidth, Height = (uint)bmp.PixelHeight };

                Binding bindingSide = new Binding
                {
                    ElementName = "proxy",
                    Path = new PropertyPath("ActualWidthValue"),
                    Converter = Application.Current.Resources["GridLengthSideConverter"] as GridLengthSideConverter,
                    ConverterParameter = bitmapSize,
                };

                Binding bindingCenter = new Binding
                {
                    ElementName = "proxy",
                    Path = new PropertyPath("ActualWidthValue"),
                    Converter = Application.Current.Resources["GridLengthCenterConverter"] as GridLengthCenterConverter,
                    ConverterParameter = bitmapSize,
                };

                ColumnDefinition c1 = new ColumnDefinition(), c2 = new ColumnDefinition(), c3 = new ColumnDefinition();
                BindingOperations.SetBinding(c1, ColumnDefinition.WidthProperty, bindingSide);
                BindingOperations.SetBinding(c2, ColumnDefinition.WidthProperty, bindingCenter);
                BindingOperations.SetBinding(c3, ColumnDefinition.WidthProperty, bindingSide);

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
            }
            else
            {
                Binding bindingWidth = new Binding
                {
                    ElementName = "proxy",
                    Path = new PropertyPath("ActualWidthValue"),
                    Converter = Application.Current.Resources["GridWidthConverter"] as GridWidthConverter,
                };

                Binding bindingHeight = new Binding
                {
                    ElementName = "proxy",
                    Path = new PropertyPath("ActualWidthValue"),
                    Converter = Application.Current.Resources["GridHeightConverter"] as GridHeightConverter,
                };

                Border border = new Border() {
                    Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)),
                };
                border.SetBinding(FrameworkElement.WidthProperty, bindingWidth);
                border.SetBinding(FrameworkElement.HeightProperty, bindingHeight);

                TextBlock textBlock = new TextBlock() {
                    Text = "圖片下載失敗",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                };
                BindingOperations.SetBinding(textBlock, TextBlock.FontSizeProperty, NormalFontSizeBinding);
                border.Child = textBlock;
                ImgGrid.Children.Add(border);
            }

            return new DownloadResult() { Index = index, Item = ImgGrid };
        }

        private void AddVideoView(string tag, string ID, PTTUri uri)
        {
            Grid MainGrid = new Grid() { Tag = tag, HorizontalAlignment = HorizontalAlignment.Stretch, };

            SettingProperty setting = Application.Current.Resources["SettingProperty"] as SettingProperty;

            Button button = new Button {
                Content = "載入影片",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            Binding bindingVideoWidth = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["VideoButtonWidthConverter"] as VideoButtonWidthConverter,
            };

            Binding bindingVideoHeight = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["VideoButtonHeightConverter"] as VideoButtonHeightConverter,
            };

            BindingOperations.SetBinding(button, Button.WidthProperty, bindingVideoWidth);
            BindingOperations.SetBinding(button, Button.HeightProperty, bindingVideoHeight);
            BindingOperations.SetBinding(button, Button.FontSizeProperty, NormalFontSizeBinding);

            ToolTip toolTip = new ToolTip() { Content = uri.OriginalString }; 
            ToolTipService.SetToolTip(button, toolTip);

            ColumnDefinition c1, c2, c3;

            Binding bindingSide = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["WebViewGridLengthSideConverter"] as WebViewGridLengthSideConverter,
            };

            Binding bindingCenter = new Binding
            {
                ElementName = "proxy",
                Path = new PropertyPath("ActualWidthValue"),
                Converter = Application.Current.Resources["WebViewGridLengthCenterConverter"] as WebViewGridLengthCenterConverter,
            };

            c1 = new ColumnDefinition();
            c2 = new ColumnDefinition();
            c3 = new ColumnDefinition();
            BindingOperations.SetBinding(c1, ColumnDefinition.WidthProperty, bindingSide);
            BindingOperations.SetBinding(c2, ColumnDefinition.WidthProperty, bindingCenter);
            BindingOperations.SetBinding(c3, ColumnDefinition.WidthProperty, bindingSide);

            MainGrid.ColumnDefinitions.Add(c1);
            MainGrid.ColumnDefinitions.Add(c2);
            MainGrid.ColumnDefinitions.Add(c3);

            Grid InnerGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };
            InnerGrid.SetValue(Grid.ColumnProperty, 1);
            MainGrid.Children.Add(InnerGrid);

            Grid youtuGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Color.FromArgb(0xff, 0x0A, 0x0A, 0x0A)) };
            Binding binding = new Binding { ElementName = "proxy", Path = new PropertyPath("ActualWidthValue"), Converter = Application.Current.Resources["YoutubeGridHeightConverter"] as YoutubeGridHeightConverter };
            youtuGrid.SetBinding(FrameworkElement.HeightProperty, binding);
            InnerGrid.Children.Add(youtuGrid);

            ProgressRing ring = new ProgressRing() { IsActive = false, Foreground = new SolidColorBrush(Colors.Orange) };
            Binding ringBinding = new Binding { ElementName = "proxy", Path = new PropertyPath("ActualWidthValue"), Converter = Application.Current.Resources["RingRatioConverter"] as RingRatioConverter };
            ring.SetBinding(FrameworkElement.WidthProperty, ringBinding);
            ring.SetBinding(FrameworkElement.HeightProperty, ringBinding);

            WebView webview = new WebView { DefaultBackgroundColor = Colors.Black };

            if (!setting.AutoLoad)
            {
                button.Click += (a, b) =>
                {
                    VideoRun++;
                    switch (tag)
                    {
                        case "youtube":
                            webview.Navigate(new Uri("ms-appx-web:///Templates/youtube/youtube.html"));
                            break;
                        case "twitch":
                            webview.Navigate(new Uri("ms-appx-web:///Templates/twitch/twitch.html"));
                            break;
                    }
                };
            }

            webview.ContainsFullScreenElementChanged += (WebView, args) =>
            {
                if (WebView.ContainsFullScreenElement)
                {
                    FullScreenEntered?.Invoke(youtuGrid, new FullScreenEventArgs(WebView));
                }
                else
                {
                    FullScreenExited?.Invoke(youtuGrid, new FullScreenEventArgs(WebView));
                }
            };
            
            webview.ContentLoading += (a, b) =>
            {
                button.Visibility = Visibility.Collapsed;
                ring.IsActive = true;
                webview.Visibility = Visibility.Collapsed;
            };

            webview.FrameDOMContentLoaded += (a, b) =>
            {
                ring.IsActive = false;
                webview.Visibility = Visibility.Visible;
                VideoRun--;
            };

            webview.ScriptNotify += (c, d) =>
            {
                Debug.WriteLine(string.Format("youtube err: {0}", d.Value));

                if (d.Value == "101" || d.Value == "150")
                {
                    var a = Launcher.LaunchUriAsync(uri);
                }   
            };

            webview.DOMContentLoaded += async (a, b) =>
            {
                try
                {
                    bool autoplay = !setting.AutoLoad;
                    await webview.InvokeScriptAsync("LoadVideoByID", new string[] { ID, autoplay.ToString() });
                    if (tag == "youtube" && uri.YoutubeStartSeconds > 0)
                    {
                        Debug.WriteLine(string.Format("youtube start time: {0}", uri.YoutubeStartSeconds));
                        await webview.InvokeScriptAsync("AddStartSecond", new string[] { uri.YoutubeStartSeconds.ToString() });
                    }
                    
                }
                catch (Exception)
                {
                    if (tag == "youtube")
                    {
                        if (uri.YoutubeStartSeconds > 0)
                            Debug.WriteLine(string.Format("JavaScript Exception: {0} StartSecond={1} AutoPlay={2}", ID, uri.YoutubeStartSeconds, (!setting.AutoLoad).ToString()));
                        else
                            Debug.WriteLine(string.Format("JavaScript Exception: {0} AutoPlay={1}", ID, (!setting.AutoLoad).ToString()));
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("JavaScript Exception: twitchID={0}", ID));
                    }
                }
            };

            youtuGrid.Children.Add(webview);
            youtuGrid.Children.Add(ring);
            if (!setting.AutoLoad)
                youtuGrid.Children.Add(button);

            Add(MainGrid);
            LoadMoreItemsResult++;

            if (setting.AutoLoad)
            {
                VideoRun++;
                switch (tag)
                {
                    case "youtube":
                        webview.Navigate(new Uri("ms-appx-web:///Templates/youtube/youtube.html"));
                        break;
                    case "twitch":
                        webview.Navigate(new Uri("ms-appx-web:///Templates/twitch/twitch.html"));
                        break;
                }
            }
        }

        private bool IsEchoes(string msg)
        {
            if (msg.StartsWith("推") || msg.StartsWith("噓") || msg.StartsWith("→"))
            {
                Match match = new Regex(@"[\u63a8\u5653\u2192]{1}\s+[A-Za-z0-9 ]+:").Match(msg);

                if (match.Success)
                {
                    //match = new Regex(@"\d{2}/\d{2} \d{2}:\d{2} *\Z").Match(msg.Replace('\0', ' ').Trim());
                    //if (match.Success) return true;
                    return true;
                }
            }
            return false;
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

        private void AddExpandUriTooltip(DependencyObject obj, string url)
        {
            TextBlock tb = new TextBlock
            {
                Text = url,
                TextWrapping = TextWrapping.Wrap,
            };

            ToolTip tt = new ToolTip
            {
                Content = tb,
                Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x30, 0x90, 0x90)),
                BorderThickness = new Thickness(0),
                FontSize = 16,
            };
            ToolTipService.SetToolTip(obj, tt);
        }

        private int ReadProgress(string msg)
        {
            Regex regex = new Regex(PTT.BoundRegex);
            Match match = regex.Match(msg);

            if (match.Success)
            {
                string percent = msg.Substring(match.Index + 1, match.Length - 3);
                return Convert.ToInt32(percent);
            }

            return 0;
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

        private SolidColorBrush GetBackgroundBrush(Block b)
        {
            switch (b.BackgroundColor)
            {
                case 40:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
                case 41:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x00, 0x00));
                case 42:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x80, 0x00));
                case 43:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x80, 0x00));
                case 44:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x80));
                case 45:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x00, 0x80));
                case 46:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x80, 0x80));
                case 47:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0));
                default:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
            }
        }

        private ScrollViewer GetScrollViewer(DependencyObject depObj)
        {
            try
            {
                if (depObj is ScrollViewer)
                {
                    return depObj as ScrollViewer;
                }

                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = VisualTreeHelper.GetChild(depObj, i);

                    var result = GetScrollViewer(child);
                    if (result != null)
                    {
                        return result;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    public class FullScreenEventArgs : EventArgs
    {
        public FullScreenEventArgs(WebView webview)
        {
            WebView = webview;
        }

        public WebView WebView
        {
            get; set;
        }
    }
}
