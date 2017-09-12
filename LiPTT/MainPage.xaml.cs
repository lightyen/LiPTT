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

using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.ViewManagement;

using System.Diagnostics;

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            //判斷是電腦還是平板
            //Debug.WriteLine(Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily);

            InitializeComponent();

            Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayFrame.Navigate(typeof(PttMainPage));
            RatioPTT.IsChecked = true;
        }

        private void Page_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Debug.WriteLine("released pointer");
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            /***
            if (DisplayFrame.CurrentSourcePageType == typeof(BBSPage))
            {
                SplitView1.DisplayMode = SplitViewDisplayMode.Overlay;
            }
            else if (DisplayFrame.CurrentSourcePageType == typeof(YoutubePage))
            {
                if (LiPTT.IsYoutubeFullScreen)
                    SplitView1.DisplayMode = SplitViewDisplayMode.Overlay;
                else
                    SplitView1.DisplayMode = SplitViewDisplayMode.CompactOverlay;
            }
            else
            {
                SplitView1.DisplayMode = SplitViewDisplayMode.CompactOverlay;
            }

            /***/
        }

        RenderTargetBitmap rtb = new RenderTargetBitmap();

        async Task StartDissolvingAsync()
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

        public static void AnimateDouble(DependencyObject target, string path, double to, double duration, Action onCompleted = null)
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

        private async void ColorPage_Click(object sender, RoutedEventArgs e)
        {
            if (DisplayFrame.CurrentSourcePageType != typeof(ColorPage))
            {
                await StartDissolvingAsync();
                DisplayFrame.Navigate(typeof(ColorPage));
            }
        }

        private void Page_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pointerPosition = Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerPosition;
            var x = pointerPosition.X - Window.Current.Bounds.X;

            if (SplitView1.IsPaneOpen == false && x < this.ActualWidth * 0.025)
            {
                SplitView1.IsPaneOpen = true;
            }
            else if (SplitView1.IsPaneOpen == true && x > SplitView1.OpenPaneLength)
            {
                SplitView1.IsPaneOpen = false;
            }
        }

        private async void RatioBBSChecked(object sender, RoutedEventArgs e)
        {
            RatioBBS.IsEnabled = false;

            if (DisplayFrame.CurrentSourcePageType != typeof(BBSPage))
            {
                // CompactOverlay ==> Overlay
                if (SplitView1.DisplayMode == SplitViewDisplayMode.CompactOverlay)
                {
                    await StartDissolvingAsync();
                    DisplayFrame.Navigate(typeof(BBSPage));
                    SplitView1.DisplayMode = SplitViewDisplayMode.Overlay;
                }
                else
                {
                    await StartDissolvingAsync();
                    DisplayFrame.Navigate(typeof(BBSPage));
                    SplitView1.DisplayMode = SplitViewDisplayMode.Overlay;
                }
            }
        }

        private void RatioBBSUnchecked(object sender, RoutedEventArgs e)
        {
            RatioBBS.IsEnabled = true;
            //SplitView1.DisplayMode = SplitViewDisplayMode.CompactOverlay;
        }

        private async void YoutubePageChecked(object sender, RoutedEventArgs e)
        {
            if (DisplayFrame.CurrentSourcePageType != typeof(YoutubePage))
            {
                if (SplitView1.DisplayMode == SplitViewDisplayMode.CompactOverlay && LiPTT.IsYoutubeFullScreen)
                {
                    // CompactOverlay ==> Overlay
                    await rtb.RenderAsync(SplitContent);
                    DissolveImage.Source = rtb;
                    DissolveImage.Visibility = Visibility.Visible;
                    DissolveImage.Opacity = 1.0;
                    double duration = 150;
                    AnimateDouble(DissolveImage, "Opacity", 0.0, duration + 50, () =>
                    {
                        DissolveImage.Visibility = Visibility.Collapsed;
                    });

                    SplitView1.DisplayMode = SplitViewDisplayMode.Overlay;
                    DisplayFrame.Navigate(typeof(YoutubePage));
                    SplitContentTransform.X = 0;                 
                    SplitContentTransform.Y = 20;
                    AnimateDouble(SplitContentTransform, "Y", 0, duration, () =>
                    {
                        SplitContentTransform.X = 0;
                    });
                }
                else if (SplitView1.DisplayMode == SplitViewDisplayMode.Overlay && !LiPTT.IsYoutubeFullScreen)
                {
                    // Overlay ==> CompactOverlay
                    await rtb.RenderAsync(SplitContent);
                    DissolveImage.Source = rtb;
                    DissolveImage.Visibility = Visibility.Visible;
                    DissolveImage.Opacity = 1.0;
                    double duration = 150;
                    AnimateDouble(DissolveImage, "Opacity", 0.0, duration + 50, () =>
                    {
                        DissolveImage.Visibility = Visibility.Collapsed;
                    });

                    DisplayFrame.Navigate(typeof(YoutubePage));
                    SplitContentTransform.X = SplitView1.CompactPaneLength;

                    SplitContentTransform.Y = 20;
                    AnimateDouble(SplitContentTransform, "Y", 0, duration, () => {

                        SplitView1.DisplayMode = SplitViewDisplayMode.CompactOverlay;
                        SplitContentTransform.X = 0;
                    });
                }
                else
                {
                    await StartDissolvingAsync();
                    DisplayFrame.Navigate(typeof(YoutubePage));
                }
            }
        }

        private void YoutubePageUnchecked(object sender, RoutedEventArgs e)
        {
            //SplitView1.DisplayMode = SplitViewDisplayMode.CompactOverlay;
        }

        private async void TestPageChecked(object sender, RoutedEventArgs e)
        {
            if (DisplayFrame.CurrentSourcePageType != typeof(TestArticlePage))
            {
                await StartDissolvingAsync();
                DisplayFrame.Navigate(typeof(TestArticlePage));
                SplitView1.DisplayMode = SplitViewDisplayMode.CompactOverlay;
            }
        }

        private async void RatioPTTChecked(object sender, RoutedEventArgs e)
        {
            if (DisplayFrame.CurrentSourcePageType != typeof(PttMainPage))
            {
                if (SplitView1.DisplayMode == SplitViewDisplayMode.CompactOverlay)
                {
                    await rtb.RenderAsync(SplitContent);
                    DissolveImage.Source = rtb;
                    DissolveImage.Visibility = Visibility.Visible;
                    DissolveImage.Opacity = 1.0;
                    double duration = 150;
                    AnimateDouble(DissolveImage, "Opacity", 0.0, duration + 50, () =>
                    {
                        DissolveImage.Visibility = Visibility.Collapsed;
                    });

                    SplitView1.DisplayMode = SplitViewDisplayMode.Overlay;
                    DisplayFrame.Navigate(typeof(PttMainPage));
                    SplitContentTransform.X = 0;
                    SplitContentTransform.Y = 20;
                    AnimateDouble(SplitContentTransform, "Y", 0, duration, () =>
                    {
                        SplitContentTransform.X = 0;
                    });
                }

                /***
                if (SplitView1.DisplayMode == SplitViewDisplayMode.Overlay)
                {
                    // Overlay ==> CompactOverlay
                    await rtb.RenderAsync(SplitContent);
                    DissolveImage.Source = rtb;
                    DissolveImage.Visibility = Visibility.Visible;
                    DissolveImage.Opacity = 1.0;
                    double duration = 150;
                    AnimateDouble(DissolveImage, "Opacity", 0.0, duration + 50, () =>
                    {
                        DissolveImage.Visibility = Visibility.Collapsed;
                    });

                    DisplayFrame.Navigate(typeof(PttMainPage));
                    SplitContentTransform.X = SplitView1.CompactPaneLength;

                    SplitContentTransform.Y = 20;
                    AnimateDouble(SplitContentTransform, "Y", 0, duration, () => {

                        SplitView1.DisplayMode = SplitViewDisplayMode.CompactOverlay;
                        SplitContentTransform.X = 0;
                    });
                }
                /***/
                else
                {
                    await StartDissolvingAsync();
                    DisplayFrame.Navigate(typeof(PttMainPage));
                }
            }
        }

    }
}
