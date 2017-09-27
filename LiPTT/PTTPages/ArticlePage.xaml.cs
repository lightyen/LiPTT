using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.System;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Documents;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
                //GoBack();
            };
        }

        private EchoContentDialog EchoDialog { get; set; }

        private Article article;

        private bool control_visible;

        public Visibility ControlVisible
        {
            get
            {
                if (control_visible)
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
            private set
            {
                if (value == Visibility.Visible)
                    control_visible = true;
                else
                    control_visible = false;
                NotifyPropertyChanged("ControlVisible");
                NotifyPropertyChanged("RingActive");
            }
        }

        public bool RingActive
        {
            get
            {
                return !control_visible;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ControlVisible = Visibility.Collapsed;
            //冷靜一下，先喝杯咖啡
            await Task.Delay(50);
            article = LiPTT.CurrentArticle;

            ContentCollection.BeginLoaded += (a, b) => {
                if (ListVW.Items.Count > 0) ListVW.ScrollIntoView(ListVW.Items[0]);
                Window.Current.CoreWindow.PointerPressed += ArticlePage_PointerPressed;
                Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            };

            LoadingExtraData = false;
            pressAny = false;

            LiPTT.PttEventEchoed += ReadAIDandExtra;
            LiPTT.Right();            
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed -= ArticlePage_PointerPressed;
        }

        private async void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (EchoDialog.Showing) return;
            
            bool Control_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;
            bool Shift_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0;
            bool CapsLocked = (Window.Current.CoreWindow.GetKeyState(VirtualKey.CapitalLock) & CoreVirtualKeyStates.Locked) != 0;

            var scrollviewer = GetScrollViewer(ListVW);

            switch (args.VirtualKey)
            {
                case VirtualKey.PageDown:
                case VirtualKey.Down:
                    scrollviewer?.ChangeView(0, scrollviewer.VerticalOffset + scrollviewer.ViewportHeight - 50.0, null);
                    break;
                case VirtualKey.PageUp:
                case VirtualKey.Up:
                    scrollviewer?.ChangeView(0, scrollviewer.VerticalOffset - scrollviewer.ViewportHeight + 50.0, null);
                    break;
                case VirtualKey.Home:
                    if (ListVW.Items.Count > 0) ListVW.ScrollIntoView(ListVW.Items.First());
                    break;
                case VirtualKey.End:
                    if (ListVW.Items.Count > 0) ListVW.ScrollIntoView(ListVW.Items.Last());
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

        private bool PressRight = false;

        private void ArticlePage_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            if (EchoDialog.Showing) return;

            if (PressRight == false && args.CurrentPoint.Properties.IsRightButtonPressed)
            {
                Debug.WriteLine("Article PressRight");
                PressRight = true;
                Window.Current.CoreWindow.PointerReleased += ArticlePage_PointerReleased;
            }
        }

        private void ArticlePage_PointerReleased(CoreWindow sender, PointerEventArgs args)
        {
            if (PressRight)
            {
                PressRight = false;
                Window.Current.CoreWindow.PointerPressed -= ArticlePage_PointerPressed;
                Window.Current.CoreWindow.PointerReleased -= ArticlePage_PointerReleased;
                GoBack();
            }
        }

        private bool LoadingExtraData;

        private bool pressAny;

        private void ReadAIDandExtra(PTTProvider sender, LiPttEventArgs e)
        {
            if (e.State == PttState.Article && !LoadingExtraData)
            {
                LoadingExtraData = true;
                LiPTT.SendMessage('Q');
            }
            else if (!pressAny && e.State == PttState.PressAny && LoadingExtraData)
            {
                pressAny = true;
                ReadExtraData(e.Screen);
                LiPTT.PressAnyKey();
            }
            else if (e.State == PttState.Board && LoadingExtraData)
            {
                LiPTT.PttEventEchoed -= ReadAIDandExtra;
                LiPTT.PttEventEchoed += BrowseArticle;
                LiPTT.Right();
            }
        }

        private void BrowseArticle(PTTProvider sender, LiPttEventArgs e)
        {
            switch (e.State)
            {
                case PttState.Article:
                    LiPTT.PttEventEchoed -= BrowseArticle;
                    LoadArticle();                   
                    break;
            }
        }

        private void LoadArticle()
        {
            var action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ContentCollection.ListViewProxy = ListVW.Resources["proxy"] as ActualSizePropertyProxy;
                ContentCollection.BeginLoad(LiPTT.CurrentArticle);
                ArticleHeader.DataContext = LiPTT.CurrentArticle;
                ControlVisible = Visibility.Visible;
            });
            
        }

        private void ReadExtraData(ScreenBuffer screen)
        {
            //AID
            article.AID = screen.ToString(19, 18, 9);
            Debug.WriteLine("文章代碼: " + article.AID);
            //網頁版網址
            string str = screen.ToString(20);
            Regex regex = new Regex(LiPTT.http_regex);
            Match match = regex.Match(str);
            if (match.Success)
            {
                string aaa = str.Substring(match.Index, match.Length);

                try
                {
                    article.WebUri = new Uri(str.Substring(match.Index, match.Length));
                    Debug.WriteLine("網頁版: " + article.WebUri.OriginalString);
                }
                catch (UriFormatException ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }

            //P幣
            str = screen.ToString(21);
            regex = new Regex(@"\d+");
            match = regex.Match(str);
            if (match.Success)
            {
                article.PttCoin = Convert.ToInt32(str.Substring(match.Index, match.Length));
                Debug.WriteLine("PTT Coin: " + article.PttCoin.ToString());
            }
        }

        private void StopVideo()
        {
            foreach (var o in ContentCollection)
            {
                if (o is Grid grid)
                {
                    if (GetUIElement<WebView>(grid) is WebView view)
                    {
                        view.Navigate(new Uri("ms-appx-web:///Templates/blank.html"));
                    }                 
                }
            }
        }

        private bool back = false;

        private void GoBack()
        {
            if (!control_visible && !ContentCollection.InitialLoaded && ContentCollection.Loading && LiPTT.ArticleCollection != null || LiPTT.Frame.CurrentSourcePageType != typeof(ArticlePage)) return;

            if (back) return;
            back = true;
            StopVideo();
            LiPTT.PttEventEchoed += PttEventEchoed_UpdateArticleTag;
            LiPTT.Left();
        }

        private void PttEventEchoed_UpdateArticleTag(PTTProvider sender, LiPttEventArgs e)
        {
            LiPTT.PttEventEchoed -= PttEventEchoed_UpdateArticleTag;

            if (e.State == PttState.Board)
            {
                ReLoadArticleTag(e.Screen);
                LiPTT.PageEnd();
                //LiPTT.SendMessage(LiPTT.ArticleCollection.CurrentIndex.ToString(), 0x0D);
                var action = LiPTT.RunInUIThread(() =>
                {
                    LiPTT.Frame.Navigate(typeof(BoardPage));
                });
            }
        }

        private void ReLoadArticleTag(ScreenBuffer screen)
        {
            if (LiPTT.CurrentArticle.ID != int.MaxValue)
            {
                Article article = LiPTT.ArticleCollection.FirstOrDefault(i => i.ID == LiPTT.CurrentArticle.ID);  
            }
            else //置底文
            {
                Article article = LiPTT.ArticleCollection.FirstOrDefault(i => (i.ID == int.MaxValue) && (i.Star == LiPTT.CurrentArticle.Star));
            }

            if (article != null)
            {
                var act = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    article.State = LiPTT.GetReadSate((char)screen.CurrentBlocks[8].Content);
                });
            }
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
    }
}
