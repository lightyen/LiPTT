using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.ApplicationModel.Core;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace LiPTT
{
    public enum PttState
    {
        None,
        Disconnected, //未連線
        Disconnecting, //斷線中
        Kicked, //被踢惹
        OverLoading, //PTT爆炸惹
        Login, // PTT歡迎畫面(輸入帳號名稱：)
        Password, //輸入密碼
        WrongPassword, //密碼錯誤
        Accept, //密碼正確
        Loginning, //登入中
        Synchronizing, //同步處理中
        LoginSoMany, //登入太頻繁
        AlreadyLogin, //重複登入?
        MainPage, //(全站分類)
        Popular, //熱門看板列表
        PressAny, //按任意鍵喔
        WrongLog, //登入錯誤資訊
        Exit, //離開嗎?
        ByeBye, //掰掰囉
        Favor, //最愛看板列表
        BoardArt, //進版畫面
        RelatedBoard, //相關資訊一覽表
        SearchBoard, //搜尋看板
        Board, //文章列表
        Article, //閱覽文章
        Angel, //小天使廣告
    }

    /// <summary>
    /// 全域功能
    /// </summary>
    public static class LiPTT
    {
        private static PTTProvider pTTProvider;

        public static DispatcherTimer TestConnectionTimer { get; set; }

        public static Frame Frame { get; set; }

        //https://www.regexpal.com
        // '\w'會match到中文字，用[A-Za-z0-9_]替代
        public const string http_regex = @"(http|https)://(A-Za-z0-9_+:??A-Za-z0-9_*@)?([A-Za-z0-9_#!:.?+=&%@!-/$^,;|*~'()]+)(/|/([A-Za-z0-9_#!:.?+=&%@!-/]))?";
        //private const string http_exp = @"((http|https)://([A-Za-z0-9_]+:{0,1}[A-Za-z0-9_]*@)?([A-Za-z0-9_#!:.?+=&%@!-/$^,;|*~'()]+)(:[0-9]+)?(/|/([A-Za-z0-9_#!:.?+=&%@!-/]))?)|(pid://(\d{1,10}))";
        //private const string http_exp = @"http(s)?://([\w]+\.)+[\w]+(/[\w-./?%&=]*)?";

        /// <summary>
        /// 當前連線物件
        /// </summary>
        public static PTTProvider Current
        {
            get
            {
                return pTTProvider;
            }
        }

        /// <summary>
        /// 是否連線
        /// </summary>
        public static bool IsConnected => Current.IsConnected;

        public static PttState State;

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

        public static ArticleCollection ArticleCollection
        {
            get; set;
        }

        public static Article CurrentArticle
        {
            get; set;
        }

        /// <summary>
        /// 開始連PTT囉
        /// </summary>
        public static void TryConnect()
        {
            if (IsConnected) Current.Disconnect();
            Current.Connected += AddEventHandler;
            Current.Connect();
        }

        private static void AddEventHandler(object o, EventArgs e)
        {
            Current.ScreenUpdated += Current_ScreenUpdated;
            Current.Disconnected += Current_Disconnected;
            Current.Connected -= AddEventHandler;
            Current.Kicked += PTTKicked;
        }

        private static void PTTKicked(object sender, EventArgs e)
        {
            State = PttState.Kicked;
            Current.Kicked -= PTTKicked;
            Current.ScreenUpdated -= Current_ScreenUpdated;
            CurrentArticle = null;
            OnPttEventEchoed(State, pTTProvider.Screen);
        }

        private static void Current_ScreenUpdated(object sender, ScreenEventArgs e)
        {
            Current.ScreenLocker.Wait();

            if (Current.MatchPattern(@"瀏覽", 23))
            {
                Debug.WriteLine("瀏覽文章");
                State = PttState.Article;
                OnPttEventEchoed(State, pTTProvider.Screen);
            }
            else if (Current.MatchPattern(@"您確定要離開", 22))
            {
                if (State != PttState.Exit)
                {
                    State = PttState.Exit;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else if (Current.MatchPattern(@"主功能表", 0))
            {
                if (State != PttState.MainPage)
                {
                    Debug.WriteLine("主功能表");
                    State = PttState.MainPage;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else if (Current.MatchPattern(@"相關資訊一覽表", 2))
            {
                Debug.WriteLine("搜尋相關看板");
                State = PttState.RelatedBoard;
                OnPttEventEchoed(State, pTTProvider.Screen);
            }
            else if (Current.MatchPattern(@"選擇看板", 0))
            {
                Debug.WriteLine("搜尋看板");
                State = PttState.SearchBoard;
                OnPttEventEchoed(State, pTTProvider.Screen);
            }
            else if (Current.MatchPattern(@"【板主", 0))
            {
                Debug.WriteLine("看板");
                State = PttState.Board;
                OnPttEventEchoed(State, pTTProvider.Screen);
            }
            else if (Current.MatchPattern(@"您想刪除其他重複登入的連線嗎", 22))
            {
                if (State != PttState.AlreadyLogin)
                {
                    State = PttState.AlreadyLogin;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }

            else if (Current.MatchPattern(@"密碼不對或無此帳號", 21))
            {
                if (State != PttState.WrongPassword)
                {
                    State = PttState.WrongPassword;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }

            }
            else if (Current.MatchPattern(@"系統過載", 13))
            {
                if (State == PttState.OverLoading)
                {
                    Debug.WriteLine("系統過載");
                    State = PttState.OverLoading;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else if (Current.MatchPattern(@"密碼正確", 21))
            {
                if (State != PttState.Accept)
                {
                    Debug.WriteLine("密碼正確");
                    State = PttState.Accept;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else if (Current.MatchPattern(@"登入中", 22))
            {
                if (State != PttState.Loginning)
                {
                    Debug.WriteLine("登入中");
                    State = PttState.Loginning;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else if (Current.MatchPattern(@"任意鍵繼續", 23))
            {
                if (State != PttState.PressAny)
                {
                    Debug.WriteLine("請按任意鍵繼續");
                    State = PttState.PressAny;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else if (Current.MatchPattern(@"登入太頻繁", 23))
            {
                if (State != PttState.LoginSoMany)
                {
                    Debug.WriteLine("登入太頻繁 請稍後在試");
                    State = PttState.LoginSoMany;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else if (Current.MatchPattern(@"更新與同步", 22))
            {
                if (State != PttState.Synchronizing)
                {
                    Debug.WriteLine("更新與同步中...");
                    State = PttState.Synchronizing;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else if (Current.MatchPattern(@"請輸入您的密碼", 21))
            {
                if (State != PttState.Password)
                {
                    Debug.WriteLine("請輸入您的密碼");
                    State = PttState.Password;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else if (Current.MatchPattern(@"您要刪除以上錯誤嘗試的記錄嗎", 23))
            {
                if (State != PttState.WrongLog)
                {
                    Debug.WriteLine("您要刪除以上錯誤嘗試的記錄嗎");
                    State = PttState.WrongLog;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else if (Current.MatchPattern(@"請輸入代號", 20))
            {
                Debug.WriteLine("請輸入代號");
                if (State != PttState.Login)
                {
                    State = PttState.Login;
                    OnPttEventEchoed(State, pTTProvider.Screen);
                }
            }
            else
            {
                Debug.WriteLine("這裡是哪裡?");

                foreach (string s in Current.Screen.ToStringArray())
                {
                    Debug.WriteLine(s);
                }

                State = PttState.Angel;
            }

            Current.ScreenLocker.Release();
        }

        private static void Current_Disconnected(object sender, EventArgs e)
        {
            State = PttState.Disconnected;
            OnPttEventEchoed(State, pTTProvider.Screen);
            Current.ScreenUpdated -= Current_ScreenUpdated;
            Current.Disconnected -= Current_Disconnected;
        }

        public static void TryDisconnect()
        {
            State = PttState.Disconnecting;
            OnPttEventEchoed(State, pTTProvider.Screen);
            Current.Disconnect();
        }

        public static void PressUpdateEcho()
        {
            //意同：左 右 PageEnd
            Current.Send(new byte[] { 0x71, 0x72, 0x24 }); //qr$
        }

        public static void PressBackspace()
        {
            Current.Send(0x08);
        }

        public static void PressEnter()
        {
            Current.Send(0x0D); // Carriage return
        }

        public static void EnterUserName()
        {
            Current.Send(UserName, 0x0D);
        }

        public static void EnterPassword()
        {
            Current.Send(Password, 0x0D);
        }

        public static void Yes()
        {
            Current.Send('y', 0x0D);
        }

        public static void No()
        {
            Current.Send('n', 0x0D);
        }

        public static void PressSpace()
        {
            Current.Send(0x20); // ' '
        }

        public static void PressAnyKey()
        {
            PressSpace();
        }

        public static void Up()
        {
            Current.Send(new byte[] { 0x1B, 0x5B, 0x41 }); //ESC[A
        }

        public static void Down()
        {
            Current.Send(new byte[] { 0x1B, 0x5B, 0x42 }); //ESC[B
        }

        public static void Right()
        {
            Current.Send(new byte[] { 0x1B, 0x5B, 0x43 }); //ESC[C
        }

        public static void Left()
        {
            Current.Send(new byte[] { 0x1B, 0x5B, 0x44 }); //ESC[D
        }

        public static void PageDown()
        {
            Current.Send(new byte[] { 0x06 });
        }

        public static void PageUp()
        {
            Current.Send(new byte[] { 0x02 });
        }

        public static void PageHome()
        {
            Current.Send(new byte[] { 0x30, 0x72 }); //0r
        }

        public static void PageEnd()
        {
            Current.Send(new byte[] { 0x24 }); //'$'
        }

        public static void SendMessage(string s)
        {
            Current.Send(s);
        }

        public static void SendMessage(params object[] args)
        {
            Current.Send(args);
        }

        public static void SendMessage(byte[] message)
        {
            Current.Send(message);
        }

        public static void SendMessage(byte b)
        {
            Current.Send(b);
        }

        public static void SendMessage(char c)
        {
            Current.Send(c);
        }

        private static void WaitEcho()
        {
            Current.ScreenSemaphore.Wait();
        }

        public static void CreateInstance()
        {
            pTTProvider = new PTTProvider();
        }

        public static void ReleaseInstance()
        {
            pTTProvider.IsExit = true;
            pTTProvider.Dispose();
            var task = Task.Run(async () => { await ImageCache.ClearAllCache(); });
            task.Wait();
        }

        public static ReadType GetReadType(char readtype)
        {
            switch (readtype)
            {
                case '+':
                    return ReadType.None;
                case 'M':
                    return ReadType.被標記;
                case 'S':
                    return ReadType.待處理;
                case 'm':
                    return ReadType.已讀 | ReadType.被標記;
                case 's':
                    return ReadType.已讀 | ReadType.待處理;
                case '!':
                    return ReadType.被鎖定;
                case '~':
                    return ReadType.有推文;
                case '=':
                    return ReadType.有推文 | ReadType.被標記;
                case ' ':
                    return ReadType.已讀;
                default:
                    return ReadType.Undefined;
            }
        }

        public static string GetString(Block[] blocks)
        {
            byte[] mssage = new byte[blocks.Length];
            for (int j = 0; j < blocks.Length; j++) mssage[j] = blocks[j].Content;
            return LiPTT_Encoding.GetEncoding().GetString(mssage);
        }

        public static string GetString(Block[] blocks, int index, int length)
        {
            if (index < 0 || index + length > blocks.Length) return "";
            byte[] mssage = new byte[length];
            for (int j = 0; j < length; j++) mssage[j] = blocks[index + j].Content;
            return LiPTT_Encoding.GetEncoding().GetString(mssage);
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

        public delegate void LiPttEventHandler(PTTProvider sender, LiPttEventArgs e);

        /// <summary>
        /// non-UI Thread介面
        /// </summary>
        public static event LiPttEventHandler PttEventEchoed;

        public static void ClearSubscriptions()
        {
            PttEventEchoed = null;
        }

        private static void OnPttEventEchoed(PttState state, ScreenBuffer screen)
        {
            PttEventEchoed?.Invoke(pTTProvider, new LiPttEventArgs(state, screen));
        }

        public static Windows.Foundation.IAsyncAction RunInUIThread(Windows.UI.Core.DispatchedHandler callback)
        {
            return CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, callback);
        }
    }

    public class LiPttEventArgs : EventArgs
    {
        public LiPttEventArgs(PttState state, ScreenBuffer screen)
        {
            State = state;
            Screen = screen;
        }

        public PttState State
        {
            get; set;
        }

        public ScreenBuffer Screen
        {
            get; set;
        }
    }
}
