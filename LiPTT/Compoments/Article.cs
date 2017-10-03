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
using Windows.UI;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml;

namespace LiPTT
{
    public class ArticleContentCollection : ObservableCollection<object>, ISupportIncrementalLoading
    {
        private bool more;

        public bool InitialLoaded { get; private set; }

        public Article ArticleTag
        {
            get; set;
        }

        protected override void ClearItems()
        {
            InitialLoaded = false;
            RichTextBlock = null;
            line = 0;
            header = false;
            ParsedLine = 0;
            RawLines.Clear();
            more = false;
            base.ClearItems();
        }

        public List<Block[]> RawLines { get; set; } //文章生肉串

        public List<Task<DownloadResult>> DownloadImageTasks { get; set; }

        private bool header;

        private bool line_bug;

        /// <summary>
        /// 已讀的行數(包含標題頭)
        /// </summary>
        private int line;

        /// <summary>
        /// 已過濾的行數(不包含標題頭)
        /// </summary>
        private int ParsedLine;

        private const double ArticleFontSize = 24.0;

        private FontFamily ArticleFontFamily;

        private RichTextBlock RichTextBlock;

        private Paragraph Paragraph;

        private Bound bound;

        /// <summary>
        /// Load完成。(給ScrollViewer在Load完成後捲到頂部用)
        /// </summary>
        public event EventHandler BeginLoaded;

        public delegate void FullScreenEventHandler(Grid sender, FullScreenEventArgs e);

        public event FullScreenEventHandler FullScreenEntered;
        public event FullScreenEventHandler FullScreenExited;

        private SemaphoreSlim sem = new SemaphoreSlim(0, 1);

        private uint Floor = 1;

        public ArticleContentCollection()
        {
            header = false;
            line = 0;
            RawLines = new List<Block[]>();
            DownloadImageTasks = new List<Task<DownloadResult>>();

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

            var action = LiPTT.RunInUIThread(() =>
            {
                ArticleFontFamily = new FontFamily("Noto Sans Mono CJK TC");
            });
        }

        public bool Loading
        {
            get
            {
                return sem.CurrentCount == 0;
            }
        }

        public bool HasMoreItems
        {
            get
            {
                if (InitialLoaded)
                    return more;
                else
                    return false;
            }
            private set
            {
                more = value;
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
        }

        private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint count)
        {
            if (InitialLoaded)
            {
                await Task.Run(() => {
                    LiPTT.PttEventEchoed += PttUpdated;
                    LiPTT.PageDown();
                });

                await sem.WaitAsync();

                return new LoadMoreItemsResult { Count = (uint)this.Count };
            }
            else
            {
                return new LoadMoreItemsResult { Count = 0 };
            }
        }

        private void PttUpdated(PTTClient client, LiPttEventArgs e)
        {
            LiPTT.PttEventEchoed -= PttUpdated;

            IAsyncAction action;

            if (e.State == PttState.Article)
            {
                bound = ReadLineBound(client.Screen.ToString(23));

                int o = header ? 1 : 0;

                //有些文章bound.End - bound.Begin不等於23，而且也沒到100%，PTT的Bug嗎?
                if (bound.Percent < 100)
                {
                    if (header)
                    {
                        if (bound.Begin < 5)
                        {
                            if (bound.End + o - bound.Begin != 22)
                            {
                                Debug.WriteLine(string.Format("PTT BUG? {0} {1}", bound.Begin, bound.End));
                                line_bug = true;
                            }
                                
                        }
                        else if (bound.End - bound.Begin != 22)
                        {
                            Debug.WriteLine(string.Format("PTT BUG? {0} {1}", bound.Begin, bound.End));
                            line_bug = true;
                        }
                    }
                    else
                    {
                        if (bound.End - bound.Begin != 22)
                        {
                            Debug.WriteLine(string.Format("PTT BUG? {0} {1}", bound.Begin, bound.End));
                            line_bug = true;
                        }
                    }
                }

                if (line_bug)
                {
                    bound.Begin = line + 1;
                }

                for (int i = line - bound.Begin + 1 + (bound.Begin < 5 ? o : 0); i < 23; i++, line++)
                {
                    RawLines.Add(LiPTT.Copy(client.Screen[i]));
                }

                if (line_bug) Debug.WriteLine(string.Format("PTT BUG? ReadLine {0}", RawLines.Count));


                action = LiPTT.RunInUIThread(() =>
                {
                    Parse();
                    AddNewTextBlock();
                    sem.Release();
                });

                if (bound.Percent == 100)
                {
                    more = false;
                }
                else
                    more = true;
            }
        }

        public void BeginLoad(Article article)
        {
            Clear();

            ArticleTag = article;

            IAsyncAction action = null;

            ScreenBuffer screen = LiPTT.Screen;

            bound = ReadLineBound(screen.ToString(23));

            Regex regex;
            Match match;
            string tmps;

            if (bound.Begin == 1)
            {
                line_bug = false;

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

                    //內文標題
                    tmps = screen.ToString(1).Replace('\0', ' ').Trim();
                    match = new Regex(LiPTT.bracket_regex).Match(tmps);
                    if (match.Success)
                    {
                        if (match.Index + match.Length + 1 < tmps.Length)
                        {
                            ArticleTag.InnerTitle = tmps.Substring(match.Index + match.Length + 1);
                        }
                    }
                    else
                    {
                        if (tmps.StartsWith("標題 "))
                            ArticleTag.InnerTitle = tmps.Substring(3);
                        else
                            ArticleTag.InnerTitle = tmps;
                    }

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
                if (bound.Percent < 100 && bound.End - (header ? 0 : 1) - bound.Begin != 21)
                {
                    Debug.WriteLine(string.Format("PTT BUG? {0} {1}", bound.Begin, bound.End));
                    line_bug = true;
                }

                int o = bound.End - bound.Begin + 1;
                if (o < 23)
                {
                    if (header) o = bound.End + 1;
                    else o = bound.End;
                }

                if (line_bug) o = bound.End + (header ? 4 : 0);

                for (int i = header ? 4 : 0; i < o; i++, line++)
                {
                    RawLines.Add(LiPTT.Copy(screen[i]));
                }

                if (line_bug) Debug.WriteLine(string.Format("PTT BUG? ReadLine {0}", RawLines.Count));

                action = LiPTT.RunInUIThread(() => 
                {
                    Parse();
                    AddNewTextBlock();
                    BeginLoaded?.Invoke(this, new EventArgs());
                });

                InitialLoaded = true;
                if (bound.Percent < 100) more = true;
                else more = false;
            }
        }

        private Binding NormalFontSizeBinding;
        private Binding SmallFontSizeBinding;
        private Binding EchoFontSizeBinding;

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

            //Parse完了以後 等待圖片下載完成後 並加入
            if (DownloadImageTasks.Count > 0)
            {
                while (DownloadImageTasks.Count > 0)
                {
                    var firstFinishedTask = await Task.WhenAny(DownloadImageTasks);

                    var item = firstFinishedTask.Result.Item;

                    if (item != null)
                        this[firstFinishedTask.Result.Index] = item;
                    
                    DownloadImageTasks.Remove(firstFinishedTask);
                }
            }
        }

        public void AddLine(Block[] blocks, string str)
        {
            Match match;
            Regex regex = new Regex(LiPTT.http_regex);
            int index = 0; //for string
            int i = 0; //for block
            PrepareAddText();

            List<Uri> ViewUri = new List<Uri>();

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
                    Uri uri = new Uri(match.ToString());

                    if (IsUriView(uri))
                    {
                        if (IsUriVisible(uri))
                            AddHyperlink(uri);
                        ViewUri.Add(uri);
                    }
                    else
                    {
                        Uri u = GetUri(uri);
                        if (u != null)
                        {
                            if (IsUriVisible(u))
                                AddHyperlink(u);
                            if (IsUriView(u))
                                ViewUri.Add(u);
                        }
                        else
                            AddHyperlink(uri);
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
                AddNewTextBlock();

                foreach (Uri view_uri in ViewUri)
                {
                    AddUriView(view_uri);
                }
            }
        }

        private void PrepareAddText()
        {
            if (RichTextBlock == null)
            {
                RichTextBlock = new RichTextBlock()
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    IsTextSelectionEnabled = true,
                    IsRightTapEnabled = false,
                };
                Paragraph = new Paragraph
                {
                    Margin = new Thickness(0),
                    IsTextScaleFactorEnabled = true,
                    //LineHeight = ArticleFontSize,
                    //LineStackingStrategy = LineStackingStrategy.BlockLineHeight
                };
                RichTextBlock.Blocks.Add(Paragraph);
                //還不要加到Visual Tree
                //Add(RichTextBlock);
            }
        }

        private void AddNewTextBlock()
        {
            if (RichTextBlock != null)
            {
                Border border = new Border
                {
                    //BorderThickness = new Thickness(1),
                    //BorderBrush = new SolidColorBrush(Colors.Red),
                };
                border.Child = RichTextBlock;
                Add(border);
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
                    string text = LiPTT.GetString(blocks, idx, i - idx);
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
                    string text = LiPTT.GetString(blocks, idx, index + count - idx);
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

        private void AddHyperlink(Uri uri)
        {
            //插入超連結
            try
            {
                Hyperlink hyperlink = new Hyperlink() { NavigateUri = uri, UnderlineStyle = UnderlineStyle.Single };
                Run run = new Run()
                {
                    Text = uri.OriginalString,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontFamily = ArticleFontFamily,
                };
                hyperlink.Inlines.Add(run);
                BindingOperations.SetBinding(run, TextElement.FontSizeProperty, NormalFontSizeBinding);
                Paragraph.Inlines.Add(hyperlink);
            }
            catch {
                Run run = new Run()
                {
                    Text = uri.OriginalString,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontFamily = ArticleFontFamily,
                    //FontSize = ArticleFontSize,
                    TextDecorations = Windows.UI.Text.TextDecorations.Underline,
                };
                Paragraph.Inlines.Add(run);
                BindingOperations.SetBinding(run, TextElement.FontSizeProperty, NormalFontSizeBinding);
            }
        }

        private void AddEcho(Block[] block)
        {
            AddNewTextBlock();

            Echo echo = new Echo() { Floor = Floor++ };

            string str = LiPTT.GetString(block, 0, block.Length - 13).Replace('\0', ' ').Trim();

            int index = 2;
            int end = str.IndexOf(':');

            string auth = str.Substring(index, end - index);

            echo.Author = auth.Trim();

            echo.Content = str.Substring(end + 1);

            string time = LiPTT.GetString(block, 67, 11);
            //https://msdn.microsoft.com/zh-tw/library/8kb3ddd4(v=vs.110).aspx
            try
            {
                System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                echo.Date = DateTimeOffset.ParseExact(time, "MM/dd HH:mm", provider);
                echo.DateFormated = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

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

            Grid grid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };

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

            switch (echo.Evaluation)
            {
                case Evaluation.推:
                    grid.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x1A, 0x1A, 0x00));
                    break;
                case Evaluation.噓:
                    grid.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x1A, 0x00, 0x00));
                    break;
                default:
                    break;
            }

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

            var tb1 = new TextBlock()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Text = str[0].ToString(),
                Foreground = EvalColor
            };
            BindingOperations.SetBinding(tb1, TextBlock.FontSizeProperty, EchoFontSizeBinding);
            g0.Children.Add(tb1);

            //推文ID////高亮五樓/////高亮原PO/////////////////////
            SolidColorBrush authorColor = new SolidColorBrush(Colors.LightSalmon);

            if (ArticleTag.Author == echo.Author)
            {
                authorColor = new SolidColorBrush(Colors.LightBlue);
            }

            var tb2 = new TextBlock() {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = echo.Author,
                Foreground = authorColor
            };
            BindingOperations.SetBinding(tb2, TextBlock.FontSizeProperty, EchoFontSizeBinding);
            g1.Children.Add(tb2);

            //推文內容////////////////////////////////////////////
            Match match;
            Regex regex = new Regex(LiPTT.http_regex);
            index = 0; //for string
            StackPanel stackpanel = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Stretch };
            List<Uri> ViewUri = new List<Uri>();

            while ((match = regex.Match(echo.Content, index)).Success)
            {
                if (index < match.Index)
                {
                    string text = echo.Content.Substring(index, match.Index - index);
                    var tb3 = new TextBlock()
                    {
                        IsTextSelectionEnabled = true,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 22,
                        Foreground = new SolidColorBrush(Colors.Gold),
                        Text = text,
                    };
                    BindingOperations.SetBinding(tb1, TextBlock.FontSizeProperty, EchoFontSizeBinding);
                    stackpanel.Children.Add(tb3);
                }

                try
                {
                    string text = match.ToString();
                    Uri uri = new Uri(text);

                    //總是顯示超連結
                    var hb = new HyperlinkButton()
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        NavigateUri = uri,
                        Content = new TextBlock() { Text = text },
                    };
                    BindingOperations.SetBinding(hb, Control.FontSizeProperty, EchoFontSizeBinding);
                    stackpanel.Children.Add(hb);
                    if (IsUriView(uri))
                    {

                        ViewUri.Add(uri);
                    }
                    else
                    {
                        Uri u = GetUri(uri);
                        if (u != null)
                        {
                            if (IsUriView(u))
                                ViewUri.Add(u);
                        }
                    }
                }
                catch (UriFormatException)
                {
                    string text = echo.Content.Substring(index, match.Index - index);
                    var tb = new TextBlock()
                    {
                        IsTextSelectionEnabled = true,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(Colors.Gold),
                        Text = text,
                    };
                    BindingOperations.SetBinding(tb, TextBlock.FontSizeProperty, EchoFontSizeBinding);
                    stackpanel.Children.Add(tb);
                }

                index = match.Index + match.Length;
            }

            if (index < echo.Content.Length)
            {
                string text = echo.Content.Substring(index, echo.Content.Length - index);
                var tb = new TextBlock()
                {
                    IsTextSelectionEnabled = true,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Gold),
                    Text = text,
                };
                BindingOperations.SetBinding(tb, TextBlock.FontSizeProperty, EchoFontSizeBinding);
                stackpanel.Children.Add(tb);
            }

            g2.Children.Add(stackpanel);

            //推文時間////////////////////////////////////////////
            if (echo.DateFormated)
            {
                var tb = new TextBlock()
                {
                    Margin = new Thickness(0, 0, 20, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Wheat),
                    Text = echo.Date.ToString("MM/dd HH:mm")
                };
                BindingOperations.SetBinding(tb, TextBlock.FontSizeProperty, EchoFontSizeBinding);
                g3.Children.Add(tb);
            }

            //////////////////////////////////////////////
            grid.Children.Add(g0);
            grid.Children.Add(g1);
            grid.Children.Add(g2);
            grid.Children.Add(g3);

            ToolTip toolTip = new ToolTip() { Content = string.Format("{0}樓", echo.Floor) };

            Button button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Colors.Black),
            };
            button.Content = grid;
            //ListView list = new ListView() { IsItemClickEnabled = true, HorizontalAlignment = HorizontalAlignment.Stretch, };
            ToolTipService.SetToolTip(button, toolTip);
            /***
            list.Items.Add(new ListViewItem()
            {
                Content = grid,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                AllowFocusOnInteraction = false,
            });
            /***/
            Add(button);

            if (ViewUri.Count > 0)
            {
                foreach (Uri uri in ViewUri)
                {
                    AddUriView(uri);
                }
            }
        }

        private void AddUriView(Uri uri)
        {
            Debug.WriteLine("request: " + uri.OriginalString);

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

            if (IsPictureUri(uri))
            {       
                ProgressRing ring = new ProgressRing() { IsActive = true, Foreground = new SolidColorBrush(Colors.Aquamarine) };
                ring.SetBinding(FrameworkElement.WidthProperty, ringBinding);
                ring.SetBinding(FrameworkElement.HeightProperty, ringBinding);
                Grid grid = new Grid() { Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)) };
                grid.SetBinding(FrameworkElement.WidthProperty, bindingWidth);
                grid.SetBinding(FrameworkElement.HeightProperty, bindingHeight);
                grid.Children.Add(ring);
                Add(grid);

                DownloadImageTasks.Add(DownloadImage(Count - 1, uri));
            }
            else if (uri.Host == "imgur.com")
            {
                if (uri.OriginalString.IndexOf("imgur.com/a/") == -1)
                {
                    Match match = new Regex("imgur.com/").Match(uri.OriginalString);

                    if (match.Success)
                    {
                        string ID = uri.OriginalString.Substring(match.Index + match.Length);

                        if (ID != "")
                        {
                            Uri new_uri = new Uri("http://i.imgur.com/" + ID + ".png");

                            ProgressRing ring = new ProgressRing() { IsActive = true, Foreground = new SolidColorBrush(Colors.Aquamarine) };
                            ring.SetBinding(FrameworkElement.WidthProperty, ringBinding);
                            ring.SetBinding(FrameworkElement.HeightProperty, ringBinding);
                            Grid grid = new Grid() { Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)) };
                            grid.SetBinding(FrameworkElement.WidthProperty, bindingWidth);
                            grid.SetBinding(FrameworkElement.HeightProperty, bindingHeight);
                            grid.Children.Add(ring);
                            Add(grid);
                            DownloadImageTasks.Add(DownloadImage(Count - 1, new_uri));
                        }                     
                    }
                }
            }
            else if (uri.Host == "i.imgur.com")
            {
                string str = uri.OriginalString;

                if (str.IndexOf("i.imgur.com/a/") == -1)
                {
                    Match match = new Regex("i.imgur.com/").Match(str);

                    if (match.Success)
                    {
                        string ID = uri.OriginalString.Substring(match.Index + match.Length);
                        if (ID != "")
                        {
                            Uri new_uri = new Uri("http://i.imgur.com/" + ID + ".png");

                            ProgressRing ring = new ProgressRing() { IsActive = true, Foreground = new SolidColorBrush(Colors.Aquamarine) };
                            ring.SetBinding(FrameworkElement.WidthProperty, ringBinding);
                            ring.SetBinding(FrameworkElement.HeightProperty, ringBinding);
                            Grid grid = new Grid() { Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)) };
                            grid.SetBinding(FrameworkElement.WidthProperty, bindingWidth);
                            grid.SetBinding(FrameworkElement.HeightProperty, bindingHeight);
                            grid.Children.Add(ring);
                            Add(grid);
                            DownloadImageTasks.Add(DownloadImage(Count - 1, new_uri));
                        } 
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

                AddVideoView("youtube", youtubeID);
            }
            else if (IsTwitchUri(uri))
            {
                string twitchID = uri.LocalPath.Substring(1);
                AddVideoView("twitch", twitchID);
            }
        }

        private Uri GetUri(Uri uri)
        {
            if (!IsShortUri(uri)) return null;

            Debug.WriteLine(string.Format("Try Get: {0}", uri.OriginalString));
            try
            {
                WebRequest webRequest = WebRequest.Create(uri);
                Task<WebResponse> t = webRequest.GetResponseAsync();
                if (t.Wait(30000))
                    return t.Result.ResponseUri;
                else
                {
                    Debug.WriteLine(string.Format("{0} - {1}", uri.OriginalString, "等太久了"));
                    return null;
                }
            }
            catch (UriFormatException e)
            {
                Debug.WriteLine(string.Format("{0} - {1}", uri.OriginalString, e.Message));
                return null;
            }
            catch (WebException e)
            {
                Debug.WriteLine(string.Format("{0} - {1}", uri.OriginalString, e.Message));
                return null;
            }
            catch (AggregateException e)
            {
                Debug.WriteLine(string.Format("{0} - {1}", uri.OriginalString, e.Message));
                return null;
            }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("{0} - {1}", uri.OriginalString, e.Message));
                return null;
            }
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

        private void AddVideoView(string tag, string ID)
        {
            Grid MainGrid = new Grid() { Tag = tag, HorizontalAlignment = HorizontalAlignment.Stretch };

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

            Grid youtuGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };
            Binding binding = new Binding { ElementName = "proxy", Path = new PropertyPath("ActualWidthValue"), Converter = Application.Current.Resources["YoutubeGridHeightConverter"] as YoutubeGridHeightConverter };
            youtuGrid.SetBinding(FrameworkElement.HeightProperty, binding);
            InnerGrid.Children.Add(youtuGrid);

            ProgressRing ring = new ProgressRing() { IsActive = true, Foreground = new SolidColorBrush(Colors.Orange) };
            Binding ringBinding = new Binding { ElementName = "proxy", Path = new PropertyPath("ActualWidthValue"), Converter = Application.Current.Resources["RingRatioConverter"] as RingRatioConverter };
            ring.SetBinding(FrameworkElement.WidthProperty, ringBinding);
            ring.SetBinding(FrameworkElement.HeightProperty, ringBinding);

            WebView webview = new WebView { DefaultBackgroundColor = Colors.Black };

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
                webview.Visibility = Visibility.Collapsed;
            };

            webview.FrameDOMContentLoaded += (a, b) =>
            {
                ring.IsActive = false;
                webview.Visibility = Visibility.Visible;
            };

            webview.DOMContentLoaded += async (a, b) =>
            {
                try
                {
                    //在WebView裡面執行Javascript, 撒尿牛丸膩?
                    string returnStr = await webview.InvokeScriptAsync("LoadVideoByID", new string[] { ID });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Script Error" + ex.ToString());
                }
            };

            youtuGrid.Children.Add(webview);
            youtuGrid.Children.Add(ring);

            Add(MainGrid);

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

        private bool IsUriView(Uri uri)
        {
            bool view = false;

            if (IsPictureUri(uri)) //.jpg,.png結尾 等等
                view = true;
            else if (uri.Host == "imgur.com" && uri.OriginalString.IndexOf("imgur.com/a/") == -1)
            {
                Match match = new Regex("imgur.com/").Match(uri.OriginalString);
                if (match.Success)
                {
                    string id = uri.OriginalString.Substring(match.Index + match.Length);
                    if (id.Length > 0)
                        view = true;
                }
            }  
            else if (uri.Host == "i.imgur.com" && uri.OriginalString.IndexOf("i.imgur.com/a/") == -1)
            {
                Match match = new Regex("i.imgur.com/").Match(uri.OriginalString);
                if (match.Success)
                {
                    string id = uri.OriginalString.Substring(match.Index + match.Length);
                    if (id.Length > 0)
                        view = true;
                }
            }
            else if (IsYoutubeUri(uri))
                view = true;
            else if (IsTwitchUri(uri))
                view = true;

            return view;
        }

        private bool IsUriVisible(Uri uri)
        {
            if (IsUriView(uri))
            {
                bool visible = true;

                if (IsPictureUri(uri)) //.jpg,.png結尾 等等
                    visible = false;
                else if (uri.Host == "imgur.com" && uri.OriginalString.IndexOf("imgur.com/a/") == -1)
                    visible = false;
                else if (uri.Host == "i.imgur.com" && uri.OriginalString.IndexOf("i.imgur.com/a/") == -1)
                    visible = false;
                else if (IsYoutubeUri(uri))
                    visible = false;
                else if (uri.Host == "www.twitch.tv")
                    visible = true;

                return visible;
            }
            else
                return true;
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
            if (uri.Host == "www.youtube.com")
                return true;
            else
                return false;
        }

        private bool IsTwitchUri(Uri uri)
        {
            if (uri.Host == "www.twitch.tv" || uri.Host == "go.twitch.tv")
                return true;
            else
                return false;
        }

        private bool IsShortUri(Uri uri)
        {
            switch (uri.Host)
            {
                case "youtu.be":
                    return true;
                case "goo.gl":
                    return true;
                case "bit.ly":
                    return true;
                default:
                    return false;
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

        private Bound ReadLineBound(string msg)
        {
            Bound bound = new Bound();
            Regex regex = new Regex(LiPTT.bound_regex);
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
