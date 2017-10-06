using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.Storage;
using System.Runtime.Serialization;
using System.Reflection;

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class ColorPage : Page
    {
        private const string ColorTableKey = "ColorTable";

        public ColorPage()
        {
            InitializeComponent();

            LoadColorTable();

            foreach (var b in FindVisualChildren<Button>(ColorTable))
            {
                SolidColorBrush br = b.Background as SolidColorBrush;
                b.Foreground = new SolidColorBrush(Inverse(br.Color));

                b.Click += (o, e) =>
                {
                    SolidColorBrush brush = b.Background as SolidColorBrush;
                    b.Foreground = new SolidColorBrush(Inverse(brush.Color));
                    System.Diagnostics.Debug.WriteLine((b.Background as SolidColorBrush).Color.ToString());
                };
            }

            /***
            var colors = typeof(Windows.UI.Colors).GetTypeInfo().DeclaredProperties;

            foreach (var s in colors)
            {
                System.Diagnostics.Debug.WriteLine(s.ToString());
            }
            /****/
        }

        private Windows.UI.Color Inverse(Windows.UI.Color color)
        {
            HSVColor hsv = ColorHelper.RGBtoHSV(color);
            if (hsv.S < 0.3) hsv.V = 1.0 - hsv.V * hsv.V;
            hsv.H = (hsv.H + 180.0) % 360.0;
            return ColorHelper.HSVtoRGB(hsv);
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void LoadColorTable()
        {
            if (ApplicationData.Current.RoamingSettings.Containers.ContainsKey(ColorTableKey))
            {
                LoadColor();
            }
            else
            {
                DefaultColorTable();
                LoadColor();
            }
        }

        private void LoadColor()
        {
            var container = ApplicationData.Current.RoamingSettings.Containers[ColorTableKey].Values;

            if (container != null)
            {   
                ButtonFore30.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[30m"] as string));
                ButtonFore31.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[31m"] as string));
                ButtonFore32.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[32m"] as string));
                ButtonFore33.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[33m"] as string));
                ButtonFore34.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[34m"] as string));
                ButtonFore35.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[35m"] as string));
                ButtonFore36.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[36m"] as string));
                ButtonFore37.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[37m"] as string));
                ///////////////////////////////////
                ButtonFore130.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[1;30m"] as string));
                ButtonFore131.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[1;31m"] as string));
                ButtonFore132.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[1;32m"] as string));
                ButtonFore133.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[1;33m"] as string));
                ButtonFore134.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[1;34m"] as string));
                ButtonFore135.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[1;35m"] as string));
                ButtonFore136.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[1;36m"] as string));
                ButtonFore137.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[1;37m"] as string));
                ///////////////////////////////////
                ButtonFore40.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[40m"] as string));
                ButtonFore41.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[41m"] as string));
                ButtonFore42.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[42m"] as string));
                ButtonFore43.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[43m"] as string));
                ButtonFore44.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[44m"] as string));
                ButtonFore45.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[45m"] as string));
                ButtonFore46.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[46m"] as string));
                ButtonFore47.Background = new SolidColorBrush(Newtonsoft.Json.JsonConvert.DeserializeObject<Windows.UI.Color>(container["[47m"] as string));
            }
        }

        private void DefaultColorTable()
        {
            var container = ApplicationData.Current.RoamingSettings.CreateContainer(ColorTableKey, ApplicationDataCreateDisposition.Always);

            if (container != null)
            {  
                container.Values["[30m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
                container.Values["[31m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x80, 0x00, 0x00));
                container.Values["[32m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x80, 0x00));
                container.Values["[33m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x80, 0x80, 0x00));
                container.Values["[34m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x00, 0x80));
                container.Values["[35m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x80, 0x00, 0x80));
                container.Values["[36m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x80, 0x80));
                container.Values["[37m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0));
                /////////////////////////
                container.Values["[1;30m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x80, 0x80, 0x80));
                container.Values["[1;31m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x00, 0x00));
                container.Values["[1;32m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0xFF, 0x00));
                container.Values["[1;33m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00));
                container.Values["[1;34m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x00, 0xFF));
                container.Values["[1;35m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x00, 0xFF));
                container.Values["[1;36m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0xFF, 0xFF));
                container.Values["[1;37m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
                /////////////////////////
                container.Values["[40m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
                container.Values["[41m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x80, 0x00, 0x00));
                container.Values["[42m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x80, 0x00));
                container.Values["[43m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x80, 0x80, 0x00));
                container.Values["[44m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x00, 0x80));
                container.Values["[45m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x80, 0x00, 0x80));
                container.Values["[46m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x80, 0x80));
                container.Values["[47m"] = Newtonsoft.Json.JsonConvert.SerializeObject(Windows.UI.Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0));
            }
        }
    }
}
