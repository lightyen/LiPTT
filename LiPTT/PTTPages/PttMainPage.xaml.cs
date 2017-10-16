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

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class PttMainPage : Page
    {
        PTT ptt;

        public PttMainPage()
        {
            InitializeComponent();

            PTT ptt = Application.Current.Resources["PTT"] as PTT;

            LiPTT.Frame = PTTFrame;
            ptt.Kicked += async (o, e) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    PTTFrame.Navigate(typeof(LoginPage));
                });   
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            if (!ptt.IsConnected && PTTFrame.CurrentSourcePageType != typeof(LoginPage))
            {
                PTTFrame.Navigate(typeof(LoginPage));
            }
        }
    }
}
