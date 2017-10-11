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
    public enum PttState
    {
        None,
        Disconnected, //未連線
        Connecting, //連線中...
        ConnectFailedTCP, //連線失敗 TCP
        ConnectFailedWebSocket, //連線失敗 WebSocket
        Disconnecting, //斷線中
        Kicked, //被踢惹
        OverLoading, //PTT爆炸惹
        Login, // PTT歡迎畫面(輸入帳號)
        Password, //輸入密碼
        WrongPassword, //密碼錯誤
        Accept, //密碼正確
        Loginning, //登入中
        Synchronizing, //同步處理中
        LoginSoMany, //登入太頻繁
        AlreadyLogin, //重複登入
        MainPage, //主功能表
        Popular, //熱門看板列表
        PressAny, //按任意鍵喔
        WrongLog, //登入錯誤資訊
        Exit, //離開嗎?
        Favorite, //最愛看板列表
        SearchBoard, //搜尋看板
        RelatedBoard, //相關資訊一覽表
        Board, //文章列表
        BoardInfomation, //看板資訊
        Article, //閱覽文章
        EchoType, //推文類型
        Angel, //小天使廣告
        BoardArt, //進版畫面
    }

    /// <summary>
    /// 全域功能
    /// </summary>
    public static partial class LiPTT
    {
        public static PTTClient Client;

        /// <summary>
        /// XBOX ONE 手把我有 希望無窮(來鬧的)
        /// </summary>
        public static List<Gamepad> Gamepads;
        public static ThreadPoolTimer GamepadPollTimer;

        public static Frame Frame { get; set; }

        public static bool CacheBoard { get; set; } 

        //https://www.regexpal.com
        // '\w'會match到中文字，用[A-Za-z0-9_]替代
        public const string http_regex = @"(http|https)://([A-Za-z0-9_]+:??[A-Za-z0-9_]*@)?([A-Za-z0-9_#!:.?+=&%@!-/$^,;|*~'()]+)(/|/([A-Za-z0-9_#!:.?+=&%@!-/]))?";
        //private const string http_exp = @"((http|https)://([A-Za-z0-9_]+:{0,1}[A-Za-z0-9_]*@)?([A-Za-z0-9_#!:.?+=&%@!-/$^,;|*~'()]+)(:[0-9]+)?(/|/([A-Za-z0-9_#!:.?+=&%@!-/]))?)|(pid://(\d{1,10}))";
        //private const string http_exp = @"http(s)?://([\w]+\.)+[\w]+(/[\w-./?%&=]*)?";

        /// <summary>
        /// 判斷括號的正規表示式 // '[' '(' '［' '《' '<'
        /// </summary>
        public const string bracket_regex = @"[\u005b\u003c\uff3b\u300a]{1}[^\u005b\u003c\uff3b\u300a\u005d\u003e\uff3d\u300b]+[\u005d\u003e\uff3d\u300b]{1}";
        public const string bound_regex = @"[\u0028]{1}[^\u0028\u0029]+[\u0029]{1}";

        public static ScreenBuffer Screen
        {
            get
            {
                return Client.Screen;
            }
        }

        public static event EventHandler Connected
        {
            add
            {
                Client.Connected += value;
            }
            remove
            {
                Client.Connected -= value;
            }
        }

        public static event EventHandler Belled
        {
            add
            {
                Client.Belled += value;
            }
            remove
            {
                Client.Belled -= value;
            }
        }

        public static event PTTClient.ScreenEventHandler ScreenDrawn
        {
            add
            {
                Client.ScreenDrawn += value;
            }
            remove
            {
                Client.ScreenDrawn -= value;
            }
        }

        public static PttState State { get; set; }

        /// <summary>
        /// 使用者名稱 (通常至少4個字元，不過元老級的帳號是例外)
        /// </summary>
        public static string UserName { get; set; }

        /// <summary>
        /// 密碼 (通常至少4個字元，新建帳號new可以不需要)
        /// </summary>
        public static string Password { get; set; }

        /// <summary>
        /// WebView 暫存
        /// </summary>
        public static WebView CurrentWebView
        {
            get; set;
        }

        public static ImageCache ImageCache { get; set; }

        /// <summary>
        /// 全螢幕模式
        /// </summary>
        public static bool IsYoutubeFullScreen
        {
            get; set;
        }

        public static BoardContentCollection ArticleCollection
        {
            get; set;
        }

        public static Article CurrentArticle
        {
            get; set;
        }

        public static Bound Bound
        {
            get; private set;
        }

        public static bool Logined
        {
            get; set;
        }

        /// <summary>
        /// 在線人數
        /// </summary>
        public static int OnlineUsers { get; set; }

        /// <summary>
        /// 開始連PTT囉
        /// </summary>
        public static void TryConnect()
        {
            Client.Security = security;
            State = PttState.Connecting;
            Bound = new Bound();
            OnPttEventEchoed(State);
            Client.ConnectionFailed += HandleConnectionFailed;
            Client.Connected += AddEventHandler;
            Client.Connect();
        }

        private static void HandleConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            Client.Connected -= AddEventHandler;
            switch (e.ConnectionType)
            {
                case ConnectionType.WebSocket:
                    State = PttState.ConnectFailedWebSocket;
                    break;
                default:
                    State = PttState.ConnectFailedTCP;
                    break;
            }
            OnPttEventEchoed(State);
        }

        private static void AddEventHandler(object o, EventArgs e)
        {
            Client.ScreenUpdated += Current_ScreenUpdated;
            Client.Disconnected += Current_Disconnected;
            Client.Connected -= AddEventHandler;
            Client.Kicked += PTTKicked;
        }

        private static void PTTKicked(object sender, EventArgs e)
        {
            State = PttState.Kicked;
            Client.Kicked -= PTTKicked;
            Client.ScreenUpdated -= Current_ScreenUpdated;
            OnPttEventEchoed(State);
        }

        public static void RemoveHandlerStateChecker()
        {
            Client.ScreenUpdated -= Current_ScreenUpdated;
        }

        /// <summary>
        /// 判斷螢幕狀態
        /// </summary>
        /// <param name="regex">關鍵字</param>
        /// <param name="row">從0開始</param>
        private static bool Match(string regex, int row)
        {
            return new Regex(regex).Match(Screen.ToString(row)).Success;
        }

        private static Bound ReadBound(string msg)
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

        private static void Current_ScreenUpdated(object sender, ScreenEventArgs e)
        {
            if (Match(@"瀏覽 第", 23))
            {
                State = PttState.Article;

                Bound b = ReadBound(Screen.ToString(23));
                if (b.Begin != Bound.Begin)
                {
                    Bound = b;
                    Debug.WriteLine("瀏覽文章");
                    State = PttState.Article;
                    OnPttEventEchoed(State);
                }   
            }
            else if (Match(@"您確定要離開", 22))
            {
                if (State != PttState.Exit)
                {
                    Debug.WriteLine("您確定要離開?");
                    State = PttState.Exit;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match("主功能表", 0))
            {
                if (State != PttState.MainPage)
                {
                    string s = Client.Screen.ToString(23);
                    Match match = new Regex(@"(線上[\d\s]+人)").Match(s);
                    if (match.Success)
                    {
                        string st = s.Substring(match.Index + 2, match.Length - 3).Trim();
                        try { OnlineUsers = Convert.ToInt32(st); }
                        catch { }
                    }

                    Debug.WriteLine("主功能表");
                    State = PttState.MainPage;
                    OnPttEventEchoed(State);
                }
            }
            else if(Match(@"選擇看板    \(a\)增加看板", 23))
            {
                if (State != PttState.Favorite)
                {
                    Debug.WriteLine("我的最愛");
                    State = PttState.Favorite;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match(@"相關資訊一覽表", 2))
            {
                Debug.WriteLine("搜尋相關看板");
                State = PttState.RelatedBoard;
                OnPttEventEchoed(State);
            }
            else if (Match(@"請輸入看板名稱", 1))
            {
                Debug.WriteLine("請輸入看板名稱");
                State = PttState.SearchBoard;
                OnPttEventEchoed(State);
            }
            else if (Match(@"看板設定", 3))
            {
                if (State != PttState.BoardInfomation)
                {
                    Debug.WriteLine("看板資訊");
                    State = PttState.BoardInfomation;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match(@"文章選讀  \(y\)回應", 23))
            {
                Debug.WriteLine("看板");
                Bound = new Bound();
                State = PttState.Board;
                OnPttEventEchoed(State);
            }
            else if (Match(@"您想刪除其他重複登入的連線嗎", 22))
            {
                if (State != PttState.AlreadyLogin)
                {
                    State = PttState.AlreadyLogin;
                    OnPttEventEchoed(State);
                }
            }

            else if (Match(@"密碼不對或無此帳號", 21))
            {
                if (State != PttState.WrongPassword)
                {
                    State = PttState.WrongPassword;
                    Client.PTTWrongResponse = true;
                    OnPttEventEchoed(State);
                }

            }
            else if (Match(@"系統過載", 13))
            {
                if (State == PttState.OverLoading)
                {
                    Debug.WriteLine("系統過載");
                    State = PttState.OverLoading;
                    Client.PTTWrongResponse = true;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match(@"密碼正確", 21))
            {
                if (State != PttState.Accept)
                {
                    Debug.WriteLine("密碼正確");
                    State = PttState.Accept;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match(@"登入中", 22))
            {
                if (State != PttState.Loginning)
                {
                    Debug.WriteLine("登入中");
                    State = PttState.Loginning;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match(@"任意鍵", 23))
            {
                if (State != PttState.PressAny)
                {
                    Debug.WriteLine("請按任意鍵繼續");
                    Bound = new Bound();
                    State = PttState.PressAny;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match(@"登入太頻繁", 23))
            {
                if (State != PttState.LoginSoMany)
                {
                    Debug.WriteLine("登入太頻繁 請稍後在試");
                    State = PttState.LoginSoMany;
                    Client.PTTWrongResponse = true;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match(@"更新與同步", 22))
            {
                if (State != PttState.Synchronizing)
                {
                    Debug.WriteLine("更新與同步中...");
                    State = PttState.Synchronizing;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match(@"請輸入您的密碼", 21))
            {
                if (State != PttState.Password)
                {
                    Debug.WriteLine("請輸入您的密碼");
                    State = PttState.Password;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match(@"您要刪除以上錯誤嘗試的記錄嗎", 23))
            {
                if (State != PttState.WrongLog)
                {
                    Debug.WriteLine("您要刪除以上錯誤嘗試的記錄嗎");
                    State = PttState.WrongLog;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match("請輸入代號", 20))
            {
                Debug.WriteLine("請輸入代號");
                if (State != PttState.Login)
                {
                    State = PttState.Login;
                    OnPttEventEchoed(State);
                }
            }
            else if (Match("您覺得這篇文章", 23))
            {
                Bound = new Bound();
                State = PttState.EchoType;
                OnPttEventEchoed(State);
            }
            else
            {
#if DEBUG
                StringBuilder sb = new StringBuilder();
                sb.Append("這裡是哪裡? 進板畫面?\n");
                var x = Client.Screen.ToStringArray();
                foreach (string s in x)
                {
                    sb.AppendFormat("{0}\n", s);
                }
                Debug.WriteLine(sb.ToString());
#endif
                State = PttState.Angel;
            }
        }

        private static bool security;

        public static bool ConnectionSecurity
        {
            get
            {
                return security;
            }
            set
            {
                security = value;
            }
        }

        public static bool AlwaysAlive
        {
            get
            {
                SettingProperty Setting = Application.Current.Resources["SettingProperty"] as SettingProperty;
                return Setting.AlwaysAlive;
            }
        }

        public static bool IsExit
        {
            get
            {
                return Client.IsExit;
            }
            set
            {
                Client.IsExit = value;
            }
        }

        private static void Current_Disconnected(object sender, EventArgs e)
        {
            State = PttState.Disconnected;
            OnPttEventEchoed(State);
            Client.ScreenUpdated -= Current_ScreenUpdated;
            Client.Disconnected -= Current_Disconnected;
        }

        public static void TryDisconnect()
        {
            State = PttState.Disconnecting;
            OnPttEventEchoed(State);
            Client.Disconnect();
        }

        public static void PressUpdateEcho()
        {
            //意同：左 右 PageEnd
            Client.Send(new byte[] { 0x71, 0x72, 0x24 }); //qr$
        }

        public static void PressBackspace()
        {
            Client.Send(0x08);
        }

        public static void PressEnter()
        {
            Client.Send(0x0D); // Carriage return
        }

        public static void EnterUserName()
        {
            Client.Send(UserName, 0x0D);
        }

        public static void EnterPassword()
        {
            Client.Send(Password, 0x0D);
        }

        public static void Yes()
        {
            Client.Send('y', 0x0D);
        }

        public static void No()
        {
            Client.Send('n', 0x0D);
        }

        public static void PressSpace()
        {
            Client.Send(0x20); // ' '
        }

        public static void PressAnyKey()
        {
            Left();
        }

        public static void PressI()
        {
            Client.Send(0x69);
        }

        public static void PressKeepAlive()
        {
            Client.Send(0x0C); //^L
        }

        public static void Up()
        {
            Client.Send(new byte[] { 0x1B, 0x5B, 0x41 }); //ESC[A
        }

        public static void Down()
        {
            Client.Send(new byte[] { 0x1B, 0x5B, 0x42 }); //ESC[B
        }

        public static void Right()
        {
            //Current.Send(new byte[] { 0x1B, 0x5B, 0x43 }); //ESC[C
            Client.Send(0x72); //r
        }

        public static void Left()
        {
            //Current.Send(new byte[] { 0x1B, 0x5B, 0x44 }); //ESC[D
            Client.Send(0x71); //q
        }

        public static void PageDown()
        {
            Client.Send(new byte[] { 0x06 });
        }

        public static void PageUp()
        {
            Client.Send(new byte[] { 0x02 });
        }

        public static void PageHome()
        {
            Client.Send(new byte[] { 0x30, 0x72 }); //0r
        }

        public static void PageEnd()
        {
            Client.Send(new byte[] { 0x24 }); //'$'
        }

        public static void GoToFavorite()
        {
            Client.Send(new byte[] { 0x46, 0x1B, 0x5B, 0x43 });
        }

        public static void SendMessage(string s)
        {
            Client.Send(s);
        }

        public static void SendMessage(params object[] args)
        {
            Client.Send(args);
        }

        public static void SendMessage(byte[] message)
        {
            Client.Send(message);
        }

        public static void SendMessage(byte b)
        {
            Client.Send(b);
        }

        public static void SendMessage(char c)
        {
            Client.Send(c);
        }

        public static void Send(byte[] msg)
        {
            Client.Send(msg);
        }

        public static void CreateInstance()
        {
            if (ApplicationData.Current.RoamingSettings.Containers.ContainsKey(SettingPropertyName))
            {
                LoadSetting();
            }
            else
            {
                DefaultSetting();
                LoadSetting();
            }

            SettingProperty Setting = Application.Current.Resources["SettingProperty"] as SettingProperty;
            ApplicationProperty app = Application.Current.Resources["ApplicationProperty"] as ApplicationProperty;
            app.FullScreen = Setting.FullScreen;

            Client = new PTTClient { Security = security };

            Gamepads = new List<Gamepad>();
        }

        private static SemaphoreSlim exitSemaphore = new SemaphoreSlim(0, 1);
        private static Task ClearCacheTask;

        public static void ReleaseInstance()
        {
            SaveSetting();
            Gamepads.Clear();
            Debug.WriteLine("Clear Cache");
            ClearCacheTask = Task.Run(async () => { await ImageCache.ClearAllCache(); });

            if (Client.IsConnected)
            {
                PttEventEchoed = null;
                PttEventEchoed += ExitPttEventEchoed;

                if (State != PttState.MainPage)
                { 
                    Left();
                }
                else
                {
                    SendMessage('G', 0x0D);
                }
                State = PttState.Disconnected;
            }
            else
            {
                exitSemaphore.Release();
            }

            exitSemaphore.Wait();
            Debug.WriteLine("Exit");
        }

        private async static void ExitPttEventEchoed(PTTClient sender, LiPttEventArgs e)
        {
            if (State == PttState.Exit)
            {
                IsExit = true;
                SendMessage('y', 0x0D);
            }
            else if (!IsExit)
            {
                if (State == PttState.MainPage)
                    SendMessage('G', 0x0D);
                else
                    Left();
            }
            else if (State == PttState.PressAny)
            {
                PttEventEchoed -= ExitPttEventEchoed;
                Client.ScreenUpdated -= Current_ScreenUpdated;
                PressAnyKey();                
                Client.Disconnect();
                await ClearCacheTask;
                exitSemaphore.Release();
            }
        }

        public static ReadState GetReadSate(char state)
        {
            switch (state)
            {
                case '+':
                    return ReadState.無;
                case 'M':
                    return ReadState.被標記;
                case 'S':
                    return ReadState.待處理;
                case 'm':
                    return ReadState.已讀 | ReadState.被標記;
                case 's':
                    return ReadState.已讀 | ReadState.待處理;
                case '!':
                    return ReadState.被鎖定;
                case '~':
                    return ReadState.有推文;
                case '=':
                    return ReadState.有推文 | ReadState.被標記;
                case ' ':
                    return ReadState.已讀;
                default:
                    return ReadState.未定義;
            }
        }

        public static string GetBoardNick(string name)
        {
            switch (name)
            {
                case "Gossiping":
                    return "八卦";
                case "LoL":
                    return "LOL";
                case "C_Sharp":
                    return "C#";
                case "StupidClown":
                    return "笨板";
                case "GirlsFront":
                    return "少女前線";
                case "Windows":
                    return "Windows";
                case "Soft_Job":
                    return "軟體工作板";
                case "Beauty":
                    return "表特";
                case "PC_Shopping":
                    return "電蝦";
                default:
                    return "☐☐";
            }
        }

        public static string GetString(Block[] blocks)
        {
            byte[] mssage = new byte[blocks.Length];
            for (int j = 0; j < blocks.Length; j++) mssage[j] = blocks[j].Content;
            return PTTEncoding.GetEncoding().GetString(mssage);
        }

        public static string GetString(Block[] blocks, int index, int length)
        {
            if (index < 0 || index + length > blocks.Length) return "";
            byte[] mssage = new byte[length];
            for (int j = 0; j < length; j++) mssage[j] = blocks[index + j].Content;
            return PTTEncoding.GetEncoding().GetString(mssage);
        }


        public static Block[] Copy(Block[] blocks)
        {
            Block[] b = new Block[blocks.Length];

            for (int i = 0; i < blocks.Length; i++)
            {
                b[i] = new Block
                {
                    Content = blocks[i].Content,
                    Mode = blocks[i].Mode,
                    ForegroundColor = blocks[i].ForegroundColor,
                    BackgroundColor = blocks[i].BackgroundColor
                };
            }
            return b;
        }

        public delegate void LiPttEventHandler(PTTClient sender, LiPttEventArgs e);

        /// <summary>
        /// non-UI Thread介面
        /// </summary>
        public static event LiPttEventHandler PttEventEchoed;

        public static void ClearSubscriptions()
        {
            PttEventEchoed = null;
        }

        private static void OnPttEventEchoed(PttState state)
        {
            PttEventEchoed?.Invoke(Client, new LiPttEventArgs(state));
        }

        public static Windows.Foundation.IAsyncAction RunInUIThread(Windows.UI.Core.DispatchedHandler callback)
        {
            return CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, callback);
        }
    }

    public class LiPttEventArgs : EventArgs
    {
        public LiPttEventArgs(PttState state)
        {
            State = state;
        }

        public PttState State
        {
            get; set;
        }
    }

    public class ApplicationProperty : INotifyPropertyChanged
    {
        public bool FullScreen
        {
            get
            {
                return fullScreen;
            }
            set
            {
                if (fullScreen == value) return;

                fullScreen = value;
                var View = ApplicationView.GetForCurrentView();
                if (fullScreen)
                    View.TryEnterFullScreenMode();
                else
                    View.ExitFullScreenMode();
                NotifyPropertyChanged("FullScreen");
            }
        }

        private bool fullScreen;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
