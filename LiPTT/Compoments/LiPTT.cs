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

    /// <summary>
    /// 全域功能
    /// </summary>
    public static partial class LiPTT
    {
        //https://www.regexpal.com
        // '\w'會match到中文字，用[A-Za-z0-9_]替代
        public const string http_regex = @"(http|https)://([A-Za-z0-9_]+:??[A-Za-z0-9_]*@)?([A-Za-z0-9_#!:.?+=&%@!-/$^,;|*~'()]+)(/|/([A-Za-z0-9_#!:.?+=&%@!-/]))?";
        //private const string http_exp = @"((http|https)://([A-Za-z0-9_]+:{0,1}[A-Za-z0-9_]*@)?([A-Za-z0-9_#!:.?+=&%@!-/$^,;|*~'()]+)(:[0-9]+)?(/|/([A-Za-z0-9_#!:.?+=&%@!-/]))?)|(pid://(\d{1,10}))";
        //private const string http_exp = @"http(s)?://([\w]+\.)+[\w]+(/[\w-./?%&=]*)?";
        public const string ValidIpAddressRegex = @"(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])";

        /// <summary>
        /// 判斷括號的正規表示式 // '[' '(' '［' '《' '<'
        /// </summary>
        public const string bracket_regex = @"[\u005b\u003c\uff3b\u300a]{1}[^\u005b\u003c\uff3b\u300a\u005d\u003e\uff3d\u300b]+[\u005d\u003e\uff3d\u300b]{1}";
        public const string bound_regex = @"[\u0028]{1}[^\u0028\u0029]+[\u0029]{1}";

        public static ImageCache ImageCache { get; set; }

        public static Frame Frame { get; set; }

        public static bool CacheBoard { get; set; }

        public static BoardContentCollection ArticleCollection
        {
            get; set;
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

        /// <summary>
        /// XBOX ONE 手把我有 希望無窮(來鬧的)
        /// </summary>
        public static List<Gamepad> Gamepads;
        public static ThreadPoolTimer GamepadPollTimer;

        /// <summary>
        /// WebView 暫存
        /// </summary>
        public static WebView CurrentWebView
        {
            get; set;
        }

        /// <summary>
        /// 全螢幕模式
        /// </summary>
        public static bool IsYoutubeFullScreen
        {
            get; set;
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
