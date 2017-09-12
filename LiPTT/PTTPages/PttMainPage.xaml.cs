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
using Windows.System;


// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=234238

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class PttMainPage : Page
    {
        public PttMainPage()
        {
            this.InitializeComponent();

            LiPTT.Frame = PTTFrame;

            LiPTT.TestConnectionTimer = new DispatcherTimer();
            LiPTT.TestConnectionTimer.Tick += Timer_Tick;
            LiPTT.TestConnectionTimer.Interval = TimeSpan.FromSeconds(1);
        }


        private void Timer_Tick(object sender, object e)
        {
            if (!LiPTT.IsConnected)
            {
                LiPTT.TestConnectionTimer.Stop();

                PTTFrame.Navigate(typeof(LoginPage));
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (PTTFrame.CurrentSourcePageType != typeof(LoginPage))
            {
                if (!LiPTT.IsConnected)
                    PTTFrame.Navigate(typeof(LoginPage));
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            LiPTT.TestConnectionTimer.Stop();
        }
    }
}
