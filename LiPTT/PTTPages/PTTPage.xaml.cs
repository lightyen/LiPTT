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
using Windows.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using Windows.Gaming.Input;

namespace LiPTT
{
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
            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            ptt.IsAppExit = false;
            MainFuncFrame.Navigate(typeof(MainFunctionPage));
            ptt.PTTStateUpdated += Updated;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            ptt.PTTStateUpdated -= Updated;
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Escape)
                Exit();
        }

        private async void Updated(object sender, PTTStateUpdatedEventArgs e)
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
            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            ptt.PTTStateUpdated -= Updated;

            ptt.Exit();
            LiPTT.Logined = false;
            LiPTT.Frame.Navigate(typeof(LoginPage));
        }


        private async void GoToLogin(object o, EventArgs e)
        {
            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                LiPTT.Logined = false;
                LiPTT.Frame.Navigate(typeof(LoginPage));
                ptt.Disconnected -= GoToLogin;
            });
        }

        private int pivot_index = -1;

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            if (pivot.SelectedIndex == 1)
            {
                ptt.GoToFavorite();
            }
            else
            {
                if (pivot_index == 1) ptt.Left();

                switch (pivot.SelectedIndex)
                {
                    case 2:
                        HotFrame.Navigate(typeof(HotPage));
                        break;
                    case 3:
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
                sp.Children.Add(new TextBlock { Text = bol.ToString(), Foreground = new SolidColorBrush(Windows.UI.Colors.AntiqueWhite), TextAlignment = TextAlignment.Center , HorizontalAlignment = HorizontalAlignment.Stretch});
                sp.Children.Add(new TextBlock { Text = string.Format("{0}", (int)bol), Foreground = new SolidColorBrush(Windows.UI.Colors.AntiqueWhite), TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch });
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
