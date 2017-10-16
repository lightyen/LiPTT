using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.Gaming.Input;
using Windows.Storage;

namespace LiPTT
{
    public class PTTStateUpdatedEventArgs : EventArgs
    {
        public PttState State
        {
            get; set;
        }

        public ScreenBuffer Screen
        {
            get; set;
        }

        public int AIDLine
        {
            get; set;
        }

        public Article Article
        {
            get; set;
        }
    }

    public class SearchBoardUpdatedEventArgs : EventArgs
    {
        public List<string> Boards
        {
            get; set;
        }
    }

    public class ArticleContentUpdatedEventArgs : EventArgs
    {
        public Bound Bound
        {
            get; set;
        }

        public List<Block[]> Lines
        {
            get; set;
        }
    }

    public class ArticlesReceivedEventArgs : EventArgs
    {
        public List<Article> Articles
        {
            get; set;
        }
    }

    public class BoardInfomationCompletedEventArgs : EventArgs
    {
        public Board BoardInfomation
        {
            get; set;
        }
    }

    public class ArticleInfomationCompletedEventArgs : EventArgs
    {
        public Article Article
        {
            get; set;
        }
    }

    public partial class PTT : PTTClient
    {
        private PttState State { get; set; }

        public bool IsKicked { get { return State == PttState.Kicked; } }

        private Article CurrentArticle { get; set; }

        ScreenBuffer Cache;

        Bound Bound;

        /// <summary>
        /// 在線人數
        /// </summary>
        public int OnlineUsers { get; set; }
    }

    public partial class PTT
    {
        public delegate void PTTStateUpdatedHandler(object sender, PTTStateUpdatedEventArgs e);

        public event PTTStateUpdatedHandler PTTStateUpdated;

        public event PTTStateUpdatedHandler LoginStateUpdated;

        public event EventHandler GoToBoardCompleted;

        public event EventHandler GoToArticleCompleted;

        public delegate void SearchBoardUpdatedHandler(object sender, SearchBoardUpdatedEventArgs e);

        public event SearchBoardUpdatedHandler SearchBoardUpdated;

        public delegate void ArticleContentUpdatedHandler(object sender, ArticleContentUpdatedEventArgs e);

        public event ArticleContentUpdatedHandler ArticleContentUpdated;

        public event EventHandler ParseBugAlarmed;

        public delegate void BoardInfomationCompletedHandler(object sender, BoardInfomationCompletedEventArgs e);

        public event BoardInfomationCompletedHandler BoardInfomationCompleted;

        public delegate void ArticleInfomationCompletedEventHandler(object sender, ArticleInfomationCompletedEventArgs e);

        public event ArticleInfomationCompletedEventHandler ArticleInfomationCompleted;

        public delegate void ArticlesReceivedHandler(object sender, ArticlesReceivedEventArgs e);

        public event ArticlesReceivedHandler ArticlesReceived;

        public event PTTStateUpdatedHandler StateChangedCompleted;
    }

    public partial class PTT
    {
        public PTT()
        {

        }

        /// <summary>
        /// 開始連PTT囉
        /// </summary>
        private void TryConnect()
        {
            SettingProperty Setting = Application.Current.Resources["SettingProperty"] as SettingProperty;
            if (Setting.ConnectionSecurity is bool security)
                ConnectionSecurity = security;
            State = PttState.Connecting;
            Bound = new Bound();
            this.Connect();
        }

        protected override void OnPTTConnectionFailed(object sender, NetworkEventArgs e)
        {
            base.OnPTTConnectionFailed(sender, e);
        }

        private bool ParseLineBugAlarmed;

        protected override sealed void OnScreenUpdated(object sender, ScreenEventArgs e)
        {
            if (Match("瀏覽 第", 23))
            {
                State = PttState.Article;
                Bound b = GetBound(Screen.ToString(23));

                if (b.Begin != Bound.Begin)
                {
                    Bound = b;
                    Debug.WriteLine("瀏覽文章");
                    State = PttState.Article;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });

                    List<Block[]> linelist = new List<Block[]>();

                    int start_row = 0;

                    if (Bound.Begin != 1)
                    {
                        int r = ComparePrevious();
                        if (r != 0)
                        {
                            start_row = 23 - r;

                            if (start_row <= 0)
                            {
                                start_row = 23;
                                if (!ParseLineBugAlarmed)
                                {
                                    ParseLineBugAlarmed = true;
                                    ParseBugAlarmed?.Invoke(this, new EventArgs());
                                }
                            } 
                        }
                    }
                    else
                        ParseLineBugAlarmed = false;

                    CacheScreen();

                    for (int i = start_row; i < 23; i++)
                    {
                        linelist.Add(CopyLine(i));
                    }

                    if (!ParseLineBugAlarmed)
                        ArticleContentUpdated?.Invoke(this, new ArticleContentUpdatedEventArgs { Bound = b, Lines = linelist });
                }
            }
            else if (Match("您確定要離開", 22))
            {
                if (State != PttState.Exit)
                {
                    Debug.WriteLine("您確定要離開?");
                    State = PttState.Exit;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("主功能表", 0))
            {
                if (State != PttState.MainPage)
                {
                    string s = Screen.ToString(23);
                    Match match = new Regex(@"(線上[\d\s]+人)").Match(s);
                    if (match.Success)
                    {
                        string st = s.Substring(match.Index + 2, match.Length - 3).Trim();
                        try { OnlineUsers = Convert.ToInt32(st); }
                        catch { }
                    }
                    Debug.WriteLine("主功能表");
                    State = PttState.MainPage;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match(@"\(a\)增加看板", 23))
            {
                Debug.WriteLine("我的最愛");
                State = PttState.Favorite;
                PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
            }
            else if (Match(@"\(m\)加入/移出最愛", 23))
            {
                Debug.WriteLine("熱門看板");
                State = PttState.Popular;
                PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
            }
            else if (Match("相關資訊一覽表", 2))
            {
                Debug.WriteLine("搜尋相關看板");
                State = PttState.RelatedBoard;
                PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
            }
            else if (Match("請輸入看板名稱", 1))
            {
                Debug.WriteLine("請輸入看板名稱");
                State = PttState.SearchBoard;
                PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
            }
            else if (Match("看板設定", 3))
            {
                if (State != PttState.BoardInfomation)
                {
                    Debug.WriteLine("看板資訊");
                    State = PttState.BoardInfomation;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (GetAIDStartRow() is int line && line != -1)
            {
                if (State != PttState.AID)
                {
                    Debug.WriteLine("AID文章代碼");
                    State = PttState.AID;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State, AIDLine = line, Article = CurrentArticle });
                }
            }
            else if (Match(@"文章選讀  \(y\)回應\(X\)推文\(\^X\)轉錄", 23))
            {
                Debug.WriteLine("看板");
                Bound = new Bound();
                State = PttState.Board;
                
                PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
            }
            else if (Match("您想刪除其他重複登入的連線嗎", 22))
            {
                if (State != PttState.AlreadyLogin)
                {
                    Debug.WriteLine("您想刪除其他重複登入的連線嗎");
                    State = PttState.AlreadyLogin;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("密碼不對或無此帳號", 21))
            {
                if (State != PttState.WrongPassword)
                {
                    Debug.WriteLine("密碼不對或無此帳號");
                    State = PttState.WrongPassword;
                    PTTWrongResponse = true;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }

            }
            else if (Match("系統過載", 13))
            {
                if (State == PttState.OverLoading)
                {
                    Debug.WriteLine("系統過載");
                    State = PttState.OverLoading;
                    PTTWrongResponse = true;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("密碼正確", 21))
            {
                if (State != PttState.Accept)
                {
                    Debug.WriteLine("密碼正確");
                    State = PttState.Accept;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("登入中", 22))
            {
                if (State != PttState.Loginning)
                {
                    Debug.WriteLine("登入中");
                    State = PttState.Loginning;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("任意鍵", 23))
            {
                if (State != PttState.PressAny)
                {
                    Debug.WriteLine("請按任意鍵繼續");
                    Bound = new Bound();
                    State = PttState.PressAny;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("登入太頻繁", 23))
            {
                if (State != PttState.LoginSoMany)
                {
                    Debug.WriteLine("登入太頻繁 請稍後在試");
                    State = PttState.LoginSoMany;
                    PTTWrongResponse = true;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("更新與同步", 22))
            {
                if (State != PttState.Synchronizing)
                {
                    Debug.WriteLine("更新與同步中...");
                    State = PttState.Synchronizing;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("請輸入您的密碼", 21))
            {
                if (State != PttState.Password)
                {
                    Debug.WriteLine("請輸入您的密碼");
                    State = PttState.Password;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("您要刪除以上錯誤嘗試的記錄嗎", 23))
            {
                if (State != PttState.WrongLog)
                {
                    Debug.WriteLine("您要刪除以上錯誤嘗試的記錄嗎");
                    State = PttState.WrongLog;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("請輸入代號", 20))
            {
                if (State != PttState.Login)
                {
                    Debug.WriteLine("請輸入代號");
                    State = PttState.Login;
                    PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                }
            }
            else if (Match("您覺得這篇文章", 23))
            {
                Bound = new Bound();
                State = PttState.EchoType;
                PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
            }
            else if (Match("批踢踢實業坊        ◢▃██◥█◤", 4))
            {
                //小馬
                Debug.WriteLine("批踢踢實業坊");
            }
            else
            {
#if DEBUG
                var x = Screen.ToStringArray();
#endif
                Debug.WriteLine("這裡是哪裡?");
                State = PttState.Angel;
            }


            base.OnScreenUpdated(this, e);
        }

        public void GoToMain()
        {
            if (State == PttState.MainPage)
            {
                StateChangedCompleted?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
            }
            else
            {
                PTTStateUpdated += StateUpdated;
                void StateUpdated(object sender, PTTStateUpdatedEventArgs e)
                {
                    if (e.State == PttState.MainPage)
                    {
                        PTTStateUpdated -= StateUpdated;
                        StateChangedCompleted?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                    }
                    else
                    {
                        Left();
                    }
                }
                Left();
            }
        }

        public void LoadBoardInfomation()
        {
            if (State == PttState.Board)
            {
                LiPTT.CacheBoard = true;

                CurrentBoard = new Board();

                //人氣
                string str = Screen.ToString(2);
                Regex regex = new Regex(@"\d+");
                Match match = regex.Match(str);
                if (match.Success)
                {
                    int popu = Convert.ToInt32(str.Substring(match.Index, match.Length));
                    CurrentBoard.Popularity = popu;
                }

                PTTStateUpdated += ReadBoardInfo;
                PressI();
            }
        }

        private void ReadBoardInfo(object sender, PTTStateUpdatedEventArgs e)
        {
            if (State == PttState.BoardInfomation)
            {
                var Board = CurrentBoard;

                //看板名稱
                string str = Screen.ToString(3);
                Match match = new Regex(LiPTT.bracket_regex).Match(str);
                if (match.Success)
                {
                    Board.Name = str.Substring(match.Index + 1, match.Length - 2);
                    Board.Nick = LiPTT.GetBoardNick(Board.Name);
                }

                //看板分類 中文敘述
                Board.Category = Screen.ToString(5, 15, 4);
                Board.Description = Screen.ToString(5, 22, Screen.Width - 22).Replace('\0', ' ').Trim();

                //版主名單
                str = Screen.ToString(6, 15, Screen.Width - 15).Replace('\0', ' ').Trim();
                if (!new Regex(LiPTT.bound_regex).Match(str).Success) //(無)
                {
                    Board.Leaders = str.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                }

                if (Screen.ToString(7, 25, 4) == "公開")
                    Board.公開 = true;

                if (Screen.ToString(8, 12, 4) == "可以")
                    Board.可進入十大排行榜 = true;

                if (Screen.ToString(9, 5, 4) == "開放")
                    Board.開放非看板會員發文 = true;

                if (Screen.ToString(10, 5, 4) == "開放")
                    Board.開放回文 = true;

                if (Screen.ToString(11, 5, 4) == "開放")
                    Board.開放自刪 = true;

                if (Screen.ToString(12, 5, 4) == "開放")
                    Board.開放推文 = true;

                if (Screen.ToString(13, 5, 4) == "開放")
                    Board.開放噓文 = true;

                if (Screen.ToString(14, 5, 4) == "開放")
                    Board.開放快速連推 = true;

                if (Screen.ToString(15, 12, 4) == "自動")
                    Board.IPVisible = true;

                if (Screen.ToString(16, 12, 4) == "對齊")
                    Board.自動對齊 = true;

                if (Screen.ToString(17, 10, 2) == "可")
                    Board.板主可刪除違規文字 = true;

                if (Screen.ToString(18, 14, 2) == "會")
                    Board.轉文自動記錄 = true;

                if (Screen.ToString(19, 5, 2) == "已")
                    Board.冷靜模式 = true;

                if (Screen.ToString(20, 5, 4) == "允許")
                    Board.允許十八歲進入 = true;

                //發文限制 - 登入次數
                str = Screen.ToString(12);
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
                str = Screen.ToString(13);
                match = new Regex(@"\d+").Match(str);
                if (match.Success)
                {
                    try
                    {
                        Board.LimitReject = Convert.ToInt32(str.Substring(match.Index, match.Length));
                    }
                    catch { Debug.WriteLine(str.Substring(match.Index, match.Length)); }
                }


                PressAnyKey();
            }
            else if (e.State == PttState.Board)
            {
                PTTStateUpdated -= ReadBoardInfo;
                BoardInfomationCompleted?.Invoke(this, new BoardInfomationCompletedEventArgs { BoardInfomation = CurrentBoard });
            }
        }

        public bool HasArticle()
        {
            if (State == PttState.Board)
            {
                if (CurrentIndex > 0) return true;
            }

            return false;
        }

        private Board CurrentBoard;

        private void ReadArticleTag()
        {
            //Find Cursor
            //read Tag
            //press Q any
            //add list

            //event

        }

        public void GetArticles()
        {
            if (State == PttState.Board)
            {
                PTTStateUpdated += ReadBoard;
                if (CurrentIndex != uint.MaxValue)
                    Send(CurrentIndex.ToString(), 0x0D);
                else
                    PageEnd();
            }
        }

        private uint CurrentIndex;

        private async void ReadBoard(object sender, PTTStateUpdatedEventArgs e)
        {
            PTTStateUpdated -= ReadBoard;

            if (e.State == PttState.Board)
            {
                List<Article> ArticleTags = new List<Article>();

                Regex regex;
                Match match;
                string str;
                uint id = 0;

                /////////////////////////////
                ///置底文 and 其他文章
                ///
                for (int i = 22; i >= 3; i--)
                {
                    Article article = new Article();

                    //ID流水號
                    str = Screen.ToString(i, 0, 8);
                    if (str.IndexOf('★') != -1)
                    {
                        article.ID = uint.MaxValue;

                        await Task.Run(() => {
                            article.AID = GetFooterAID(i);
                        });
                    }
                    else
                    {
                        regex = new Regex(@"(\d+)");
                        match = regex.Match(str);

                        if (match.Success)
                        {
                            id = Convert.ToUInt32(str.Substring(match.Index, match.Length));
                            article.ID = id;

                            if (id > CurrentIndex) continue;

                            if (id != CurrentIndex && CurrentIndex != uint.MaxValue) //id 被游標遮住
                                article.ID = CurrentIndex;

                            CurrentIndex = article.ID - 1;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    //推文數
                    str = Screen.ToString(i, 9, 2);

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

                    //未讀、已讀、M文 等等等
                    article.State = LiPTT.GetReadSate((char)Screen[i][8].Content);

                    //文章日期
                    str = Screen.ToString(i, 11, 5);
                    try
                    {
                        article.Date = DateTimeOffset.Parse(str);
                    }
                    catch
                    {
                        continue;
                    }

                    //作者
                    str = Screen.ToString(i, 17, 13).Replace('\0', ' ');
                    regex = new Regex(@"[\w\S]+");
                    match = regex.Match(str);
                    if (match.Success) article.Author = str.Substring(match.Index, match.Length);

                    //文章類型
                    str = Screen.ToString(i, 30, 2).Replace('\0', ' ');
                    if (str.StartsWith("R:")) article.Type = ArticleType.回覆;
                    else if (str.StartsWith("□")) article.Type = ArticleType.一般;
                    else if (str.StartsWith("轉")) article.Type = ArticleType.轉文;
                    else article.Type = ArticleType.無;
                    //是否被刪除
                    if (article.Author == "-") article.Deleted = true;
                    else article.Deleted = false;

                    str = Screen.ToString(i, 30, Screen.Width - 30).Replace('\0', ' ');

                    if (article.Deleted)
                    {
                        article.Title = str;
                        regex = new Regex(LiPTT.bracket_regex);
                        match = regex.Match(str);
                        string s = str.Substring(1);
                        if (match.Success)
                        {
                            article.Author = str.Substring(match.Index + 1, match.Length - 2);
                            s = s.Replace(match.ToString(), "");
                        }
                        article.Title = s;
                        article.Type = ArticleType.無;
                    }
                    else
                    {
                        //標題, 分類
                        regex = new Regex(LiPTT.bracket_regex);
                        match = regex.Match(str);
                        if (match.Success)
                        {
                            article.Category = str.Substring(match.Index + 1, match.Length - 2).Trim();
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

                    ArticleTags.Add(article);
                }

                CurrentIndex = id - 1;
                ArticlesReceived?.Invoke(this, new ArticlesReceivedEventArgs { Articles = ArticleTags });
            }
        }

        private SemaphoreSlim GetAIDsem = new SemaphoreSlim(1, 1);

        private string GetFooterAID(int i)
        {
            string AID = "";

            string[] screen = Screen.ToStringArray();
            SemaphoreSlim sem = new SemaphoreSlim(0, 1);
            int cursor = IndexOfCursor(screen);
            List<byte> command = new List<byte>();

            if (cursor != i)
            {
                if (cursor > i) //Up
                {
                    for (int k = 0; k < (cursor - i); k++)
                    {
                        command.Add(0x1B);
                        command.Add(0x5B);
                        command.Add(0x41);
                    }
                }
                else if (cursor < i) //Down
                {
                    for (int k = 0; k < (i - cursor); k++)
                    {
                        command.Add(0x1B);
                        command.Add(0x5B);
                        command.Add(0x42);
                    }
                }
            }

            command.Add((byte)'Q');
            PTTStateUpdated += GetAID;

            void GetAID(object sender, PTTStateUpdatedEventArgs e)
            {
                if (e.State == PttState.AID)
                {
                    int row = GetAIDStartRow();

                    AID = Screen.ToString(row, 18, 9);

                    PressAnyKey();
                }
                else if (e.State == PttState.Board && AID.Length == 9)
                {
                    PTTStateUpdated -= GetAID;
                    Debug.WriteLine(string.Format("AID {0}", AID));
                    sem.Release();
                }
            }

            Send(command.ToArray());
            sem.Wait();

            return AID;
        }

        

        private int IndexOfCursor(string[] screen)
        {
            if (State == PttState.Board)
            {
                for(int i = 0; i < screen.Length; i++)
                {
                    if (screen[i].StartsWith(">") || screen[i].StartsWith("●")) return i;
                }
            }

            return -1;
        }
    }

    public partial class PTT
    {
        /// <summary>
        /// 使用者名稱 (通常至少4個字元，不過元老級的帳號是例外)
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// 密碼 (通常至少4個字元，新建帳號new可以不需要)
        /// </summary>
        public string Password { get; set; }

        SemaphoreSlim ExitSemaphore;

        public async Task ExitPTT()
        {
            if (IsConnected)
            {
                PTTStateUpdated += Exit_PTTStateUpdated;

                if (State != PttState.MainPage)
                    Left();
                else
                    Send('G', 0x0D);
                //
            }
            else
            {
                ExitSemaphore.Release();
            }

            await ExitSemaphore.WaitAsync();
        }

        private void Exit_PTTStateUpdated(object sender, PTTStateUpdatedEventArgs e)
        {
            if (e.State == PttState.Exit)
            {
                IsAppExit = true;
                Send('y', 0x0D);
            }
            else if (!IsAppExit)
            {
                if (e.State == PttState.MainPage)
                    Send('G', 0x0D);
                else
                    Left();
            }
            else if (e.State == PttState.PressAny)
            {
                PTTStateUpdated -= Exit_PTTStateUpdated;
                PressAnyKey();
                State = PttState.Disconnected;
                Disconnect();
                PTTStateUpdated?.Invoke(this, new PTTStateUpdatedEventArgs { State = State });
                ExitSemaphore.Release();
            }
        }

        public void Login(string user, string password)
        {
            User = user;
            Password = password;
            ExitSemaphore = new SemaphoreSlim(0, 1);

            PTTStateUpdated += EnterAccount;

            //Local Function
            void EnterAccount(object sender, PTTStateUpdatedEventArgs e)
            {
                switch (e.State)
                {
                    case PttState.Login:
                        EnterUserName();
                        break;
                    case PttState.Password:
                        PTTStateUpdated -= EnterAccount;
                        EnterPassword();
                        break;
                }
            }

            TryConnect();
        }

        public void EnterUserName()
        {
            Send(User, 0x0D);
        }

        public void EnterPassword()
        {
            Send(Password, 0x0D);
        }

        bool enterboardcompleted;

        public void GoToBoard(string board)
        {
            PTTStateUpdated += EnterBoardHandle;
            LiPTT.CacheBoard = false;
            enterboardcompleted = false;
            pressany = false;
            CurrentIndex = uint.MaxValue;
            Send('s', board, 0x0D);
        }

        bool pressany;

        private void EnterBoardHandle(object sender, PTTStateUpdatedEventArgs e)
        {
            switch (e.State)
            {
                case PttState.PressAny:
                    if (!pressany)
                    {
                        pressany = true;
                        Send('q', 0x30, 0x0D);
                    }               
                    break;
                case PttState.Board:
                    if (!pressany)
                    {
                        pressany = true;
                        Send(0x30, 0x0D);
                    }
                    else if (!enterboardcompleted)
                    {
                        var screen = Screen.ToStringArray();
                        int corsur = IndexOfCursor(screen);

                        if (corsur == 3)
                        {
                            enterboardcompleted = true;
                            PTTStateUpdated -= EnterBoardHandle;
                            GoToBoardCompleted?.Invoke(this, new EventArgs());
                        }
                    }

                    break;
            }
        }

        string search;
        List<string> SearchBoardList;

        public void SearchBoard(string board)
        {
            PTTStateUpdated += SearchBoardHandle;
            SearchBoardList = new List<string>();
            search = board;
            Send('s', search, 0x20);
        }

        private void SearchBoardHandle(object sender, PTTStateUpdatedEventArgs e)
        {
            Regex regex = new Regex(@"([\w-_]+)");
            Match match;

            switch (e.State)
            {
                case PttState.SearchBoard:
                    PTTStateUpdated -= SearchBoardHandle;
                    var msg = Screen.ToString(1, 34, 20).Trim();

                    match = regex.Match(msg);

                    if (match.Success)
                    {
                        string suggestion = msg.Substring(match.Index, match.Length);

                        if (search.Length <= suggestion.Length)
                            SearchBoardList.Add(suggestion);

                        SearchBoardUpdated?.Invoke(this, new SearchBoardUpdatedEventArgs { Boards = SearchBoardList });

                        ClearSearch();
                    }
                    break;
                case PttState.RelatedBoard:

                    for (int i = 3; i < 23; i++)
                    {
                        string k = Screen.ToString(i).Replace('\0', ' ');

                        match = regex.Match(k, 0);
                        if (match.Success) SearchBoardList.Add(k.Substring(match.Index, match.Length));

                        match = regex.Match(k, 22);
                        if (match.Success) SearchBoardList.Add(k.Substring(match.Index, match.Length));

                        match = regex.Match(k, 44);
                        if (match.Success) SearchBoardList.Add(k.Substring(match.Index, match.Length));

                    }

                    if (new Regex("按空白鍵可列出更多項目").Match(Screen.ToString(23)).Success)
                    {
                        PressSpace();
                    }
                    else
                    {
                        PTTStateUpdated -= SearchBoardHandle;

                        SearchBoardList.Sort();

                        SearchBoardUpdated?.Invoke(this, new SearchBoardUpdatedEventArgs { Boards = SearchBoardList });

                        ClearSearch();
                    }
                    break;
            }
        }

        private void ClearSearch()
        {
            var msg = Screen.ToString(1, 34, 20).Trim();
            Regex regex = new Regex(@"([\w-_]+)");
            Match match = regex.Match(msg);
            if (match.Success)
            {
                string search = msg.Substring(match.Index, match.Length);

                byte[] back = new byte[search.Length + 1];
                back[search.Length] = 0x0D;
                for (int i = 0; i < search.Length; i++) back[i] = 0x08;
                Send(back);
            }
        }

        public void PressUpdateEcho()
        {
            //意同：左 右 PageEnd
            Send(new byte[] { 0x71, 0x72, 0x24 }); //qr$
        }

        public void PressBackspace()
        {
            Send(0x08);
        }

        public void PressEnter()
        {
            Send(0x0D); // Carriage return
        }

        public void Yes()
        {
            Send('y', 0x0D);
        }

        public void No()
        {
            Send('n', 0x0D);
        }

        public void PressSpace()
        {
            Send(0x20); // ' '
        }

        public void PressAnyKey()
        {
            Left();
        }

        public void PressI()
        {
            Send(0x69);
        }

        public void PressKeepAlive()
        {
            Send(0x0C); //^L
        }

        public void Up()
        {
            Send(new byte[] { 0x1B, 0x5B, 0x41 }); //ESC[A
        }

        public void Down()
        {
            Send(new byte[] { 0x1B, 0x5B, 0x42 }); //ESC[B
        }

        public void Right()
        {
            Send('r');
            //Send(new byte[] { 0x1B, 0x5B, 0x43 }); //ESC[C
        }

        public void Left()
        {
            Send('q');
            //Send(new byte[] { 0x1B, 0x5B, 0x44 }); //ESC[D
        }

        public void PageDown()
        {
            Send(new byte[] { 0x06 });
        }

        public void PageUp()
        {
            Send(new byte[] { 0x02 });
        }

        public void PageHome()
        {
            Send(new byte[] { 0x30, 0x72 }); //0r
        }

        public void PageEnd()
        {
            Send(new byte[] { 0x24 }); //'$'
        }

        public void GoToFavorite()
        {
            Send(new byte[] { 0x46, 0x1B, 0x5B, 0x43 });
        }
    }

    public partial class PTT
    {
        private Block[] CopyLine(int row)
        {
            Block[] src = Screen[row];
            Block[] b = new Block[Screen.Width];
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = new Block
                {
                    Content = src[i].Content,
                    Mode = src[i].Mode,
                    ForegroundColor = src[i].ForegroundColor,
                    BackgroundColor = src[i].BackgroundColor
                };
            }
            return b;
        }

        private void CacheScreen()
        {
            Cache = new ScreenBuffer(Screen);
        }

        public int ComparePrevious()
        {
            var ps = Cache.ToStringArray();
            var cs = Screen.ToStringArray();

            for (int i = 0; i < ps.Length; i++)
            {
                ps[i] = ps[i].Replace('\0', ' ');
                cs[i] = cs[i].Replace('\0', ' ');
            }

            for (int i = 0; i < Screen.Height - 1; i++)
            {
                if (Intersect(ps, cs, i) == true)
                {
                    return i;
                }
            }

            return Screen.Height;
        }

        private bool Intersect(string[] prev, string[] current, int intersect)
        {
            int height = Screen.Height - 1;
            int range = height - intersect;

            for (int pi = intersect; pi < height; pi++)
            {
                if (prev[pi] != current[pi - intersect])
                {
                    return false;
                }
            }

            return true;
        }

        private bool Match(string regex, int row)
        {
            return new Regex(regex).Match(Screen.ToString(row)).Success;
        }

        private static Bound GetBound(string msg)
        {
            Bound bound = new Bound();

            Regex regex = new Regex(LiPTT.bound_regex);
            Match match = regex.Match(msg);

            if (match.Success)
            {
                string percent = msg.Substring(match.Index + 1, match.Length - 3);
                try
                {
                    bound.Progress = Convert.ToInt32(percent);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }

            regex = new Regex(@"第 \d+~\d+ 行");
            match = regex.Match(msg);

            if (match.Success)
            {
                try
                {
                    string s = msg.Substring(match.Index + 2, match.Length - 4);
                    string[] k = s.Split(new char[] { '~' }, StringSplitOptions.RemoveEmptyEntries);
                    bound.Begin = Convert.ToInt32(k[0]);
                    bound.End = Convert.ToInt32(k[1]);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }
            return bound;
        }

        private int GetAIDStartRow()
        {
            var x = Screen.ToStringArray();
            Regex regex = new Regex(@"文章代碼\(AID\)");
            for (int i = 0; i < Screen.Height - 4; i++)
            {
                if (regex.IsMatch(Screen.ToString(i)))
                {
                    if (Screen.ToString(i + 1).IndexOf("文章網址:") != -1)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }
    }

    public partial class PTT
    {

        private bool ReadArticleInfomationCompleted;

        public void GoToArticle(Article article)
        {
            PTTStateUpdated += ReadArticleInfomation;
            ReadArticleInfomationCompleted = false;

            if (article.AID?.Length == 9)
            {
                CurrentArticle = article;
                Debug.WriteLine(string.Format("Go To {0}", article.AID));
                Send(article.AID, 0x0D, 'r');
            }
            else if (article.ID != uint.MaxValue)
            {
                CurrentArticle = article;
                Debug.WriteLine(string.Format("Go To {0}", article.ID));
                Send(article.ID.ToString(), 0x0D, 'r');
            }
            else
            {
                CurrentArticle = null;
                PTTStateUpdated -= ReadArticleInfomation;
            }     
        }

        private async void ReadArticleInfomation(object sender, PTTStateUpdatedEventArgs e)
        {
            if (e.State == PttState.Article)
            {
                if(!ReadArticleInfomationCompleted)
                    Send('Q');
                else
                {
                    PTTStateUpdated -= ReadArticleInfomation;
                    ArticleInfomationCompleted?.Invoke(this, new ArticleInfomationCompletedEventArgs { Article = e.Article });
                }
            }
            else if (e.State == PttState.AID)
            {
                await LiPTT.RunInUIThread(() => 
                {
                    //AID
                    if (e.Article.AID?.Length != 9)
                    {
                        e.Article.AID = Screen.ToString(e.AIDLine, 18, 9);
                    }
                    //網頁版網址
                    string str = Screen.ToString(e.AIDLine + 1);
                    Regex regex = new Regex(LiPTT.http_regex);
                    Match match1 = regex.Match(str);
                    if (match1.Success)
                    {
                        string aaa = str.Substring(match1.Index, match1.Length);

                        try
                        {
                            string url = str.Substring(match1.Index, match1.Length);
                            e.Article.WebUri = new Uri(url);
                        }
                        catch (UriFormatException ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }
                    }

                    //P幣
                    string p = Screen.ToString(e.AIDLine + 2);
                    regex = new Regex(@"\d+");
                    Match match2 = regex.Match(p);
                    if (match2.Success)
                        e.Article.PttCoin = Convert.ToInt32(p.Substring(match2.Index, match2.Length));
                    else
                        e.Article.PttCoin = 0;
                });

                ReadArticleInfomationCompleted = true;
                PressAnyKey();
            }
            else if (e.State == PttState.Board)
            {
                Right();
            }
        }
    }
}
