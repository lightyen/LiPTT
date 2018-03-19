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

        public static void ReleaseInstance()
        {
            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            Task exit = Task.Run(() => { ptt.ClearPTTStateUpdatedSubscription(); ptt.Exit(); });
            SaveSetting();
            Gamepads.Clear();
            Debug.WriteLine("Clear Cache");
            Task ClearCacheTask = Task.Run(async () => { await ImageCache.ClearAllCache(); });

            exit.Wait();
            ClearCacheTask.Wait();
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
