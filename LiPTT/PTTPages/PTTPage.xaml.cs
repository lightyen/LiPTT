using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.System;
using Windows.System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using Windows.Gaming.Input;

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class PTTPage : Page
    {
        public PTTPage()
        {
            InitializeComponent();
            //LiPTT.GamepadPollTimer = ThreadPoolTimer.CreatePeriodicTimer(GamepadUpdate, TimeSpan.FromMilliseconds(50));
        }

        private static void GamepadUpdate(ThreadPoolTimer timer)
        {
            if (LiPTT.Gamepads.Count > 0)
            {
                var re = LiPTT.Gamepads[0].GetCurrentReading();

                if (re.Buttons.HasFlag(GamepadButtons.A))
                {
                    Debug.WriteLine("A");
                }
            }
        }


        GamepadVibration vibration = new GamepadVibration { LeftMotor = 0.7, RightMotor = 0.7 };

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            LiPTT.IsExit = false;
            MainFuncFrame.Navigate(typeof(MainFunctionPage));
            LiPTT.PttEventEchoed += Updated;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            LiPTT.PttEventEchoed -= Updated;
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Escape)
                Exit();
        }

        private async void Updated(PTTClient sender, LiPttEventArgs e)
        {

            switch(e.State)
            {
                case PttState.MainPage:
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                        if (pivot_index != pivot.SelectedIndex) MainFuncFrame.Navigate(typeof(MainFunctionPage));
                    });
                    break;
                case PttState.Favorite:
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                        if (pivot_index != pivot.SelectedIndex) FavoriteFrame.Navigate(typeof(FavoritePage));
                    });
                    break;
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                pivot_index = pivot.SelectedIndex;
            });
        }

        private void Exit()
        {
            LiPTT.PttEventEchoed -= Updated;

            if (LiPTT.IsExit == false)
            {
                LiPTT.IsExit = true;
                LiPTT.KeepAliveTimer.Stop();
                LiPTT.PttEventEchoed += Exit_echoed;

                if (pivot.SelectedIndex == 1)
                {
                    LiPTT.SendMessage(0x71, 0x47, 0x0D);
                }
                else
                {
                    LiPTT.SendMessage(0x47, 0x0D);
                }
            }
        }

        private void Exit_echoed(PTTClient sender, LiPttEventArgs e)
        {
            if (e.State == PttState.Exit)
            {
                LiPTT.SendMessage('y', 0x0D); 
            }
            else if (e.State == PttState.PressAny)
            {
                LiPTT.PttEventEchoed -= Exit_echoed;
                LiPTT.RemoveHandlerStateChecker();
                LiPTT.PressAnyKey();
                LiPTT.Client.Disconnected += GoToLogin;
            }
        }

        private async void GoToLogin(object o, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                LiPTT.Frame.Navigate(typeof(LoginPage));
                LiPTT.Client.Disconnected -= GoToLogin;
            });
        }

        private int pivot_index = -1;

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pivot.SelectedIndex == 1)
            {
                LiPTT.GoToFavorite();
            }
            else
            {
                if (pivot_index == 1) LiPTT.Left();

                switch (pivot.SelectedIndex)
                {
                    case 2:
                        HotFrame.Navigate(typeof(HotPage));
                        break;
                    case 3:
                        SettingFrame.Navigate(typeof(SettingPage));
                        break;
                    case 4:
                        CreateSymbolList();
                        break;
                }
            }
        }

        private void CreateSymbolList()
        {
            GridView gridView = new GridView();

            foreach (Symbol bol in Enum.GetValues(typeof(Symbol)))
            {
                StackPanel sp = new StackPanel { Width = 100, Height = 100 };
                sp.Children.Add(new SymbolIcon(bol) {
                    Foreground = new SolidColorBrush(Windows.UI.Colors.AntiqueWhite),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch, Margin = new Thickness(0, 33, 0, 0) });
                sp.Children.Add(new TextBlock{ Text = bol.ToString(), Foreground = new SolidColorBrush(Windows.UI.Colors.AntiqueWhite), TextAlignment = TextAlignment.Center , HorizontalAlignment = HorizontalAlignment.Stretch});
                GridViewItem item = new GridViewItem
                {
                    Content = sp
                };



                gridView.Items.Add(item);
            }

            symbolPivotItem.Content = gridView;
        }
    }
}
