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

//https://stackoverflow.com/questions/5473001/itemscontrol-with-multiple-datatemplates-for-a-viewmodel

namespace LiPTT
{
    //https://www.ptt.cc/bbs/PttNewhand/M.1265292872.A.991.html
    //未讀 + M S
    //已讀   m s
    //被標記 m
    //待處理 s
    //被鎖文 !
    //新推文 ~
    //被標記且有新推文 =

    [Flags]
    public enum ReadType
    {
        None     = 0b0000_0000, //'+'
        已讀     = 0b0000_0001,
        被標記   = 0b0000_0010,
        有推文   = 0b0000_0100,
        被鎖定   = 0b0000_1000,
        待處理   = 0b0001_0000,
        Undefined = 0b1000_0000,
    }

    public enum Evaluation
    {
        箭頭 = 0,
        推 = 1,
        噓 = 2,
    }

    public class Bound
    {
        public int Begin { get; set; }
        public int End { get; set; }
        public int Percent { get; set; }
    }

    public class Echo
    {
        public Evaluation Evaluation { get; set; }
        public string Author { get; set; }
        public string Content { get; set; }
        public DateTimeOffset Date { get; set; }
    }

    public class DownloadResult
    {
        public int Index { get; set; }
        public object Item { get; set; }
    }

    public class BoardInfo : INotifyPropertyChanged
    {
        private string name;
        private int popularity;
        private string nick;
        private string description;
        private string[] leaders;
        private string category;
        private int limit_login;
        private int limit_reject;

        public string Name
        {
            get { return name; }
            set { name = value; NotifyPropertyChanged("Name"); }
        }

        public string Category
        {
            get { return category; }
            set { category = value; NotifyPropertyChanged("Category"); }
        }

        public string NickName
        {
            get { return nick; }
            set { nick = value; NotifyPropertyChanged("NickName"); }
        }

        public string Description
        {
            get { return description; }
            set { description = value; NotifyPropertyChanged("Description"); }
        }

        public int Popularity
        {
            get { return popularity; }
            set { popularity = value; NotifyPropertyChanged("Popularity"); }
        }

        public string[] Leaders
        {
            get { return leaders; }
            set { leaders = value; NotifyPropertyChanged("Leaders"); }
        }

        public int LimitLogin
        {
            get { return limit_login; }
            set { limit_login = value; NotifyPropertyChanged("LimitLogin"); }
        }
        public int LimitReject
        {
            get { return limit_reject; }
            set { limit_reject = value; NotifyPropertyChanged("LimitReject"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                var a = LiPTT.RunInUIThread(() => {
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
                
            }
        }
    }

    public class Article : IComparable<Article>, INotifyPropertyChanged
    {
        public uint ID { get; set; } //文章流水號
        public string AID { get; set; } //文章代碼
        public int Star { get; set; }
        public bool Deleted { get; set; }
        public int Like { get; set; } //推/噓
        public DateTimeOffset Date { get; set; }
        public string Author { get; set; }
        public string AuthorNickname { get; set; }
        public string Subtitle { get; set; }
        public string Title { get; set; }
        public bool Reply { get; set; } //是否為回覆文
        public int PttCoin { get; set; } //值多少P幣
        public Uri Url { get; set; } //網頁版連結
        public bool LoadCompleted { get; set; }
        public List<Block[]> RawLines { get; set; } //文章生肉串
        public List<object> Content { get; set; } //本文內容
        public EchoCollection Echoes { get; set; } //推文集
        public List<Task<Tuple<int, object>>>  SomeTasks { get; set; }
        public List<Task<DownloadResult>> DownloadTasks { get; set; }

        public double ViewWidth { get; set; }

        public bool ParsedContent { get; private set; }
        public int PageDownPercent { get; set; }
        private int ParsedLine { get; set; }
        private Paragraph paragraph;
        private static double ArticleFontSize = 24.0;
        private FontFamily ArticleFontFamily;

        
        private ReadType readtype;
        public ReadType ReadType
        {
            get
            {
                return readtype;
            }
            set
            {
                readtype = value;
                NotifyPropertyChanged("ReadType");
            }
        }

        private static HashSet<string> ShortCutSet = new HashSet<string>()
        { 
            "youtu.be",
            //"goo.gl",
            //"bit.ly",
            //"ppt.cc",
        };

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void DefaultState()
        {
            LoadCompleted = false;
            
            ParsedLine = 0;
            PageDownPercent = 0;
            ParsedContent = false;
            RawLines.Clear();
            SomeTasks.Clear(); ;
            DownloadTasks.Clear();
            Echoes.Clear();

            //508171 #1PkEzljp #1PXU-fck
            Content.Clear();
        }

        public Article()
        {
            RawLines = new List<Block[]>();
            SomeTasks = new List<Task<Tuple<int, object>>>();
            DownloadTasks = new List<Task<DownloadResult>>();
            Echoes = new EchoCollection() { };
            Content = new List<object>();

            var action = LiPTT.RunInUIThread(() =>
            {
                ArticleFontFamily = new FontFamily("Noto Sans Mono CJK TC");
                paragraph = new Paragraph();
            });
        }

        public void AppendLine(Block[] blocks)
        {
            RawLines.Add(LiPTT.Copy(blocks));
        }

        public void ParseBeta()
        {
            for (int row = ParsedLine; row < RawLines.Count; row++, ParsedLine++)
            {
                string str = LiPTT.GetString(RawLines[row]);
                Match match;

                if (!ParsedContent)
                {
                    if (str.StartsWith("※"))
                    {
                        if (paragraph.Inlines.Count > 0)
                        {
                            Content.Add(CreateTextBlock(paragraph));
                            paragraph = new Paragraph();
                        }

                        if ((match = new Regex("(文章網址:)").Match(str)).Success)
                        {
                            //當作過濾完畢
                            ParsedContent = true;
                        }
                        else if ((match = new Regex("(發信站:)").Match(str)).Success)
                        {
                            //當作過濾完畢
                            ParsedContent = true;
                        }
                        else if ((match = new Regex("(編輯:)").Match(str)).Success)
                        {
                            //當作過濾完畢
                            ParsedContent = true;
                        }
                        
                        TextBlock tb = new TextBlock()
                        {
                            Text = str,
                            IsTextSelectionEnabled = true,
                            Foreground = new SolidColorBrush(Colors.Green),
                            FontSize = ArticleFontSize - 8,
                            FontFamily = ArticleFontFamily,
                            VerticalAlignment = VerticalAlignment.Center,
                        };
                        Content.Add(tb);
                    }
                    else if ((match = new Regex(LiPTT.http_regex).Match(str)).Success)
                    {
                        if (paragraph.Inlines.Count > 0)
                        {
                            Content.Add(CreateTextBlock(paragraph));
                            paragraph = new Paragraph();
                        }

                        if (match.Index > 0)
                        {
                            TextBlock tb = new TextBlock()
                            {
                                Text = str.Substring(0, match.Index),
                                IsTextSelectionEnabled = true,
                                Foreground = new SolidColorBrush(Colors.White),
                                FontSize = ArticleFontSize,
                                FontFamily = ArticleFontFamily,
                                VerticalAlignment = VerticalAlignment.Center,
                            };
                            Content.Add(tb);
                        }

                        if (match.Index + match.Length < str.Length)
                        {
                            TextBlock tb = new TextBlock()
                            {
                                Text = str.Substring(match.Index + match.Length, str.Length - (match.Index + match.Length)),
                                IsTextSelectionEnabled = true,
                                Foreground = new SolidColorBrush(Colors.White),
                                FontSize = ArticleFontSize,
                                FontFamily = ArticleFontFamily,
                                VerticalAlignment = VerticalAlignment.Center,
                            };
                            Content.Add(tb);

                        }


                        string url = str.Substring(match.Index, match.Length);

                        CreateUriView(url);
                    }
                    else //內文
                    {
                        int color = RawLines[row][0].ForegroundColor;
                        int index = 0;
                        for (int i = 0; i < RawLines[row].Length; i++)
                        {
                            Block b = RawLines[row][i];
                            if (color != b.ForegroundColor)
                            {
                                string text = LiPTT.GetString(RawLines[row], index, i - index);

                                /****
                                InlineUIContainer container = new InlineUIContainer
                                {
                                    Child = new Border()
                                    {
                                        Background = GetBackgroundBrush(RawLines[row][index]),
                                        Child = new TextBlock()
                                        {
                                            IsTextSelectionEnabled = true,
                                            Text = text.Replace('\0', ' '),
                                            FontSize = ArticleFontSize,
                                            FontFamily = ArticleFontFamily,
                                            Foreground = GetForegroundBrush(RawLines[row][index]),
                                        }
                                    }
                                };
                                /***/
                                Run container = new Run()
                                {
                                    Text = text.Replace('\0', ' '),
                                    FontSize = ArticleFontSize,
                                    FontFamily = ArticleFontFamily,
                                    Foreground = GetForegroundBrush(RawLines[row][index]),
                                };

                                paragraph.Inlines.Add(container);



                                index = i;
                                color = b.ForegroundColor;
                            }
                        }

                        if (index + 1 < RawLines[row].Length)
                        {
                            string text = LiPTT.GetString(RawLines[row], index, RawLines[row].Length - index);

                            /***
                            InlineUIContainer container = new InlineUIContainer
                            {
                                Child = new Border()
                                {
                                    Background = GetBackgroundBrush(RawLines[row][index]),
                                    Child = new TextBlock()
                                    {
                                        IsTextSelectionEnabled = true,
                                        Text = text.Replace('\0', ' '),
                                        FontSize = ArticleFontSize,
                                        FontFamily = ArticleFontFamily,
                                        Foreground = GetForegroundBrush(RawLines[row][index]),
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
                                Foreground = GetForegroundBrush(RawLines[row][index]),
                            };
                            /***/
                            paragraph.Inlines.Add(container);
                        }

                        paragraph.Inlines.Add(new LineBreak());
                    }

                    if (PageDownPercent == 100 && row == RawLines.Count)
                    {
                        if (paragraph.Inlines.Count > 0)
                        {
                            Content.Add(CreateTextBlock(paragraph));
                            paragraph = new Paragraph();
                        }
                        ParsedContent = true;
                    }
                }
                else
                {
                    if (str.StartsWith("※"))
                    {
                        if ((match = new Regex("(文章網址:)").Match(str)).Success)
                        {

                            match = new Regex(LiPTT.http_regex).Match(str);

                            /***
                            if (match.Success)
                            {
                                StackPanel sp = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Stretch };

                                TextBlock tb = new TextBlock()
                                {
                                    Text = str.Substring(0, match.Index),
                                    IsTextSelectionEnabled = true,
                                    Padding = new Thickness(0, 0, 8, 0),
                                    Foreground = new SolidColorBrush(Colors.LightGreen),
                                    FontSize = ArticleFontSize - 8,
                                    FontFamily = ArticleFontFamily,
                                    VerticalAlignment = VerticalAlignment.Center,
                                };
                                sp.Children.Add(tb);

                                HyperlinkButton button = new HyperlinkButton()
                                {
                                    NavigateUri = new Uri(str.Substring(match.Index, match.Length)),
                                    Content = new TextBlock()
                                    { 
                                        Text = str.Substring(match.Index, match.Length),
                                        IsTextSelectionEnabled = true,
                                        Foreground = new SolidColorBrush(Colors.LightGreen),
                                        FontSize = ArticleFontSize - 8,
                                        FontFamily = ArticleFontFamily,
                                        VerticalAlignment = VerticalAlignment.Stretch,
                                    }
                                };
                                sp.Children.Add(button);
                                Content.Add(sp);
                            }
                            /***/

                            TextBlock tb = new TextBlock()
                            {
                                Text = str,
                                IsTextSelectionEnabled = true,
                                Foreground = new SolidColorBrush(Colors.Green),
                                FontSize = ArticleFontSize - 8,
                                FontFamily = ArticleFontFamily
                            };
                            Content.Add(tb);
                            Debug.WriteLine(str);
                        }
                        else
                        {
                            TextBlock tb = new TextBlock()
                            {
                                Text = str,
                                IsTextSelectionEnabled = true,
                                Foreground = new SolidColorBrush(Colors.Green),
                                FontSize = ArticleFontSize - 8,
                                FontFamily = ArticleFontFamily,
                                VerticalAlignment = VerticalAlignment.Center,
                            };
                            Content.Add(tb);
                        }
                    }
                    else if (str.StartsWith("推") || str.StartsWith("噓") || str.StartsWith("→"))
                    {
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

                        Echoes.Add(echo);
                    }
                    else //其他類型推文
                    {
                        Debug.WriteLine(str);
                    }
                }
            }
        }

        public void Parse()
        {
            string http_exp = @"http(s)?://([\w]+\.)+[\w]+(/[\w-./?%&=]*)?";
            string str;
            int row = ParsedLine;

            //過濾文章內容(不包含推文)
            if (!ParsedContent)
            {
                for (; row < RawLines.Count; row++, ParsedLine++)
                {
                    str = LiPTT.GetString(RawLines[row]);

                    Match match;

                    if (str.StartsWith("※"))
                    {
                        if (paragraph.Inlines.Count > 0)
                        {
                            Content.Add(CreateTextBlock(paragraph));
                            paragraph = new Paragraph();
                        }

                        if ((match = new Regex("(發信站:)").Match(str)).Success)
                        {

                            TextBlock tb = new TextBlock()
                            {
                                Text = str,
                                IsTextSelectionEnabled = true,
                                Foreground = new SolidColorBrush(Colors.Green),
                                FontSize = ArticleFontSize - 8,
                                FontFamily = ArticleFontFamily
                            };
                            Content.Add(tb);
                            Debug.WriteLine(str);
                        }
                        else if ((match = new Regex("(文章網址:)").Match(str)).Success)
                        {

                            match = new Regex(http_exp).Match(str);

                            if (match.Success)
                            {
                                StackPanel sp = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Stretch };

                                TextBlock tb = new TextBlock()
                                {
                                    Text = str.Substring(0, match.Index),
                                    IsTextSelectionEnabled = true,
                                    Padding = new Thickness(0, 0, 8, 0),
                                    Foreground = new SolidColorBrush(Colors.Green),
                                    FontSize = ArticleFontSize - 8,
                                    FontFamily = ArticleFontFamily,
                                    VerticalAlignment = VerticalAlignment.Center,
                                };
                                sp.Children.Add(tb);

                                HyperlinkButton button = new HyperlinkButton()
                                {
                                    NavigateUri = new Uri(str.Substring(match.Index, match.Length)),
                                    Content = new TextBlock()
                                    {
                                        Text = str.Substring(match.Index, match.Length),
                                        IsTextSelectionEnabled = true,
                                        Foreground = new SolidColorBrush(Colors.Green),
                                        FontSize = ArticleFontSize - 8,
                                        FontFamily = ArticleFontFamily,
                                        VerticalAlignment = VerticalAlignment.Center,
                                    }
                                };
                                sp.Children.Add(button);
                                Content.Add(sp);
                            }
                            else
                            {
                                TextBlock tb = new TextBlock()
                                {
                                    Text = str,
                                    Foreground = new SolidColorBrush(Colors.Green),
                                    FontSize = ArticleFontSize - 8,
                                    FontFamily = ArticleFontFamily
                                };
                                Content.Add(tb);
                            }

                            //本文過濾完畢
                            ParsedContent = true;
                            row++; ParsedLine++;
                            break;
                        }
                    }
                    //網址
                    else if ((match = new Regex(http_exp).Match(str)).Success)
                    {
                        if (paragraph.Inlines.Count > 0)
                        {
                            Content.Add(CreateTextBlock(paragraph));
                            paragraph = new Paragraph();
                        }

                        string url = str.Substring(match.Index, match.Length);

                        CreateUriView(url);
                    }
                    //內文(非網址)
                    else
                    {
                        int color = RawLines[row][0].ForegroundColor;
                        int index = 0;
                        for (int i = 0; i < RawLines[row].Length; i++)
                        {
                            Block b = RawLines[row][i];
                            if (color != b.ForegroundColor)
                            {
                                string text = LiPTT.GetString(RawLines[row], index, i - index);

                                /****
                                InlineUIContainer container = new InlineUIContainer
                                {
                                    Child = new Border()
                                    {
                                        Background = GetBackgroundBrush(RawLines[row][index]),
                                        Child = new TextBlock()
                                        {
                                            IsTextSelectionEnabled = true,
                                            Text = text.Replace('\0', ' '),
                                            FontSize = ArticleFontSize,
                                            FontFamily = ArticleFontFamily,
                                            Foreground = GetForegroundBrush(RawLines[row][index]),
                                        }
                                    }
                                };
                                /***/
                                Run container = new Run()
                                {
                                    Text = text.Replace('\0', ' '),
                                    FontSize = ArticleFontSize,
                                    FontFamily = ArticleFontFamily,
                                    Foreground = GetForegroundBrush(RawLines[row][index]),
                                };

                                paragraph.Inlines.Add(container);
                                


                                index = i;
                                color = b.ForegroundColor;
                            }
                        }

                        if (index + 1 < RawLines[row].Length)
                        {
                            string text = LiPTT.GetString(RawLines[row], index, RawLines[row].Length - index);

                            /***
                            InlineUIContainer container = new InlineUIContainer
                            {
                                Child = new Border()
                                {
                                    Background = GetBackgroundBrush(RawLines[row][index]),
                                    Child = new TextBlock()
                                    {
                                        IsTextSelectionEnabled = true,
                                        Text = text.Replace('\0', ' '),
                                        FontSize = ArticleFontSize,
                                        FontFamily = ArticleFontFamily,
                                        Foreground = GetForegroundBrush(RawLines[row][index]),
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
                                Foreground = GetForegroundBrush(RawLines[row][index]),
                            };
                            /***/
                            paragraph.Inlines.Add(container);
                        }

                        paragraph.Inlines.Add(new LineBreak());
                    }
                }

                if (PageDownPercent == 100 && ParsedContent == false)
                {
                    if (paragraph.Inlines.Count > 0)
                    {
                        Content.Add(CreateTextBlock(paragraph));
                        paragraph = new Paragraph();
                    }
                    ParsedContent = true;
                }

                //過濾推文
                for (; row < RawLines.Count; row++, ParsedLine++)
                {
                    str = LiPTT.GetString(RawLines[row], 0, RawLines[row].Length - 13);

                    Echo echo = new Echo();

                    if (str.StartsWith("推"))
                    {
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

                        echo.Evaluation = Evaluation.推;
                    }
                    else if (str.StartsWith("噓"))
                    {
                        int index = 2;
                        int end = index;
                        while (str[end] != ':') end++;

                        string auth = str.Substring(index, end - index);

                        echo.Author = auth.Trim();

                        echo.Content = str.Substring(end + 1);

                        string time = LiPTT.GetString(RawLines[row], 67, 11);

                        try
                        {
                            System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                            echo.Date = DateTimeOffset.ParseExact(time, "MM/dd HH:mm", provider);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }

                        echo.Evaluation = Evaluation.噓;
                    }
                    else if (str.StartsWith("→"))
                    {
                        int index = 2;
                        int end = index;
                        while (str[end] != ':') end++;

                        string auth = str.Substring(index, end - index);

                        echo.Author = auth.Trim();

                        echo.Content = str.Substring(end + 1);

                        string time = LiPTT.GetString(RawLines[row], 67, 11);

                        try
                        {
                            System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                            echo.Date = DateTimeOffset.ParseExact(time, "MM/dd HH:mm", provider);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }

                        echo.Evaluation = Evaluation.箭頭;
                    }
                    else if (str.StartsWith("※"))
                    {
                        Debug.WriteLine(str);
                        echo = null;
                    }
                    else
                    {
                        //原POの文
                        Debug.WriteLine(str);
                        echo = null;
                    }

                    if (echo != null) Echoes.Add(echo);
                }
            }
            else
            { 
                //過濾推文
                for (; row < RawLines.Count; row++, ParsedLine++)
                {
                    str = LiPTT.GetString(RawLines[row], 0, RawLines[row].Length - 13);

                    Echo echo = new Echo();

                    if (str.StartsWith("推"))
                    {
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

                        echo.Evaluation = Evaluation.推;
                    }
                    else if (str.StartsWith("噓"))
                    {
                        int index = 2;
                        int end = index;
                        while (str[end] != ':') end++;

                        string auth = str.Substring(index, end - index);

                        echo.Author = auth.Trim();

                        echo.Content = str.Substring(end + 1);

                        string time = LiPTT.GetString(RawLines[row], 67, 11);

                        try
                        {
                            System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                            echo.Date = DateTimeOffset.ParseExact(time, "MM/dd HH:mm", provider);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }

                        echo.Evaluation = Evaluation.噓;
                    }
                    else if (str.StartsWith("→"))
                    {
                        int index = 2;
                        int end = index;
                        while (str[end] != ':') end++;

                        string auth = str.Substring(index, end - index);

                        echo.Author = auth.Trim();

                        echo.Content = str.Substring(end + 1);

                        string time = LiPTT.GetString(RawLines[row], 67, 11);

                        try
                        {
                            System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                            echo.Date = DateTimeOffset.ParseExact(time, "MM/dd HH:mm", provider);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }

                        echo.Evaluation = Evaluation.箭頭;
                    }
                    else if (str.StartsWith("※"))
                    {
                        Debug.WriteLine(str);
                        echo = null;
                    }
                    else
                    {
                        //原POの文
                        Debug.WriteLine(str);
                        echo = null;
                    }

                    if (echo != null) Echoes.Add(echo);
                }
            }
        }

        private SolidColorBrush GetForegroundBrush(Block b)
        {
            switch (b.ForegroundColor)
            {
                case 30:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
                case 31:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0x00)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0x00, 0x00));
                case 32:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0x00)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xC0, 0x00));
                case 33:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0x00));
                case 34:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0xFF)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0xC0));
                case 35:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0xFF)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0x00, 0xC0));
                case 36:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0xFF)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xC0, 0xC0));
                case 37:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)):
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

        private WebView GetYoutubeView(string youtubeID, double width = 0, double height = 0)
        {
            double w = width == 0 ? ViewWidth * 0.8 : width;
            double h = height == 0 ? w * 0.5625 : height;
            WebView wv = new WebView() { Width = w, Height = h, DefaultBackgroundColor = Colors.Black };

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

        private RichTextBlock CreateTextBlock(Paragraph paragraph)
        {
            RichTextBlock textblock = new RichTextBlock()
            {
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.Wrap,
            };

            textblock.Blocks.Add(paragraph);

            return textblock;
        }

        private bool IsShortCut(string host)
        {
            if (ShortCutSet.Contains(host))
            {
                return true;
            }
            else
            {
                return false;
            }
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
                Content.Add(grid);

                //SomeTasks.Add(CreateImageView(Content.Count - 1, uri));
                DownloadTasks.Add(CreateImageView(Content.Count - 1, uri));
            }
            else if (uri.Host == "imgur.com")
            {
                string str = uri.OriginalString;
                
                if (str.IndexOf("imgur.com/a") != -1)
                {
                    HyperlinkButton hyper = new HyperlinkButton()
                    {
                        Content = new TextBlock() { Text = uri.OriginalString },
                        NavigateUri = uri,
                        FontSize = ArticleFontSize,
                        FontFamily = ArticleFontFamily,
                    };

                    Content.Add(hyper);
                }
                else
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
                        Content.Add(grid);

                        DownloadTasks.Add(CreateImageView(Content.Count - 1, new_uri));
                    }
                }

                
            }
            else if (IsYoutubeUri(uri))
            {
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
            else
            {
                HyperlinkButton hyper = new HyperlinkButton()
                {
                    Content = new TextBlock() { Text = uri.OriginalString },
                    NavigateUri = uri,
                    FontSize = ArticleFontSize,
                    FontFamily = ArticleFontFamily,  
                };

                Content.Add(hyper);
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

            if (bmp.PixelWidth < ViewWidth * (1-space))
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
            Content.Add(grid);
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
            else
            {
                return false;
            }
        }

        private bool IsYoutubeUri(Uri uri)
        {
            if (uri.Host == "www.youtube.com")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override string ToString()
        {
            string like;

            if (Like >= 100) like = "爆";
            else if (Like < 0)
            {
                like = string.Format("X{0}", -Like / 10);
            }
            else if (Like == 0)
            {
                like = "  ";
            }
            else
            {
                like = string.Format("{0,2}", Like);
            }

            string type = " ";

            if (ReadType.HasFlag(ReadType.被標記))
            {
                if (ReadType.HasFlag(ReadType.已讀)) type = "m";
                else type = "m";
            }
            else if (ReadType.HasFlag(ReadType.待處理))
            {
                if (ReadType.HasFlag(ReadType.已讀)) type = "s";
                else type = "S";
            }
            else if (ReadType.HasFlag(ReadType.被鎖定))
            {
                type = "!";
            }
            if (ReadType.HasFlag(ReadType.有推文))
            {
                if (ReadType.HasFlag(ReadType.被標記)) type = "=";
                else type = "~";
            }

            StringBuilder sb = new StringBuilder();
            if (Reply) sb.Append("R:");
            else sb.Append("□");
            sb.Append(' ');
            sb.AppendFormat("[{0}]", Subtitle);
            sb.Append(' ');
            sb.Append(Title);

            return String.Format(" {0,6} {1} {2}{3,5} {4,-12} {5}", ID, type, like, Date.ToString("M/dd"), Author, sb.ToString());
        }

        public int CompareTo(Article other)
        {
            if (this.ID != uint.MaxValue && other.ID != uint.MaxValue)
            {
                if (this.ID > other.ID) return -1; //大的排前面
                else if (this.ID < other.ID) return 1;
                else return 0;
            }
            else if (this.ID == uint.MaxValue && other.ID == uint.MaxValue)
            {
                if (this.Star > other.Star) return -1;
                else if (this.Star < other.Star) return 1;
                else return 0;
            }
            else
            {
                if (this.ID == uint.MaxValue) return -1;
                else if (other.ID == uint.MaxValue) return 1;
                else return 0;
            }
        }
    }

    public class ArticleContentCollection : ObservableCollection<object>
    {
        public ArticleContentCollection()
        {
            
        }

    }

    ///關於UWP ObservableCollection Sorting and Grouping
    //https://stackoverflow.com/questions/34915276/uwp-observablecollection-sorting-and-grouping
    //https://github.com/Microsoft/Windows-universal-samples/tree/master/Samples/XamlListView

    public class ArticleCollection : ObservableCollection<Article>, ISupportIncrementalLoading
    {
        private BoardInfo boardInfo;

        public BoardInfo BoardInfo
        {
            get { return boardInfo; }
            set
            {
                boardInfo = value;
                var a = LiPTT.RunInUIThread(() => { OnPropertyChanged(new PropertyChangedEventArgs("BoardInfo")); });
            }
        }

        public SemaphoreSlim locker;

        public int StarCount { get; set; }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
        }

        private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint count)
        {
            

            if (CurrentIndex == 0)
            {
                HasMoreItems = false;
            }
            else if(!reading)
            {
                reading = true;
                await locker.WaitAsync();
                LiPTT.PttEventEchoed += ReadBoard_EventEchoed;
                LiPTT.SendMessage(CurrentIndex.ToString(), 0x0D);
            }

            return new LoadMoreItemsResult { Count = CurrentIndex };
        }

        private bool reading;

        private void ReadBoard_EventEchoed(PTTProvider sender, LiPttEventArgs e)
        {
            if (e.State == PttState.Board)
            {
                ReadBoard(e.Screen);
            }
        }

        private void ReadBoard(ScreenBuffer screen)
        {
            Regex regex;
            Match match;
            string str;
            int star = StarCount + 1;
            /////////////////////////////
            ///文章
            ///
            uint id = uint.MaxValue;

            for (int i = 22; i >= 3; i--)
            {
                Article article = new Article();

                //ID流水號
                str = screen.ToString(i, 0, 8);

                if (str.IndexOf('★') != -1)
                {
                    article.ID = uint.MaxValue;
                    article.Star = star++;
                    StarCount = article.Star;
                }
                else
                {
                    match = new Regex(@"\d+").Match(str);
                    if (match.Success)
                    {
                        id = Convert.ToUInt32(str.Substring(match.Index, match.Length));
                        article.ID = id;

                        if (id > CurrentIndex) continue;
                        //
                        //if (this.Any(x => x.ID == article.ID)) continue;
                    }
                }

                //推文數
                str = screen.ToString(i, 9, 2);

                if (str[0] == '爆')
                {
                    article.Like = 100;
                }
                else if (str[0] == 'X')
                {
                    if (str[1] == 'X')
                    {
                        article.Like = -100;
                    }
                    else
                    {
                        article.Like = Convert.ToInt32(str[1].ToString());
                        article.Like = -article.Like * 10;
                    }
                }
                else
                {
                    regex = new Regex(@"(\d+)");
                    match = regex.Match(str);
                    if (match.Success) article.Like = Convert.ToInt32(str.Substring(match.Index, match.Length));
                    else article.Like = 0;
                }

                //ReadType
                char c = (char)screen[i][8].Content;
                article.ReadType = LiPTT.GetReadType(c);
                
                //日期
                str = screen.ToString(i, 11, 5);
                try
                {
                    article.Date = DateTimeOffset.Parse(str);
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(str + ex.ToString());
                }

                //作者
                str = screen.ToString(i, 17, 13).Replace('\0', ' ');
                regex = new Regex(@"[\w\S]+");
                match = regex.Match(str);
                if (match.Success) article.Author = str.Substring(match.Index, match.Length);

                //標題
                str = screen.ToString(i, 30, screen.Width - 30).Replace('\0', ' ');
                regex = new Regex(@"R:");
                match = regex.Match(str);
                if (match.Success) article.Reply = true;
                else article.Reply = false;

                //是否被刪除?
                if (article.Author == "-") article.Deleted = true;
                else article.Deleted = false;

                if (article.Deleted)
                {
                    article.Title = str;
                    regex = new Regex(@"\[\S+\]");
                    match = regex.Match(str);
                    if (match.Success)
                    {
                        //刪除的人
                        article.Author = str.Substring(match.Index + 1, match.Length - 2);
                        article.Title = "(本文已被刪除)";
                    }
                    else
                    {
                        //被其他人刪除
                        regex = new Regex(@"\(已被\S+刪除\)");
                        match = regex.Match(str);
                        if (match.Success)
                        {
                            article.Author = str.Substring(match.Index + 3, match.Length - 6);
                        }
                        article.Title = str.Substring(1);
                    }
                }
                else
                {
                    //標題, 分類
                    regex = new Regex(@"\[\S+\]");
                    match = regex.Match(str);
                    if (match.Success)
                    {
                        article.Subtitle = str.Substring(match.Index + 1, match.Length - 2);
                        str = str.Substring(match.Index + match.Length);
                        int k = 0;
                        while (k < str.Length && str[k] == ' ') k++;
                        int j = str.Length - 1;
                        while (j >= 0 && str[j] == ' ') j--;
                        if (k <= j) article.Title = str.Substring(k, j - k + 1);
                    }
                    else
                    {
                        article.Title = str.Substring(2);
                    }
                }

                var action = LiPTT.RunInUIThread(() =>
                {
                    this.Add(article);
                });
            }

            CurrentIndex = id - 1;

            LiPTT.PttEventEchoed -= ReadBoard_EventEchoed;
            reading = false;
            locker.Release();
        }

        /// <summary>
        /// 當前位置
        /// </summary>
        public uint CurrentIndex;

        public ArticleCollection()
        {
            CurrentIndex = uint.MaxValue;
            StarCount = 0;
            locker = new SemaphoreSlim(1, 1);
            BoardInfo = new BoardInfo();

            this.CollectionChanged += ArticleCollection_CollectionChanged;
            //用了他就變超級卡...
            //this.PropertyChanged += ArticleCollection_PropertyChanged;
        }

        private void ArticleCollection_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var a = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                OnPropertyChanged(new PropertyChangedEventArgs(e.PropertyName));
            });
        }

        private void ArticleCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (Article item in e.NewItems)
                    item.PropertyChanged += MyType_PropertyChanged;

            if (e.OldItems != null)
                foreach (Article item in e.OldItems)
                    item.PropertyChanged -= MyType_PropertyChanged;
        }

        private void MyType_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, sender, sender, IndexOf((Article)sender));

            var t = LiPTT.RunInUIThread(() =>
            {
                OnCollectionChanged(args);
            });
        }

        private bool more;

        public bool HasMoreItems
        {
            get
            {
                if (!reading) return more;
                else return false;
            }
            set { more = value; }
        }
    }

    //只能用這個了...
    //https://stackoverflow.com/questions/40052378/isupportincrementalloading-gridview-within-a-scrollviewer-fires-loadmoreitemsasy
    //https://msdn.microsoft.com/en-us/windows/uwp/debug-test-perf/optimize-gridview-and-listview#ui-virtualization
    public class EchoCollection : ObservableCollection<object>, ISupportIncrementalLoading
    {
        private SemaphoreSlim locker;

        public EchoCollection()
        {
            locker = new SemaphoreSlim(1, 1);
            loading = false;
            more = false;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
        }

        private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint count)
        {
            if (Percent == 100)
            {
                HasMoreItems = false;
            }
            else if (!loading)
            {
                await locker.WaitAsync();
                loading = true;
                LiPTT.PttEventEchoed += LiPTT_PttEventEchoed;
                LiPTT.PageDown();
            }

            return new LoadMoreItemsResult { Count = (uint)this.Count };
        }

        private void LiPTT_PttEventEchoed(PTTProvider sender, LiPttEventArgs e)
        {
            if (e.State == PttState.Article)
            {
                LiPTT.PttEventEchoed -= LiPTT_PttEventEchoed;
                var t = LiPTT.RunInUIThread(() => { LoadEchoes(e.Screen); });
                
            }
        }

        private bool loading = false;

        private void LoadEchoes(ScreenBuffer screen)
        {
            Bound bound = ReadLineBound(screen.ToString(23));

            Percent = bound.Percent;

            Echo echo = null;

            if (Percent < 100)
            {
                for (int i = 1; i < 23; i++)
                {
                    Article.RawLines.Add(screen[i]);
                    echo = CreateEcho(screen[i]);
                    if (echo != null) this.Add(echo);
                }
            }
            else if (Percent == 100)
            {
                //最後一頁
                for (int i = Article.RawLines.Count - bound.Begin + 4; i < 23; i++)
                {
                    Article.RawLines.Add(screen[i]);
                    echo = CreateEcho(screen[i]);
                    if (echo != null) this.Add(echo);
                }
            }

            loading = false;
            locker.Release();
        }

        private Echo CreateEcho(Block[] blocks)
        {
            string str = LiPTT.GetString(blocks, 0, blocks.Length - 13);

            Echo echo = new Echo();

            if (str.StartsWith("推"))
            {
                int index = 2;
                int end = index;
                while (str[end] != ':') end++;

                string auth = str.Substring(index, end - index);

                echo.Author = auth.Trim();

                echo.Content = str.Substring(end + 1);

                string time = LiPTT.GetString(blocks, 67, 11);
                //https://msdn.microsoft.com/zh-tw/library/8kb3ddd4(v=vs.110).aspx
                try
                {
                    System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                    echo.Date = DateTimeOffset.ParseExact(time, "MM/dd HH:mm", provider);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(str + ex.ToString());
                }

                echo.Evaluation = Evaluation.推;
            }
            else if (str.StartsWith("噓"))
            {
                int index = 2;
                int end = index;
                while (str[end] != ':') end++;

                string auth = str.Substring(index, end - index);

                echo.Author = auth.Trim();

                echo.Content = str.Substring(end + 1);

                string time = LiPTT.GetString(blocks, 67, 11);

                try
                {
                    System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                    echo.Date = DateTimeOffset.ParseExact(time, "MM/dd HH:mm", provider);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }

                echo.Evaluation = Evaluation.噓;
            }
            else if (str.StartsWith("→"))
            {
                int index = 2;
                int end = index;
                while (str[end] != ':') end++;

                string auth = str.Substring(index, end - index);

                echo.Author = auth.Trim();

                echo.Content = str.Substring(end + 1);

                string time = LiPTT.GetString(blocks, 67, 11);

                try
                {
                    System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                    echo.Date = DateTimeOffset.ParseExact(time, "MM/dd HH:mm", provider);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }

                echo.Evaluation = Evaluation.箭頭;
            }
            else if (str.StartsWith("※"))
            {
                Debug.WriteLine(str);
                echo = null;
            }
            else
            {
                Debug.WriteLine(str);
                echo = null;
            }

            return echo;
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

        public Article Article { get; set; }

        public int Percent { get; set; }

        private bool more;

        public bool HasMoreItems
        {
            get
            {
                if (!loading) return more;
                else return false;
            }
            set { more = value; }
        }
    }

    /***
    //http://jacopretorius.net/2010/01/implementing-a-custom-linq-provider.html
    public class ArticleCollectionProvider : IQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
        {
            Type elementType = TypeSystem.GetElementType(expression.Type);
            try
            {
                return (IQueryable)Activator.CreateInstance(typeof(ArticleCollection).MakeGenericType(elementType), new object[] { this, expression });
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        public IQueryable CreateQuery<Article>(Expression expression)
        {
            return new ArticleCollection(this, expression);
        }

        public object Execute(Expression expression)
        {
            return ArticleCollection.Execute(expression, false);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            bool IsEnumerable = (typeof(TResult).Name == "IEnumerable`1");

            return (TResult)ArticleCollection.Execute(expression, IsEnumerable);
        }
    }

        class TerraServerQueryContext
    {
        // Executes the expression tree that is passed to it.
        internal static object Execute(Expression expression, bool IsEnumerable)
        {
            // The expression must represent a query over the data source.
            if (!IsQueryOverDataSource(expression))
                throw new InvalidProgramException("No query over the data source was specified.");

            // Find the call to Where() and get the lambda expression predicate.
            InnermostWhereFinder whereFinder = new InnermostWhereFinder();
            MethodCallExpression whereExpression = whereFinder.GetInnermostWhere(expression);
            LambdaExpression lambdaExpression = (LambdaExpression)((UnaryExpression)(whereExpression.Arguments[1])).Operand;

            // Send the lambda expression through the partial evaluator.
            lambdaExpression = (LambdaExpression)Evaluator.PartialEval(lambdaExpression);

            // Get the place name(s) to query the Web service with.
            LocationFinder lf = new LocationFinder(lambdaExpression.Body);
            List<string> locations = lf.Locations;
            if (locations.Count == 0)
                throw new InvalidQueryException("You must specify at least one place name in your query.");

            // Call the Web service and get the results.
            Place[] places = WebServiceHelper.GetPlacesFromTerraServer(locations);

            // Copy the IEnumerable places to an IQueryable.
            IQueryable<Place> queryablePlaces = places.AsQueryable<Place>();

            // Copy the expression tree that was passed in, changing only the first
            // argument of the innermost MethodCallExpression.
            ExpressionTreeModifier treeCopier = new ExpressionTreeModifier(queryablePlaces);
            Expression newExpressionTree = treeCopier.Visit(expression);

            // This step creates an IQueryable that executes by replacing Queryable methods with Enumerable methods.
            if (IsEnumerable)
                return queryablePlaces.Provider.CreateQuery(newExpressionTree);
            else
                return queryablePlaces.Provider.Execute(newExpressionTree);
        }

        private static bool IsQueryOverDataSource(Expression expression)
        {
            // If expression represents an unqueried IQueryable data source instance,
            // expression is of type ConstantExpression, not MethodCallExpression.
            return (expression is MethodCallExpression);
        }
    }
    /***/

}
