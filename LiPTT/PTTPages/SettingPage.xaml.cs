using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Reflection;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=234238

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class SettingPage : Page
    {
        public SettingPage()
        {
            InitializeComponent();
        }

        string targetColor;

        private void TextColor_Click(object sender, RoutedEventArgs e)
        {
            targetColor = "TextColor";
        }

        private void BoardTitleColor_Click(object sender, RoutedEventArgs e)
        {
            targetColor = "BoardTitleColor";
        }

        private void ColorPicker_Open(object sender, object e)
        {
            SettingProperty Setting = Application.Current.Resources["SettingProperty"] as SettingProperty;

            Type t = Setting.GetType();
            object value = t.InvokeMember(targetColor,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty,
                null, Setting, null);
            myColorPicker.Color = (Color)value;
        }

        private void ColorPicker_Change(object sender, RoutedEventArgs e)
        {
            SettingProperty Setting = Application.Current.Resources["SettingProperty"] as SettingProperty;
            Type t = Setting.GetType();
            t.InvokeMember(targetColor,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty,
                null, Setting, new object[] { myColorPicker.Color });
            ColorPickerFlyout.Hide();
        }

        private void ColorPicker_Cancel(object sender, RoutedEventArgs e)
        {
            ColorPickerFlyout.Hide();
        }
    }
}
