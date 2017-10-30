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

using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media.Animation;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.UI.Popups;

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class AppPage : Page
    {
        public AppPage()
        {
            this.InitializeComponent();
            
        }

        RenderTargetBitmap rtb = new RenderTargetBitmap();

        private async Task StartDissolvingAsync()
        {
            try
            {
                await rtb.RenderAsync(SplitContent);
                DissolveImage.Source = rtb;

                DissolveImage.Visibility = Visibility.Visible;
                DissolveImage.Opacity = 1.0;
                AnimateDouble(DissolveImage, "Opacity", 0.0, 200, () =>
                {
                    DissolveImage.Visibility = Visibility.Collapsed;
                });


                SplitContentTransform.Y = 20;
                AnimateDouble(SplitContentTransform, "Y", 0, 150);
            }
            catch
            {
                // Ignore error
                DissolveImage.Visibility = Visibility.Collapsed;
            }
        }

        private static void AnimateDouble(DependencyObject target, string path, double to, double duration, Action onCompleted = null)
        {
            var animation = new DoubleAnimation
            {
                EnableDependentAnimation = true,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(duration))
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, path);

            var sb = new Storyboard();
            sb.Children.Add(animation);

            if (onCompleted != null)
            {
                sb.Completed += (s, e) =>
                {
                    onCompleted();
                };
            }

            sb.Begin();
        }


        private async void OnSelectMenu(string label)
        {
            var splitView = GetUIElement<SplitView>(HamburgerMenu);
            SolidColorBrush PaneBackground = splitView.GetValue(SplitView.PaneBackgroundProperty) as SolidColorBrush;

            switch (label)
            {
                case "測試頁":
                    HamburgerMenu.DisplayMode = SplitViewDisplayMode.Overlay;
                    PaneBackground.Opacity = 0.8;
                    splitView.SetValue(SplitView.PaneBackgroundProperty, PaneBackground);

                    break;
                default:
                    HamburgerMenu.DisplayMode = SplitViewDisplayMode.CompactOverlay;
                    PaneBackground.Opacity = 1.0;
                    splitView.SetValue(SplitView.PaneBackgroundProperty, PaneBackground);
                    break;
            }

            switch (label)
            {
                case "我的最愛":
                    if (AppPageFrame.CurrentSourcePageType != typeof(FavoritePage))
                    {
                        await StartDissolvingAsync();
                        AppPageFrame.Navigate(typeof(FavoritePage));
                    }
                    break;
                case "熱門看板":
                    if (AppPageFrame.CurrentSourcePageType != typeof(HotPage))
                    {
                        await StartDissolvingAsync();
                        AppPageFrame.Navigate(typeof(HotPage));
                    }
                    break;
                case "設定":
                    if (AppPageFrame.CurrentSourcePageType != typeof(SettingPage))
                    {
                        await StartDissolvingAsync();
                        AppPageFrame.Navigate(typeof(SettingPage));
                    }   
                    break;
                case "Debug":
                    if (AppPageFrame.CurrentSourcePageType != typeof(BBSPage))
                    {
                        await StartDissolvingAsync();
                        AppPageFrame.Navigate(typeof(BBSPage));
                    }                       
                    break;
                default:
                    if (AppPageFrame.CurrentSourcePageType != typeof(BlankPage))
                    {
                        await StartDissolvingAsync();
                        AppPageFrame.Navigate(typeof(BlankPage));
                    } 
                    break;
            }
        }

        private void MenuItemClick(object sender, ItemClickEventArgs e)
        {
            var menuItem = e.ClickedItem as HamburgerMenuImageItem;
            OnSelectMenu(menuItem.Label);
        }

        private void MenuOptionItemClick(object sender, ItemClickEventArgs e)
        {
            var menuItem = e.ClickedItem as HamburgerMenuImageItem;
            OnSelectMenu(menuItem.Label);
        }

        private DependencyObject GetUIElement<T>(DependencyObject depObj)
        {
            try
            {
                if (depObj is T)
                {
                    return depObj;
                }

                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = VisualTreeHelper.GetChild(depObj, i);

                    var result = GetUIElement<T>(child);
                    if (result != null)
                    {
                        return result;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
