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

using System.Diagnostics;
using Windows.UI.Core;
using Windows.System;

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class BBSPage : Page
    {
        public BBSPage()
        {
            InitializeComponent();
            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            List<string> fontNames = SharpDX.DirectXFactory.GetInstalledFontNames();
            foreach (var s in fontNames) FontsComboBox.Items.Add(s);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //測試用
            //Window.Current.CoreWindow.KeyDown += PanelKeyDown;

        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            myPanel.Dispose();
        }

        private void PanelKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            //args.VirtualKey == VirtualKey
        }

        

        private void FontsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            myPanel.PreferFont = FontsComboBox.SelectedValue as string;
            myPanel.DrawPTT();
        }

        private bool IsDigit(int v)
        {
            return v >= 0x30 && v <= 0x39;
        }

        private bool IsUpperCase(int v)
        {
            return v >= 0x41 && v <= 0x5A;
        }
    }
}
