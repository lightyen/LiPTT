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

namespace LiPTT
{
    public class ArticleContentCollection : ObservableCollection<object>, ISupportIncrementalLoading
    {
        private bool more;

        public bool InitialLoaded { get; private set; }

        /// <summary>
        /// 圖片、影片左右留空白，左右各留10%，即0.2
        /// </summary>
        public double Space { get; set; }

        //public double ViewWidth { get; set; }

        public ActualSizePropertyProxy ListViewProxy { get; set; }

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

        private SemaphoreSlim sem = new SemaphoreSlim(0, 1);

        private bool DividerLine = false;
        private uint Floor = 0;
        private string FiveFloor = "";

        public ArticleContentCollection()
        {
            Space = 0.2;
            header = false;
            line = 0;
            RawLines = new List<Block[]>();
            DownloadPictureTasks = new List<Task<DownloadResult>>();

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

        private void PttUpdated(PTTProvider sender, LiPttEventArgs e)
        {
            LiPTT.PttEventEchoed -= PttUpdated;

            IAsyncAction action;

            if (e.State == PttState.Article)
            {
                bound = ReadLineBound(e.Screen.ToString(23));

                int o = header ? 1 : 0;

                //有些文章bound.End - bound.Begin不等於23，而且也沒到100%，PTT的Bug嗎?
                for (int i = line - bound.Begin + 1 + (bound.Begin < 5 ? o : 0); i < 23; i++, line++)
                {
                    RawLines.Add(LiPTT.Copy(e.Screen[i]));
                }

                action = LiPTT.RunInUIThread(() =>
                {
                    Parse();
                    if (RichTextBlock != null)
                        Add(RichTextBlock);
                    RichTextBlock = null;
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
                    Parse();
                    if (RichTextBlock != null)
                        Add(RichTextBlock);
                    RichTextBlock = null;
                    BeginLoaded?.Invoke(this, new EventArgs());
                });

                InitialLoaded = true;
                if (bound.Percent < 100) more = true;
                else more = false;
            }
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
                }
                else if (IsEchoes(str))
                {
                    AddEcho(RawLines[row]);
                }
                else
                {
                    string eee = str.Trim(new char[] { '\0', ' ' });
                    if (eee == "--")
                    {
                        DividerLine = true;
                    }

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
                            int _count = LiPTT_Encoding.GetEncoding().GetByteCount(text);
                            AddText(RawLines[row], i, _count);
                            i += _count;
                        }

                        int count = LiPTT_Encoding.GetEncoding().GetByteCount(match.ToString());

                        try
                        {
                            Uri uri = new Uri(match.ToString());
                            if (!AddHyperlink(uri))
                                ViewUri.Add(uri);
                        }
                        catch (UriFormatException)
                        {
                            AddText(RawLines[row], i, count);
                        }

                        i += count;

                        index = match.Index + match.Length;
                    }

                    if (index < str.Length)
                    {
                        string text = str.Substring(index, str.Length - index);
                        int _count = LiPTT_Encoding.GetEncoding().GetByteCount(text);
                        AddText(RawLines[row], i, _count);
                        i += _count;
                    }

                    Paragraph.Inlines.Add(new LineBreak());

                    if (ViewUri.Count > 0)
                    {
                        if (RichTextBlock != null)
                            Add(RichTextBlock);
                        RichTextBlock = null;

                        foreach (Uri view_uri in ViewUri)
                        {
                            CreateUriView(view_uri);
                        }
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
            if (RichTextBlock == null)
            {
                RichTextBlock = new RichTextBlock() { Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Left };
                Paragraph = new Paragraph();
                RichTextBlock.Blocks.Add(Paragraph);
                //還不要加到Visual Tree
                //Add(RichTextBlock);
            }
        }

        private void AddTextLine(Block[] blocks)
        {
            int color = blocks[0].ForegroundColor;
            int index = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                Block b = blocks[i];
                if (color != b.ForegroundColor)
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
                    Run run = new Run()
                    {
                        Text = text.Replace('\0', ' '),
                        FontSize = ArticleFontSize,
                        FontFamily = ArticleFontFamily,
                        Foreground = GetForegroundBrush(blocks[index]),
                    };
                    /***/
                    Paragraph.Inlines.Add(run);
                    index = i;
                    color = b.ForegroundColor;
                }

                if (i == blocks.Length - 1)
                {
                    string text = LiPTT.GetString(blocks, index, blocks.Length - index);
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
                        FontSize = ArticleFontSize,
                        FontFamily = ArticleFontFamily,
                        Foreground = GetForegroundBrush(blocks[index]),
                    };
                    /***/
                    Paragraph.Inlines.Add(run);
                    color = b.ForegroundColor;
                    break;
                }
            }

            Paragraph.Inlines.Add(new LineBreak());
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

                    if (color != b.ForegroundColor)
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

                    if (i == count - 1)
                    {
                        string text = LiPTT.GetString(blocks, index, count - index).Replace('\0', ' ');
                        Run container = new Run()
                        {
                            Text = text,
                            FontSize = ArticleFontSize,
                            FontFamily = ArticleFontFamily,
                            Foreground = GetForegroundBrush(blocks[index]),
                        };

                        Paragraph.Inlines.Add(container);
                        color = b.ForegroundColor;
                        break;
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
            else if (uri.Host == "imgur.com" && uri.OriginalString.IndexOf("imgur.com/a/") == -1)
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

                    if (color != b.ForegroundColor)
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

                    if (i == blocks.Length - 1)
                    {
                        string text = LiPTT.GetString(blocks, index, blocks.Length - index).Replace('\0', ' ');
                        Run container = new Run()
                        {
                            Text = text,
                            FontSize = ArticleFontSize,
                            FontFamily = ArticleFontFamily,
                            Foreground = GetForegroundBrush(blocks[index]),
                        };

                        Paragraph.Inlines.Add(container);
                        color = b.ForegroundColor;
                        break;
                    }
                }
            }
            Paragraph.Inlines.Add(new LineBreak());

            if (!hyperlinkVisible)
            {
                if (RichTextBlock != null)
                    Add(RichTextBlock);
                RichTextBlock = null;
                CreateUriView(uri);
            }             
        }

        private void AddText(Block[] blocks, int index, string text)
        {
            int count = LiPTT_Encoding.GetEncoding().GetByteCount(text);
            if (index + count <= blocks.Length)
            {
                AddText(blocks, index, count);
            }
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
                        FontSize = ArticleFontSize,
                        FontFamily = ArticleFontFamily,
                        Foreground = GetForegroundBrush(blocks[idx]),
                    };
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
                        FontSize = ArticleFontSize,
                        FontFamily = ArticleFontFamily,
                        Foreground = GetForegroundBrush(blocks[idx]),
                    };
                    /***/
                    Paragraph.Inlines.Add(run);
                    color = b.ForegroundColor;
                    break;
                }
            }
        }

        private bool AddHyperlink(Uri uri)
        {
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
            else if (uri.Host == "imgur.com" && uri.OriginalString.IndexOf("imgur.com/a/") == -1)
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

            return hyperlinkVisible;
        }

        /// <summary>
        /// 找五樓用的
        /// </summary>
        private Dictionary<Echo, TextBlock> tempEchoes = new Dictionary<Echo, TextBlock>();

        private void AddEcho(Block[] block)
        {
            if (DividerLine)
                Floor++;

            if (RichTextBlock != null)
                Add(RichTextBlock);
            RichTextBlock = null;

            Uri uri = null;
            Echo echo = new Echo() { Floor = Floor };

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
            Grid grid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };
            ColumnDefinition c0 = new ColumnDefinition() { Width = new GridLength(30, GridUnitType.Pixel) };
            ColumnDefinition c1 = new ColumnDefinition() { Width = new GridLength(200, GridUnitType.Pixel) };
            ColumnDefinition c2 = new ColumnDefinition() { Width = new GridLength(8.0, GridUnitType.Star) };
            ColumnDefinition c3 = new ColumnDefinition() { Width = new GridLength(1.5, GridUnitType.Star) };

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

            g0.Children.Add(new TextBlock() {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Text = str[0].ToString(),
                FontSize = 22,
                Foreground = EvalColor });
            
            //推文ID////高亮五樓/////高亮原PO/////////////////////
            SolidColorBrush authorColor = new SolidColorBrush(Colors.LightSalmon);
            if (Floor == 5)
            {
                FiveFloor = echo.Author;
                authorColor = new SolidColorBrush(Colors.LightPink);
            }
            else if (FiveFloor == echo.Author)
            {
                authorColor = new SolidColorBrush(Colors.LightPink);
            }
            else if (ArticleTag.Author == echo.Author)
            {
                authorColor = new SolidColorBrush(Colors.LightBlue);
            }

            TextBlock tb = new TextBlock() {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = echo.Author, FontSize = 22,
                Foreground = authorColor
            };
            g1.Children.Add(tb);

            //把五樓以前的五樓也高亮起來
            if (Floor <= 5)
                tempEchoes.Add(echo, tb);

            if (tempEchoes.Count >= 5)
            {
                foreach (var kv in tempEchoes)
                {
                    if (FiveFloor == kv.Key.Author)
                    {
                        if (FiveFloor != ArticleTag.Author)
                            kv.Value.Foreground = new SolidColorBrush(Colors.LightPink);
                    }
                        
                }
                tempEchoes.Clear();
            }

            //推文內容////////////////////////////////////////////
            Match match;

            if ((match = new Regex(LiPTT.http_regex).Match(echo.Content)).Success)
            {
                string url = echo.Content.Substring(match.Index, match.Length);

                StackPanel sp = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Stretch };

                if (match.Index > 0)
                {
                    sp.Children.Add(new TextBlock()
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 22,
                        Foreground = new SolidColorBrush(Colors.Gold),
                        Text = echo.Content.Substring(0, match.Index),
                    });
                }

                try
                {
                    uri = new Uri(url);
                    sp.Children.Add(new HyperlinkButton()
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        NavigateUri = uri,
                        FontSize = 22,
                        Content = new TextBlock() { Text = url },
                    });
                }
                catch (UriFormatException)
                {
                    sp.Children.Add(new TextBlock()
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 22, 
                        Foreground = new SolidColorBrush(Colors.Gray),
                        Text = url,
                    });
                }

                if (match.Index + match.Length < echo.Content.Length)
                {
                    sp.Children.Add(new TextBlock()
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 22,
                        Foreground = new SolidColorBrush(Colors.Gold),
                        Text = echo.Content.Substring(match.Index + match.Length, echo.Content.Length - (match.Index + match.Length)),
                    });
                }

                g2.Children.Add(sp);
            }
            else
            {
                g2.Children.Add(new TextBlock()
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 22,
                    Foreground = new SolidColorBrush(Colors.Gold),
                    Text = echo.Content,
                });
            }

            //推文時間////////////////////////////////////////////
            if (echo.DateFormated)
            {
                g3.Children.Add(new TextBlock()
                {
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Wheat),
                    Text = echo.Date.ToString("MM/dd HH:mm")
                });
            }

            //////////////////////////////////////////////
            grid.Children.Add(g0);
            grid.Children.Add(g1);
            grid.Children.Add(g2);
            grid.Children.Add(g3);

            ToolTip toolTip = new ToolTip() { Content = string.Format("{0}樓", echo.Floor) };
            ListView list = new ListView() { IsItemClickEnabled = true, HorizontalAlignment = HorizontalAlignment.Stretch };
            ToolTipService.SetToolTip(list, toolTip);
            list.Items.Add(new ListViewItem()
            {
                Content = grid,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                AllowFocusOnInteraction = false,
                
            });
            Add(list);

            if (uri != null)
            {
                bool CreateView = false;

                if (IsPictureUri(uri))
                {
                    CreateView = true;
                }
                else if (IsYoutubeUri(uri))
                {
                    CreateView = true;
                }
                else if (uri.Host == "imgur.com" && uri.OriginalString.IndexOf("imgur.com/a/") == -1)
                {
                    CreateView = true;
                }

                if (CreateView) CreateUriView(uri);
            }
        }

        private void CreateUriView(Uri uri)
        {
            Debug.WriteLine("request: " + uri.OriginalString);
            //http://www.cnblogs.com/jesse2013/p/async-and-await.html
            //***
            if (IsShortCut(uri.Host))
            {
                WebRequest webRequest = WebRequest.Create(uri);
                WebResponse webResponse = webRequest.GetResponseAsync().Result;
                uri = webResponse.ResponseUri;
            }
            /***/
            double ViewWidth = ListViewProxy == null ? 0 : ListViewProxy.ActualWidthValue;

            if (IsPictureUri(uri))
            {
                ProgressRing ring = new ProgressRing() { IsActive = true, Width = 55, Height = 55 };

                Grid grid = new Grid() { Width = ViewWidth * (1 - Space), Height = 0.5625 * ViewWidth * (1 - Space), Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)) };
                grid.Children.Add(ring);
                Add(grid);

                DownloadPictureTasks.Add(CreateImageView(Count - 1, uri));
            }
            else if (uri.Host == "imgur.com")
            {
                string str = uri.OriginalString;

                if (str.IndexOf("imgur.com/a/") == -1)
                {
                    Match match = new Regex("imgur.com").Match(str);

                    if (match.Success)
                    {
                        str = str.Insert(match.Index, "i.");
                        str += ".png";
                        Uri new_uri = new Uri(str);

                        ProgressRing ring = new ProgressRing() { IsActive = true, Width = 55, Height = 55 };
                        Grid grid = new Grid() { Width = ViewWidth * (1 - Space), Height = 0.5625 * ViewWidth * (1 - Space), Background = new SolidColorBrush(Color.FromArgb(0x20, 0x80, 0x80, 0x80)) };
                        grid.Children.Add(ring);
                        Add(grid);

                        DownloadPictureTasks.Add(CreateImageView(Count - 1, new_uri));
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

        ///#1PoaSW6h

        /// <summary>
        /// 下載圖片，並加入到ListView
        /// </summary>
        private async Task<DownloadResult> CreateImageView(int index, Uri uri)
        {
            Task<BitmapImage> task = LiPTT.ImageCache.GetFromCacheAsync(uri);

            BitmapImage bmp = await task;

            Image img = new Image() { Source = bmp, HorizontalAlignment = HorizontalAlignment.Stretch };

            double ratio = (double)bmp.PixelWidth / bmp.PixelHeight;

            double ViewWidth = ListViewProxy == null ? 0 : ListViewProxy.ActualWidthValue;

            ColumnDefinition c1, c2, c3;

            if (bmp.PixelWidth < ViewWidth * (1 - Space))
            {
                c1 = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
                c2 = new ColumnDefinition { Width = new GridLength(bmp.PixelWidth, GridUnitType.Pixel) };
                c3 = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
            }
            else if (ratio >= 1.0)
            {
                c1 = new ColumnDefinition { Width = new GridLength(Space / 2.0, GridUnitType.Star) };
                c2 = new ColumnDefinition { Width = new GridLength((1 - Space), GridUnitType.Star) };
                c3 = new ColumnDefinition { Width = new GridLength(Space / 2.0, GridUnitType.Star) };
            }
            else
            {
                double x = ratio * (1 - Space) / 2.0;
                c1 = new ColumnDefinition { Width = new GridLength(Space / 2.0 + x, GridUnitType.Star) };
                c2 = new ColumnDefinition { Width = new GridLength((1 - Space) * ratio, GridUnitType.Star) };
                c3 = new ColumnDefinition { Width = new GridLength(Space / 2.0 + x, GridUnitType.Star) };
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

        private void AddYoutubeView(string youtubeID)
        {
            Grid MainGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };

            ColumnDefinition c1, c2, c3;

            c1 = new ColumnDefinition { Width = new GridLength(Space / 2.0, GridUnitType.Star) };
            c2 = new ColumnDefinition { Width = new GridLength((1 - Space), GridUnitType.Star) };
            c3 = new ColumnDefinition { Width = new GridLength(Space / 2.0, GridUnitType.Star) };

            MainGrid.ColumnDefinitions.Add(c1);
            MainGrid.ColumnDefinitions.Add(c2);
            MainGrid.ColumnDefinitions.Add(c3);

            Grid InnerGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };
            InnerGrid.SetValue(Grid.ColumnProperty, 1);
            MainGrid.Children.Add(InnerGrid);

            Grid youtuGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };
            Binding binding = new Binding { ElementName = "proxy", Path = new PropertyPath("ActualWidthValue"), Converter = Application.Current.Resources["MyRatioConverter"] as RatioConverter };
            youtuGrid.SetBinding(FrameworkElement.HeightProperty, binding);
            InnerGrid.Children.Add(youtuGrid);

            ProgressRing ring = new ProgressRing() { IsActive = true, Foreground = new SolidColorBrush(Colors.Orange) };
            Binding ringBinding = new Binding { ElementName = "proxy", Path = new PropertyPath("ActualWidthValue"), Converter = Application.Current.Resources["MyRingRatioConverter"] as RingRatioConverter };
            ring.SetBinding(FrameworkElement.WidthProperty, ringBinding);
            ring.SetBinding(FrameworkElement.HeightProperty, ringBinding);

            WebView webview = new WebView { DefaultBackgroundColor = Colors.Black };

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
                    string returnStr = await webview.InvokeScriptAsync("LoadYoutubeByID", new string[] { youtubeID });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Script Error" + ex.ToString());
                }
            };

            youtuGrid.Children.Add(webview);
            youtuGrid.Children.Add(ring);

            Add(MainGrid);
            webview.Navigate(new Uri("ms-appx-web:///Templates/youtube/youtube.html"));
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
    }
}
