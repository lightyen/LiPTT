using System;
using System.Linq;
using Windows.System;
using Windows.System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace LiPTT
{
    /// <summary>
    /// 瀏覽文章頁面
    /// </summary>
    public sealed partial class ArticlePage : Page, INotifyPropertyChanged
    {
        public ArticlePage()
        {
            InitializeComponent();
            DataContext = this;
            EchoDialog = new EchoContentDialog();

            EchoDialog.PrimaryButtonClick += (a, b) =>
            {
                GoBack();
            };

            this.DataContext = this;
        }

        private EchoContentDialog EchoDialog { get; set; }

        private Article article;

        private bool control_visible;

        public bool ControlVisible
        {
            get
            {
                return control_visible;
            }
            private set
            {
                control_visible = value;
                NotifyPropertyChanged("ControlVisible");             
            }
        }

        private bool ringActive;

        public bool RingActive
        {
            get
            {
                return ringActive;
            }
            set
            {
                ringActive = value;
                NotifyPropertyChanged("RingActive");
            }
        }

        private double VerticalScrollOffset;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            PTT ptt = Application.Current.Resources["PTT"] as PTT;

            ControlVisible = false;
            RingActive = true;

            article = ptt.CurrentArticle;

            ptt.ArticleInfomationCompleted += async (a, b) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    ArticleHeader.DataContext = b.Article;
                    SplitViewPaneContent.DataContext = b.Article;
                });
            };

            ptt.ArticleContentUpdated += ArticleContentBeginLoad;

            ptt.ParseBugAlarmed += (a, b) =>
            {
                var act = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    FlyoutBase.ShowAttachedFlyout(RingGrid);
                });

                ThreadPoolTimer.CreateTimer((timer) => {
                    var acti = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                        FlyoutBase.GetAttachedFlyout(RingGrid).Hide();
                    });
                }, TimeSpan.FromMilliseconds(2000));
            };

            Debug.WriteLine("Article 訂閱事件");
            Window.Current.CoreWindow.PointerPressed += ArticlePage_PointerPressed;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            ContentCollection.FullScreenEntered += EnterFullScreen;
            ContentCollection.FullScreenExited += ExitFullScreen;

            ptt.GoToCurrentArticle();       
        }

        private async void ArticleContentBeginLoad(object sender, ArticleContentUpdatedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                PTT ptt = Application.Current.Resources["PTT"] as PTT;
                ptt.ArticleContentUpdated -= ArticleContentBeginLoad;
                ContentCollection.BeginLoad(e);
                RingActive = false;
                ControlVisible = true;
                back = false;
            });
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Debug.WriteLine("Article 取消訂閱");
            ContentCollection.FullScreenEntered -= EnterFullScreen;
            ContentCollection.FullScreenExited -= ExitFullScreen;
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed -= ArticlePage_PointerPressed;

            base.OnNavigatedFrom(e);
        }

        private async void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (EchoDialog.Showing) return;
            
            bool Control_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;
            bool Shift_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0;
            bool CapsLocked = (Window.Current.CoreWindow.GetKeyState(VirtualKey.CapitalLock) & CoreVirtualKeyStates.Locked) != 0;

            var scrollviewer = GetScrollViewer(ArticleListView);

            switch (args.VirtualKey)
            {
                case VirtualKey.PageDown:
                case VirtualKey.Down:
                case VirtualKey.Space:
                    scrollviewer?.ChangeView(0, scrollviewer.VerticalOffset + scrollviewer.ViewportHeight - 50.0, null);
                    break;
                case VirtualKey.PageUp:
                case VirtualKey.Up:
                    scrollviewer?.ChangeView(0, scrollviewer.VerticalOffset - scrollviewer.ViewportHeight + 50.0, null);
                    break;
                case VirtualKey.Home:
                    if (ArticleListView.Items.Count > 0) ArticleListView.ScrollIntoView(ArticleListView.Items.First());
                    break;
                case VirtualKey.End:
                    if (ArticleListView.Items.Count > 0) ArticleListView.ScrollIntoView(ArticleListView.Items.Last());
                    break;
                case VirtualKey.Left:
                case VirtualKey.Escape:
                    GoBack();
                    break;
                default:
                    if (Shift_Down ^ CapsLocked)
                    {
                        switch (args.VirtualKey)
                        {
                            case VirtualKey.X:
                                await EchoDialog.ShowAsync();
                                break;
                        }
                    }
                    break;
            }
        }

        private void EnterFullScreen(Grid youtuGrid, FullScreenEventArgs e)
        {
            Debug.WriteLine("Article 取消訂閱");
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed -= ArticlePage_PointerPressed;

            var app = Application.Current.Resources["ApplicationProperty"] as ApplicationProperty;
            var setting = Application.Current.Resources["SettingProperty"] as SettingProperty;

            var scrollviewer = GetScrollViewer(ArticleListView);
            if (scrollviewer != null)
            {
                VerticalScrollOffset = scrollviewer.VerticalOffset;
            }

            app.FullScreen = true;
            youtuGrid.Children.Remove(e.WebView);
            VideoGrid.Children.Add(e.WebView);
            e.WebView.Focus(FocusState.Programmatic);
        }

        private void ExitFullScreen(Grid youtuGrid, FullScreenEventArgs e)
        {
            var app = Application.Current.Resources["ApplicationProperty"] as ApplicationProperty;
            var setting = Application.Current.Resources["SettingProperty"] as SettingProperty;

            VideoGrid.Children.Remove(e.WebView);
            youtuGrid.Children.Add(e.WebView);

            app.FullScreen = setting.FullScreen;

            if (GetScrollViewer(ArticleListView) is ScrollViewer scrollviewer)
            {
                var act = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    scrollviewer.UpdateLayout();
                    scrollviewer.ChangeView(0, VerticalScrollOffset, null, true);
                });
            }

            Debug.WriteLine("Article 訂閱事件");
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed += ArticlePage_PointerPressed;
        }

        private void ArticlePage_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            if (EchoDialog.Showing) return;

            if (args.CurrentPoint.Properties.IsRightButtonPressed) GoBack();
        }

        private bool back;

        private void GoBack()
        {
            if (!control_visible || 
                !ContentCollection.InitialLoaded ||
                ContentCollection.Loading ||
                ContentCollection.VideoRun > 0 ||
                LiPTT.Frame.CurrentSourcePageType != typeof(ArticlePage)) return;

            if (back) return;
            back = true;
            StopVideo();

            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            ptt.GoBack();
            ptt.GoBackCompleted += GoBackCompleted;  
        }

        private async void StopVideo()
        {
            foreach (var o in ContentCollection)
            {
                if (o is Grid grid)
                {
                    if (GetUIElement<WebView>(grid) is WebView webview)
                    {
                        if (GetUIElement<Button>(grid) is Button button)
                        {
                            if (button.Visibility == Visibility.Visible)
                            {
                                return;
                            }
                        }
                        await TryStopVideo(webview);
                    }
                }
            }
        }

        private async Task TryStopVideo(WebView webview)
        {
            try
            {
                Debug.WriteLine(string.Format("StopVideo"));
                string returnStr = await webview.InvokeScriptAsync("StopVideo", new string[] { });
                webview.Navigate(new Uri("ms-appx-web:///Templates/blank.html"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Script Error" + ex.ToString());
            }
        }

        private async void GoBackCompleted(object sender, PTTStateUpdatedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                PTT ptt = Application.Current.Resources["PTT"] as PTT;
                ptt.GoBackCompleted -= GoBackCompleted;
                LiPTT.Frame.Navigate(typeof(BoardPage));
            });
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

        private ScrollViewer GetScrollViewer(DependencyObject depObj)
        {
            try
            {
                if (depObj is ScrollViewer)
                {
                    return depObj as ScrollViewer;
                }

                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = VisualTreeHelper.GetChild(depObj, i);

                    var result = GetScrollViewer(child);
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

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void ArticleHeader_Click(object sender, RoutedEventArgs e)
        {
            if (ArticleHeader.DataContext is Article article)
            {
                await Launcher.LaunchUriAsync(article.WebUri);
            }
        }

        private void Page_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pointerPosition = CoreWindow.GetForCurrentThread().PointerPosition;
            var x = pointerPosition.X - Window.Current.Bounds.X;

            if (SplitView.IsPaneOpen == false && x > ActualWidth * 0.99)
            {
                SplitView.IsPaneOpen = true;
            }
            else if (SplitView.IsPaneOpen == true && x < ActualWidth - SplitView.OpenPaneLength)
            {
                SplitView.IsPaneOpen = false;
            }
        }
    }
}
