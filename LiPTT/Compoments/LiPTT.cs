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
        /// <summary>
        /// 無
        /// </summary>
        None,
        /// <summary>
        /// 未連線
        /// </summary>
        Disconnected,
        /// <summary>
        /// 連線中
        /// </summary>
        Connecting,
        /// <summary>
        /// TCP連線失敗
        /// </summary>
        ConnectFailedTCP,
        /// <summary>
        /// WebSocket連線失敗
        /// </summary>
        ConnectFailedWebSocket,
        /// <summary>
        /// 斷線中
        /// </summary>
        Disconnecting,
        /// <summary>
        /// 被踢下線
        /// </summary>
        Kicked,
        /// <summary>
        /// PTT系統過載
        /// </summary>
        OverLoading,
        /// <summary>
        /// 登入畫面
        /// </summary>
        Login,
        /// <summary>
        /// 請輸入密碼
        /// </summary>
        Password,
        /// <summary>
        /// 密碼錯誤或無此帳號
        /// </summary>
        WrongPassword,
        /// <summary>
        /// 密碼正確
        /// </summary>
        Accept,
        /// <summary>
        /// 登入中
        /// </summary>
        Loginning,
        /// <summary>
        /// 同步處理中
        /// </summary>
        Synchronizing,
        /// <summary>
        /// 登入太頻繁
        /// </summary>
        LoginSoMany,
        /// <summary>
        /// 重複登入
        /// </summary>
        AlreadyLogin,
        /// <summary>
        /// 主功能表
        /// </summary>
        MainPage,
        /// <summary>
        /// 熱門看板列表
        /// </summary>
        Popular,
        /// <summary>
        /// 按任意鍵繼續
        /// </summary>
        PressAny,
        /// <summary>
        /// 登入警告資訊
        /// </summary>
        WrongLog,
        /// <summary>
        /// 離開嗎?
        /// </summary>
        Exit,
        /// <summary>
        /// 最愛看板列表
        /// </summary>
        Favorite,
        /// <summary>
        /// s搜尋看板
        /// </summary>
        SearchBoard,
        /// <summary>
        /// 相關資訊一覽表
        /// </summary>
        RelatedBoard,
        /// <summary>
        /// 文章列表
        /// </summary>
        Board, //文章列表
        /// <summary>
        /// 看板資訊
        /// </summary>
        BoardInfomation,
        /// <summary>
        /// 閱覽文章
        /// </summary>
        Article,
        /// <summary>
        /// AID文章代碼
        /// </summary>
        AID,
        EchoType, //推文類型
        Angel, //小天使廣告
        BoardArt, //進版畫面
    }

    /// <summary>
    /// 全域功能
    /// </summary>
    public static partial class LiPTT
    {
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
        
        public static bool AlwaysAlive
        {
            get
            {
                SettingProperty Setting = Application.Current.Resources["SettingProperty"] as SettingProperty;
                return Setting.AlwaysAlive;
            }
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

            Gamepads = new List<Gamepad>();
        }

        private static SemaphoreSlim exitSemaphore = new SemaphoreSlim(0, 1);
        private static Task ClearCacheTask;

        public static async void ReleaseInstance()
        {
            SaveSetting();
            Gamepads.Clear();
            Debug.WriteLine("Clear Cache");
            ClearCacheTask = Task.Run(async () => { await ImageCache.ClearAllCache(); });

            PTT ptt = Application.Current.Resources["PTT"] as PTT;

            Task exit = ptt.ExitPTT();

            await exit;
            await ClearCacheTask;

            Debug.WriteLine("Exit");
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

        public static Windows.Foundation.IAsyncAction RunInUIThread(Windows.UI.Core.DispatchedHandler callback)
        {
            return CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, callback);
        }
    }

    public class LiPttEventArgs : EventArgs
    {
        public PttState State
        {
            get; set;
        }

        public object Others
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
