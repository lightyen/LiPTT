using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;

namespace LiPTT
{
    //https://docs.microsoft.com/en-us/windows/uwp/debug-test-perf/listview-and-gridview-data-optimization
    public sealed partial class BoardPage : Page
    {

        public BoardPage()
        {
            this.InitializeComponent();

        }

        private bool hasBoardInfo = false;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Debug.WriteLine(">>載入新看板");

            LiPTT.ArticleCollection = ArticleListView.ItemsSource as ArticleCollection;

            if (!hasBoardInfo) ReadBoardInfomation();

            //追蹤剪貼簿
            //Clipboard.ContentChanged += Clipboard_ContentChanged;

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        private async void Clipboard_ContentChanged(object sender, object e)
        {
            DataPackageView dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                string text = await dataPackageView.GetTextAsync();
                // To output the text from this example, you need a TextBlock control
                Debug.WriteLine( "Clipboard now contains: " + text);
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            //Clipboard.ContentChanged -= Clipboard_ContentChanged;
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            
        }

        int star;

        private bool CheckBoard()
        {
            if (LiPTT.State == PttState.Board)
            {
                ScreenBuffer screen = LiPTT.Current.Screen;

                string str = screen.ToString(0);

                //本版名稱
                string board_name = "";
                Regex regex = new Regex(@"(看板《[\w\S]+》)");
                Match match = regex.Match(str);
                if (match.Success)
                {
                    board_name = str.Substring(match.Index + 3, match.Length - 4);
                }

                if (board_name == LiPTT.ArticleCollection?.BoardInfo.Name) return true;
                else return false;
            }
            else
            {
                var x = LiPTT.Current.Screen.ToStringArray();
                Debug.WriteLine("到底是哪裡?");
                return false;
            }
            
        }

        private void UpdateUI()
        {
            var act = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //ArticleList.ItemsSource = LiPTT.ArticleCollection;
            });
        }

        private void ReadBoardInfomation()
        {
            LiPTT.PttEventEchoed += ReadBoardInfo;
            LiPTT.PressI();
        }

        private void ReadBoardInfo(PTTProvider sender, LiPttEventArgs e)
        {
            if (e.State == PttState.BoardInfomation)
            {
                var Board = new BoardInfo();

                //看板名稱
                string str = e.Screen.ToString(3);
                Match match = new Regex(@"(《[\w\S]+》)").Match(str);
                if (match.Success)
                {
                    Board.Name = str.Substring(match.Index + 1, match.Length - 2);
                }

                //看板分類 中文敘述
                Board.Category = e.Screen.ToString(5, 15, 4);
                Board.Description = e.Screen.ToString(5, 22, e.Screen.Width - 22).Replace('\0', ' ').Trim();

                //版主名單
                str = e.Screen.ToString(6, 15, e.Screen.Width - 15).Replace('\0', ' ').Trim();
                Board.Leaders = str.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                //發文限制 - 登入次數
                str = e.Screen.ToString(12);
                match = new Regex(@"\d+").Match(str);
                if (match.Success)
                {
                    try
                    {
                        Board.LimitLogin = Convert.ToInt32(str.Substring(match.Index, match.Length));
                    }
                    catch { Debug.WriteLine(str.Substring(match.Index, match.Length)); }
                }

                //發文限制 - 退文篇數
                str = e.Screen.ToString(13);
                match = new Regex(@"\d+").Match(str);
                if (match.Success)
                {
                    try
                    {
                        Board.LimitReject = Convert.ToInt32(str.Substring(match.Index, match.Length));
                    }
                    catch { Debug.WriteLine(str.Substring(match.Index, match.Length)); }
                }

                LiPTT.ArticleCollection.BoardInfo = Board;
                hasBoardInfo = true;

                LiPTT.PressAnyKey();
            }
            else if (e.State == PttState.Board)
            {
                LiPTT.PttEventEchoed -= ReadBoardInfo;
                Initialize(LiPTT.Current.Screen);
            }
        }

        private void Initialize(ScreenBuffer screen)
        {
            Regex regex;
            Match match;
            string str;
            star = 0;
            uint id = 0;

            ArticleCollection ArticleCollection = LiPTT.ArticleCollection;

            var x = screen.ToStringArray();

            IAsyncAction action;

            //人氣
            str = screen.ToString(2);
            regex = new Regex(@"\d+");
            match = regex.Match(str);

            if (match.Success)
            {
                int popu = Convert.ToInt32(str.Substring(match.Index, match.Length));
                ArticleCollection.BoardInfo.Popularity = popu;
            }

            /////////////////////////////
            ///置底文 and 其他文章
            ///

            action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => 
            {
                ArticleCollection.Clear();
            });
            


            for (int i = 22; i >= 3; i--)
            {
                Article article = new Article();

                //ID流水號
                str = screen.ToString(i, 0, 8);
                regex = new Regex(@"(\d+)");
                match = regex.Match(str);

                if (match.Success)
                {
                    article.ID = id = Convert.ToUInt32(str.Substring(match.Index, match.Length));
                }
                else if (str.IndexOf('★') != -1)
                {
                    article.ID = uint.MaxValue;
                    article.Star = star++;
                    ArticleCollection.StarCount = article.Star;
                }
                else
                {
                    continue;
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
                catch
                {
                    continue;
                }

                //作者
                str = screen.ToString(i, 17, 13).Replace('\0', ' ');
                regex = new Regex(@"[\w\S]+");
                match = regex.Match(str);
                if (match.Success) article.Author = str.Substring(match.Index, match.Length);

                //標題
                str = screen.ToString(i, 30, screen.Width - 30).Replace('\0', ' ');
                regex = new Regex(@"(R:)");
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

                action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ArticleCollection.Add(article);
                });
            }

            if (id > 1)
            {
                ArticleCollection.CurrentIndex = id - 1;
                ArticleCollection.HasMoreItems = true;
            }
        }

        //進入文章
        private void ArticleList_ItemClick(object sender, ItemClickEventArgs e)
        {
            Article article = e.ClickedItem as Article;
            if (article.Deleted) return;

            LiPTT.CurrentArticle = article;

            if (article.ID != uint.MaxValue)
            {
                LiPTT.SendMessage(article.ID.ToString(), 0x0D);
            }
            else //GoTo置底文
            {
                //LiPTT.PageEnd();
                List<byte> m = new List<byte>
                {
                    (byte)'$'
                };

                for (int i = 0; i < article.Star; i++)
                {
                    //LiPTT.Up();
                    m.Add(0x1B);
                    m.Add(0x5B);
                    m.Add(0x41);
                }
                LiPTT.SendMessage(m.ToArray()); 
            }

            LiPTT.Frame.Navigate(typeof(ArticlePage));
        }

        private void GoBack()
        {
            hasBoardInfo = false;
            LiPTT.Left();
            LiPTT.Frame.Navigate(typeof(MainFunctionPage));
        }

        private void GoLeft(object sender, RoutedEventArgs e)
        {
            GoBack();
        }

        private async void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            bool Control_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;
            bool Shift_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0;

            if (Control_Down)
            {               
                if (args.VirtualKey == VirtualKey.V && SearchIDTextBox != FocusManager.GetFocusedElement())
                {
                    DataPackageView dataPackageView = Clipboard.GetContent();
                    if (dataPackageView.Contains(StandardDataFormats.Text))
                    {
                        string ClipboardText = await dataPackageView.GetTextAsync();
                        Debug.WriteLine("Clipboard now contains: " + ClipboardText);

                        if (ClipboardText.StartsWith("#"))
                        {
                            LiPTT.SendMessage(ClipboardText, 0x0D);
                            LiPTT.PttEventEchoed += SearchIDEnter;
                        }
                        else
                        {
                            try
                            {
                                uint id = Convert.ToUInt32(ClipboardText);
                                LiPTT.SendMessage(id.ToString(), 0x0D);
                                LiPTT.PttEventEchoed += SearchIDEnter;
                            }
                            catch { }
                        }

                    }
                    return;
                }
            }
            else if (args.VirtualKey == VirtualKey.Escape || args.VirtualKey == VirtualKey.Left)
            {
                GoBack();
            }
            else if (args.VirtualKey >= VirtualKey.Number0 && args.VirtualKey <= VirtualKey.Number9)
            {
                if (Shift_Down)
                {
                    if (args.VirtualKey == VirtualKey.Number3)
                    {
                        Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
                        SearchIDTextBox.Text = '#'.ToString();
                        SearchIDTextBox.SelectionStart = 1;
                        SearchIDTextBox.Focus(FocusState.Programmatic);
                    } 
                }
                else
                {
                    Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
                    SearchIDTextBox.Text = ((char)args.VirtualKey).ToString();
                    SearchIDTextBox.SelectionStart = 1;
                    SearchIDTextBox.Focus(FocusState.Programmatic);
                }
            }
        }

        private void SearchIDTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        private Article ParseArticleTag(Block[] b)
        {
            string str;

            //ID流水號
            str = LiPTT.GetString(b, 0, 8);
            Regex regex = new Regex(@"(\d+)");
            Match match = regex.Match(str);

            Article article = new Article();

            uint id;

            if (match.Success)
            {
                article.ID = id = Convert.ToUInt32(str.Substring(match.Index, match.Length));
            }
            else if (str.IndexOf('★') != -1)
            {
                article.ID = uint.MaxValue;
                article.Star = star++;
                LiPTT.ArticleCollection.StarCount = article.Star;
            }
            else
            {
                return null;
            }

            //推文數
            str = LiPTT.GetString(b, 9, 2);

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
            char c = (char)b[8].Content;
            switch (c)
            {
                case '+':
                    article.ReadType = ReadType.None;
                    break;
                case 'M':
                    article.ReadType = ReadType.被標記;
                    break;
                case 'S':
                    article.ReadType = ReadType.待處理;
                    break;
                case 'm':
                    article.ReadType = ReadType.已讀 | ReadType.被標記;
                    break;
                case 's':
                    article.ReadType = ReadType.已讀 | ReadType.待處理;
                    break;
                case '!':
                    article.ReadType = ReadType.被鎖定;
                    break;
                case '~':
                    article.ReadType = ReadType.有推文;
                    break;
                case '=':
                    article.ReadType = ReadType.有推文 | ReadType.被標記;
                    break;
                case ' ':
                    article.ReadType = ReadType.已讀;
                    break;
                default:
                    article.ReadType = ReadType.Undefined;
                    break;
            }

            //日期
            str = LiPTT.GetString(b, 11, 5);
            try
            {
                article.Date = DateTimeOffset.Parse(str);
            }
            catch
            {
                return null;
            }

            //作者
            str = LiPTT.GetString(b, 17, 13).Replace('\0', ' ');
            regex = new Regex(@"[\w\S]+");
            match = regex.Match(str);
            if (match.Success) article.Author = str.Substring(match.Index, match.Length);

            //標題
            str = LiPTT.GetString(b, 30, LiPTT.Current.Screen.Width - 30).Replace('\0', ' ');
            regex = new Regex(@"(R:)");
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

            return article;
        }

        private void SearchIDTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                SearchIDTextBox.Text = "";
                FocusManager.TryMoveFocus(FocusNavigationDirection.Previous);
                FocusManager.TryMoveFocus(FocusNavigationDirection.Previous);
            }
            else if (e.Key == VirtualKey.Enter)
            {
                FocusManager.TryMoveFocus(FocusNavigationDirection.Previous);
                FocusManager.TryMoveFocus(FocusNavigationDirection.Previous);

                if (SearchIDTextBox.Text.StartsWith("#"))
                {
                    LiPTT.SendMessage(SearchIDTextBox.Text, 0x0D);
                    LiPTT.PttEventEchoed += SearchIDEnter;
                }
                else
                {
                    try
                    {
                        uint id = Convert.ToUInt32(SearchIDTextBox.Text);
                        LiPTT.SendMessage(id.ToString(), 0x0D);

                        LiPTT.PttEventEchoed += SearchIDEnter;
                    }
                    catch { }
                }
            }
        }

        private void SearchIDEnter(PTTProvider sender, LiPttEventArgs e)
        {
            Match match;

            if ((match = new Regex("請按任意鍵繼續").Match(e.Screen.ToString(23))).Success)
            {
                LiPTT.PressAnyKey();
            }
            else
            {
                LiPTT.PttEventEchoed -= SearchIDEnter;

                Article article = ParseArticleTag(e.Screen.CurrentBlocks);

                if (article != null)
                {
                    LiPTT.CurrentArticle = article;

                    var b = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        LiPTT.Frame.Navigate(typeof(ArticlePage));
                    });

                }

                var action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    SearchIDTextBox.Text = "";
                });
            }

            

        }
    }


}
