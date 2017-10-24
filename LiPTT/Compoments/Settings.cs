using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.Storage;
using Newtonsoft.Json;

namespace LiPTT
{
    public class SettingProperty : INotifyPropertyChanged
    {
        public SettingProperty()
        {
            ConnectionSecurity = true;
            AlwaysAlive = false;
            LineSpaceDisabled = false;
            OpenShortUri = false;
            googleURLShortenerAPIKey = "AIzaSyCEcRFJD94zXZeab1yDSZ__SLBISmpPm6Y";
            Space = 0.5;
            FontSizePercent = 0.5;
            BoardTitleColor = Colors.White;
            color = Colors.White;
        }

        /// <summary>
        /// 圖片大小，預設70%
        /// </summary>
        public double Space
        {
            get
            {
                return space;
            }
            set
            {
                space = value;
                NotifyPropertyChanged("Space");
            }
        }

        /// <summary>
        /// 是否安全連線
        /// </summary>
        public bool? ConnectionSecurity
        {
            get
            {
                PTT ptt = Application.Current.Resources["PTT"] as PTT;
                return ptt.ConnectionSecurity;
            }
            set
            {
                if (value is bool b)
                {
                    PTT ptt = Application.Current.Resources["PTT"] as PTT;
                    ptt.ConnectionSecurity = b;
                    NotifyPropertyChanged("ConnectionSecurity");
                }
            }
        }

        /// <summary>
        /// 總是自動重新連線
        /// </summary>
        public bool AlwaysAlive
        {
            get
            {
                return alwaysAlive;
            }
            set
            {
                alwaysAlive = value;
                NotifyPropertyChanged("AlwaysAlive");
            }
        }

        /// <summary>
        /// 全螢幕選項
        /// </summary>
        public bool FullScreen
        {
            get
            {
                return fullScreen;
            }
            set
            {
                var app = Application.Current.Resources["ApplicationProperty"] as ApplicationProperty;
                fullScreen = value;
                app.FullScreen = fullScreen;
                NotifyPropertyChanged("FullScreen");
            }
        }

        /// <summary>
        /// 文字大小
        /// </summary>
        public double FontSizePercent
        {
            get
            {
                return fontSize;
            }
            set
            {
                fontSize = value;
                NotifyPropertyChanged("FontSizePercent");
            }
        }

        /// <summary>
        /// 行與行之間不要留空白
        /// </summary>
        public bool LineSpaceDisabled
        {
            get
            {
                return disabledlinespace;
            }
            set
            {
                disabledlinespace = value;
                NotifyPropertyChanged("LineSpaceDisabled");
            }
        }

        public bool OpenShortUri
        {
            get
            {
                return openshort;
            }
            set
            {
                openshort = value;
                NotifyPropertyChanged("OpenShortUri");
            }
        }

        public string GoogleURLShortenerAPIKey
        {
            get
            {
                return googleURLShortenerAPIKey;
            }
            set
            {
                googleURLShortenerAPIKey = value;
                NotifyPropertyChanged("GoogleURLShortenerAPIKey");
            }
        }

        public bool AutoLoad
        {
            get
            {
                return autoload;
            }
            set
            {
                autoload = value;
                NotifyPropertyChanged("AutoLoad");
            }
        }

        public Color TextColor
        {
            get
            {
                return color;
            }
            set
            {
                color = value;
                NotifyPropertyChanged("TextColor");
            }
        }

        public Color BoardTitleColor
        {
            get
            {
                return boardTitleColor;
            }
            set
            {
                boardTitleColor = value;
                NotifyPropertyChanged("BoardTitleColor");
            }
        }

        private bool alwaysAlive;
        private double space;
        private bool fullScreen;
        private double fontSize;
        private bool disabledlinespace;
        private bool openshort;
        private string googleURLShortenerAPIKey;
        private bool autoload;
        private Color color;
        private Color boardTitleColor;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public static partial class LiPTT
    {
        private static void DefaultSetting()
        {
            var container = ApplicationData.Current.RoamingSettings.CreateContainer(SettingPropertyName, ApplicationDataCreateDisposition.Always);

            if (container != null)
            {
                SettingProperty Setting = Application.Current.Resources["SettingProperty"] as SettingProperty;
                container.Values["Setting"] = JsonConvert.SerializeObject(Setting);
            }
        }

        private static void LoadSetting()
        {
            var container = ApplicationData.Current.RoamingSettings.Containers[SettingPropertyName].Values;

            if (container != null && container["Setting"] is string json)
            {
                SettingProperty Setting = JsonConvert.DeserializeObject<SettingProperty>(json);
                Application.Current.Resources["SettingProperty"] = Setting;
            }
        }

        private static void SaveSetting()
        {
            var container = ApplicationData.Current.RoamingSettings.CreateContainer(SettingPropertyName, ApplicationDataCreateDisposition.Always);

            if (container != null)
            {
                SettingProperty Setting = Application.Current.Resources["SettingProperty"] as SettingProperty;
                container.Values["Setting"] = JsonConvert.SerializeObject(Setting);
            }
        }

        private const string SettingPropertyName = "SettingProperty";

    }
}
