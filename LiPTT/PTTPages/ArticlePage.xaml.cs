using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.System;
using Windows.Foundation.Collections;
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
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace LiPTT
{
    /// <summary>
    /// 瀏覽文章頁面
    /// </summary>
    public sealed partial class ArticlePage : Page
    {
        public ArticlePage()
        {
            InitializeComponent();
        }

        private Article article;


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ArticleHeaderListBox.Visibility = Visibility.Collapsed;          
            ListVW.Visibility = Visibility.Collapsed;

            ArticleHeaderListBox.Items.Clear();
            LoadingIndicator.IsActive = true;
            article = LiPTT.CurrentArticle;

            ContentCollection.BeginLoaded += (a, b) => {
                if (ListVW.Items.Count > 0) ListVW.ScrollIntoView(ListVW.Items[0]);
            };

            SizeChanged += (o, a) => {
                ContentCollection.ViewWidth = ContentGrid.ActualWidth;
            };

            LoadingExtraData = false;
            pressAny = false;

            LiPTT.PttEventEchoed += ReadAIDandExtra;
            LiPTT.Right();

            Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed -= CoreWindow_PointerPressed;
        }

        private void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
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
                    if (!ContentCollection.InitialLoaded && ContentCollection.Loading) return;
                    StopVideo();
                    GoBack();
                    break;
            }
        }

        private void CoreWindow_PointerPressed(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.PointerEventArgs args)
        {
            if (args.CurrentPoint.Properties.IsRightButtonPressed == true)
            {
                if (!ContentCollection.InitialLoaded && ContentCollection.Loading) return;
                StopVideo();
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
                ContentCollection.BeginLoad(LiPTT.CurrentArticle);
                ArticleHeaderListBox.Items.Add(LiPTT.CurrentArticle);
                
                LoadingIndicator.IsActive = false;
                ArticleHeaderListBox.Visibility = Visibility.Visible;
                ListVW.Visibility = Visibility.Visible;
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
                article.Url = new Uri(str.Substring(match.Index, match.Length));
                Debug.WriteLine("網頁版: " + article.Url.OriginalString);
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
                    if ((string)(grid.Tag) == "Youtube")
                    {
                        foreach (object e in grid.Children)
                        {
                            if (e is WebView youtu)
                            {
                                youtu.Navigate(new Uri("ms-appx-web:///Templates/youtube/blank.html"));
                            }
                        } 
                    }
                    
                }
            }
        }

        private void GoBack()
        {
            if (LiPTT.State == PttState.Article)
            {
                isReadArticleTag = true;
                LiPTT.PttEventEchoed += PttEventEchoed_UpdateArticleTag;
                LiPTT.Left();
            }
        }

        private bool isReadArticleTag;

        private void PttEventEchoed_UpdateArticleTag(PTTProvider sender, LiPttEventArgs e)
        {
            if (e.State == PttState.Board)
            {
                if (isReadArticleTag)
                {
                    isReadArticleTag = false;
                    ReLoadArticleTag(e.Screen);
                    LiPTT.SendMessage(LiPTT.ArticleCollection.CurrentIndex.ToString(), 0x0D);
                }
                else
                {
                    LiPTT.PttEventEchoed -= PttEventEchoed_UpdateArticleTag;
                    var action = LiPTT.RunInUIThread(() =>
                    {
                        LiPTT.Frame.Navigate(typeof(BoardPage));
                    });
                }
            }
        }

        private void ReLoadArticleTag(ScreenBuffer screen)
        {
            if (LiPTT.CurrentArticle.ID != int.MaxValue)
            {
                Article article = LiPTT.ArticleCollection.FirstOrDefault(i => i.ID == LiPTT.CurrentArticle.ID);

                if (article != null)
                {
                    char readtype = (char)screen.CurrentBlocks[8].Content;
                    article.ReadType = LiPTT.GetReadType(readtype);
                }
            }
            else //置底文
            {
                Article article = LiPTT.ArticleCollection.FirstOrDefault(i => (i.ID == int.MaxValue) && (i.Star == LiPTT.CurrentArticle.Star));

                if (article != null)
                {
                    char readtype = (char)screen.CurrentBlocks[8].Content;
                    article.ReadType = LiPTT.GetReadType(readtype);
                }
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
    }
}
