using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LiPTT
{
    //https://docs.microsoft.com/en-us/windows/uwp/debug-test-perf/listview-and-gridview-data-optimization

    public sealed partial class BoardPage : Page, INotifyPropertyChanged
    {
        public BoardPage()
        {
            
            InitializeComponent();
            CurrentBoard = new Board();
            LiPTT.ArticleCollection = ContentCollection;
            clipboardsem = new SemaphoreSlim(1, 1);
            ContentCollection.BeginLoaded += ContentCollection_BeginLoaded;
        }

        public Board CurrentBoard
        {
            get; set;
        }

        SemaphoreSlim clipboardsem;

        private async void Clipboard_ContentChanged(object sender, object e)
        {
            DataPackageView dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                string text = await dataPackageView.GetTextAsync();
                Clipboard.Clear();
                // To output the text from this example, you need a TextBlock control
                Debug.WriteLine("Clipboard now contains: " + text);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool control_visible;

        public bool ControlVisible
        {
            get
            {
                return control_visible;
            }
            private set
            {
                control_visible = value;
                NotifyPropertyChanged("ControlVisible");
                NotifyPropertyChanged("RingActive");
            }
        }

        public bool RingActive
        {
            get
            {
                return !control_visible;
            }
        }

        private void ReadBoardInfomation()
        {
            if (LiPTT.State == PttState.Board)
            {
                string str = LiPTT.Screen.ToString(2);
                Regex regex = new Regex(@"\d+");
                Match match = regex.Match(str);

                if (match.Success)
                {
                    int popu = Convert.ToInt32(str.Substring(match.Index, match.Length));
                    var Board = CurrentBoard;
                    CurrentBoard.Popularity = popu;
                    NotifyPropertyChanged("CurrentBoard");
                }
            }

            LiPTT.PttEventEchoed += ReadBoardInfo;
            LiPTT.PressI();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            ControlVisible = false;
            //冷靜一下，先喝杯咖啡
            await Task.Delay(100);

            ReadBoardInfomation();
            //追蹤剪貼簿
            //Clipboard.ContentChanged += Clipboard_ContentChanged;

        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            //Clipboard.ContentChanged -= Clipboard_ContentChanged;
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed -= Board_PointerPressed;
        }

        private void ReadBoardInfo(PTTClient client, LiPttEventArgs e)
        {
            ScreenBuffer screen = client.Screen;

            if (e.State == PttState.BoardInfomation)
            {
                LiPTT.PttEventEchoed -= ReadBoardInfo;

                var action = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var Board = CurrentBoard;

                    //看板名稱
                    string str = screen.ToString(3);
                    Match match = new Regex(LiPTT.bracket_regex).Match(str);
                    if (match.Success)
                    {
                        Board.Name = str.Substring(match.Index + 1, match.Length - 2);
                        Board.Nick = LiPTT.GetBoardNick(Board.Name);
                    }

                    //看板分類 中文敘述
                    Board.Category = screen.ToString(5, 15, 4);
                    Board.Description = client.Screen.ToString(5, 22, screen.Width - 22).Replace('\0', ' ').Trim();

                    //版主名單
                    str = screen.ToString(6, 15, screen.Width - 15).Replace('\0', ' ').Trim();
                    if (!new Regex(LiPTT.bound_regex).Match(str).Success) //(無)
                    {
                        Board.Leaders = str.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    if (screen.ToString(7, 25, 4) == "公開")
                        Board.公開 = true;

                    if (screen.ToString(8, 12, 4) == "可以")
                        Board.可進入十大排行榜 = true;

                    if (screen.ToString(9, 5, 4) == "開放")
                        Board.開放非看板會員發文 = true;

                    if (screen.ToString(10, 5, 4) == "開放")
                        Board.開放回文 = true;

                    if (screen.ToString(11, 5, 4) == "開放")
                        Board.開放自刪 = true;

                    if (screen.ToString(12, 5, 4) == "開放")
                        Board.開放推文 = true;

                    if (screen.ToString(13, 5, 4) == "開放")
                        Board.開放噓文 = true;

                    if (screen.ToString(14, 5, 4) == "開放")
                        Board.開放快速連推 = true;

                    if (screen.ToString(15, 12, 4) == "自動")
                        Board.IPVisible = true;

                    if (screen.ToString(16, 12, 4) == "對齊")
                        Board.自動對齊 = true;

                    if (screen.ToString(17, 10, 2) == "可")
                        Board.板主可刪除違規文字 = true;

                    if (screen.ToString(18, 14, 2) == "會")
                        Board.轉文自動記錄 = true;

                    if (screen.ToString(19, 5, 2) == "已")
                        Board.冷靜模式 = true;

                    if (screen.ToString(20, 5, 4) == "允許")
                        Board.允許十八歲進入 = true;

                    //發文限制 - 登入次數
                    str = screen.ToString(12);
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
                    str = screen.ToString(13);
                    match = new Regex(@"\d+").Match(str);
                    if (match.Success)
                    {
                        try
                        {
                            Board.LimitReject = Convert.ToInt32(str.Substring(match.Index, match.Length));
                        }
                        catch { Debug.WriteLine(str.Substring(match.Index, match.Length)); }
                    }                 

                    if (LiPTT.CacheBoard)
                    {
                        ControlVisible = true;
                        Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
                        Window.Current.CoreWindow.PointerPressed += Board_PointerPressed;
                    }
                    else
                    {
                        ContentCollection.Clear();
                        LiPTT.PttEventEchoed += InitBoard;
                    }

                    LiPTT.PressAnyKey();
                });
            }
        }

        private void InitBoard(PTTClient sender, LiPttEventArgs e)
        {
            if (e.State == PttState.Board)
            {
                LiPTT.PttEventEchoed -= InitBoard;
                var action = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ContentCollection.BeginLoad();
                });
            }
        }

        private void ContentCollection_BeginLoaded(object sender, EventArgs e)
        {
            if (ArticleListView.Items.Count > 0)
                ArticleListView.ScrollIntoView(ArticleListView.Items[0]);
            ControlVisible = true;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed += Board_PointerPressed;
        }


        //進入文章
        private void ArticleList_ItemClick(object sender, ItemClickEventArgs e)
        {
            Article article = e.ClickedItem as Article;
            if (article.Deleted) return;

            LiPTT.CurrentArticle = article;
            LiPTT.CacheBoard = true;

            if (article.AID is string s && s.Length > 0)
            {
                LiPTT.SendMessage(article.AID, 0x0D);
            }
            else
            {
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
            }

            LiPTT.Frame.Navigate(typeof(ArticlePage));
        }

        private void GoBack()
        {
            if (!control_visible || LiPTT.Frame.CurrentSourcePageType != typeof(BoardPage)) return;
            LiPTT.CacheBoard = false;
            LiPTT.Left();
            LiPTT.Frame.Navigate(typeof(PTTPage));
        }

        private bool pressRight = false;

        private async void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            bool Control_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;
            bool Shift_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0;

            if (Control_Down)
            {               
                if (args.VirtualKey == VirtualKey.V && SearchIDTextBox != FocusManager.GetFocusedElement())
                {
                    await clipboardsem.WaitAsync();
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
                    clipboardsem.Release();
                    return;
                }
            }
            else if (args.VirtualKey == VirtualKey.Escape || args.VirtualKey == VirtualKey.Left)
            {
                GoBack();
            }
            else if (args.VirtualKey >= VirtualKey.Number0 && args.VirtualKey <= VirtualKey.Number9 || args.VirtualKey >= VirtualKey.NumberPad0 && args.VirtualKey <= VirtualKey.NumberPad9)
            {
                if (Shift_Down)
                {
                    if (args.VirtualKey == VirtualKey.Number3 && SearchIDTextBox != FocusManager.GetFocusedElement())
                    {
                        SearchIDTextBox.Text = '#'.ToString();
                        SearchIDTextBox.SelectionStart = 1;
                        SearchIDTextBox.Focus(FocusState.Programmatic);
                    }
                }
                else
                {
                    if (args.VirtualKey >= VirtualKey.Number0 && args.VirtualKey <= VirtualKey.Number9)
                        SearchIDTextBox.Text = ((char)args.VirtualKey).ToString();
                    else
                        SearchIDTextBox.Text = ((args.VirtualKey - VirtualKey.NumberPad0)).ToString();

                    SearchIDTextBox.SelectionStart = 1;
                    SearchIDTextBox.Focus(FocusState.Programmatic);
                }
            }
        }

        private void Board_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            if (pressRight == false && args.CurrentPoint.Properties.IsRightButtonPressed)
            {
                pressRight = true;
                Window.Current.CoreWindow.PointerReleased += Board_PointerReleased;
            }
        }

        private void Board_PointerReleased(CoreWindow sender, PointerEventArgs args)
        {
            if (pressRight)
            {
                pressRight = false;
                Window.Current.CoreWindow.PointerPressed -= Board_PointerPressed;
                Window.Current.CoreWindow.PointerReleased -= Board_PointerReleased;
                GoBack();
            }
        }

        private void SearchIDTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }


        private void SearchIDTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
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

        private void SearchIDEnter(PTTClient sender, LiPttEventArgs e)
        {
            Match match;

            ScreenBuffer screen = sender.Screen;

            if ((match = new Regex("請按任意鍵繼續").Match(screen.ToString(23))).Success)
            {
                LiPTT.PressAnyKey();
            }
            else
            {
                LiPTT.PttEventEchoed -= SearchIDEnter;

                Article article = ParseArticleTag(screen.CurrentBlocks);

                if (article != null && !article.Deleted)
                {
                    LiPTT.CurrentArticle = article;

                    var b = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        LiPTT.Frame.Navigate(typeof(ArticlePage));
                    });

                }

                var action = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    SearchIDTextBox.Text = "";
                });
            }

            

        }

        int star;

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
            article.State = LiPTT.GetReadSate(c);

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

            //文章類型
            str = LiPTT.GetString(b, 30, 2).Replace('\0', ' ');
            if (str.StartsWith("R:")) article.Type = ArticleType.回覆;
            else if (str.StartsWith("□")) article.Type = ArticleType.一般;
            else if (str.StartsWith("轉")) article.Type = ArticleType.轉文;
            else article.Type = ArticleType.無;
            str = LiPTT.GetString(b, 30, b.Length - 30).Replace('\0', ' ');

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
                    article.Category = str.Substring(match.Index + 1, match.Length - 2);
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

    }
}
