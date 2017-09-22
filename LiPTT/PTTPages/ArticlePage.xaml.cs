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
            var sv = (ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(this.ListVW, 0), 0);
            var scrollbars = sv.GetDescendantsOfType<ScrollBar>().ToList();
            var verticalBar = scrollbars.FirstOrDefault(x => x.Orientation == Orientation.Vertical);
            if (verticalBar != null)
                verticalBar.Scroll += BarScroll;

            LoadingIndicator.IsActive = true;
            article = LiPTT.CurrentArticle;
            article.LoadCompleted = false;
            ArticleHeaderListBox.Items.Clear();

            //ParagraphControl.ItemsSource = null;

            //EchoView.ItemsSource = null;

            article.DefaultState();

            LoadingExtraData = false;
            pressAny = false;
            //article.Echoes.Article = article;
            //article.ViewWidth = ContentScrollViewer.ViewportWidth;

            LiPTT.PttEventEchoed += ReadAIDandExtra;
            LiPTT.Right();

            Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        private void BarScroll(object sender, ScrollEventArgs e)
        {
            if (e.ScrollEventType != ScrollEventType.EndScroll) return;

            ContentCollection.ScrollEnd = true;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed -= CoreWindow_PointerPressed;
        }

        private async void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            switch (args.VirtualKey)
            {
                case VirtualKey.PageDown:
                case VirtualKey.Down:
                    //ContentScrollViewer.ChangeView(0, ContentScrollViewer.VerticalOffset + ContentScrollViewer.ViewportHeight - 50.0, null);
                    break;
                case VirtualKey.PageUp:
                case VirtualKey.Up:
                    //ContentScrollViewer.ChangeView(0, ContentScrollViewer.VerticalOffset - ContentScrollViewer.ViewportHeight + 50.0, null);
                    break;
                case VirtualKey.Home:
                    //ContentScrollViewer.ChangeView(0, 0, null);
                    break;
                case VirtualKey.End:
                    //ContentScrollViewer.ChangeView(0, ContentScrollViewer.ScrollableHeight, null);
                    break;
                case VirtualKey.Left:
                case VirtualKey.Escape:
                    if (!article.LoadCompleted) return;
                    await StopVideo();
                    GoBack();
                    break;
            }
        }

        private async void CoreWindow_PointerPressed(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.PointerEventArgs args)
        {
            if (args.CurrentPoint.Properties.IsRightButtonPressed == true)
            {
                if (!article.LoadCompleted) return;
                await StopVideo();
                GoBack();
            }
        }

        /***
        private void UpdateUI()
        {
            LoadingIndicator.IsActive = true;

            ArticleHeaderListBox.Items.Add(article);
            
            EchoView.ItemsSource = article.Echoes;

            ParagraphControl.ItemsSource = null;
            ParagraphControl.ItemsSource = article.Content;

            LoadingIndicator.IsActive = false;
        }
        /***/

        /***
        private async Task UpdateDownloadTaskView()
        {
            if (article.DownloadTasks.Count == 0)
            {
                UILoadCompleted = true;
            }
            else
            {

                foreach (var t in await Task.WhenAll(article.SomeTasks))
                {
                    article.Content[t.Item1] = t.Item2;
                    ParagraphControl.ItemsSource = null;
                    ParagraphControl.ItemsSource = article.Content;
                }


                while (article.DownloadTasks.Count > 0)
                {
                    var firstFinishedTask = await Task.WhenAny(article.DownloadTasks);

                    article.Content[firstFinishedTask.Result.Index] = firstFinishedTask.Result.Item;
                    ParagraphControl.ItemsSource = null;
                    ParagraphControl.ItemsSource = article.Content;

                    article.DownloadTasks.Remove(firstFinishedTask);
                }

                UILoadCompleted = true;
            }

            
        }
        /***/

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

            ContentCollection.BeginLoad(LiPTT.CurrentArticle);

            var action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ArticleHeaderListBox.Items.Add(LiPTT.CurrentArticle);
                LoadingIndicator.IsActive = false;
            });
            
        }


        //已讀行數
        private int line;

        //有無文章標頭
        bool header = false;

        /***
        private void LoadArticle(ScreenBuffer screen)
        {
            //var x = screen.ToStringArray();
            //#1PkNrDxZ
            IAsyncAction action = null;

            Bound bound = ReadLineBound(screen.ToString(23));

            Regex regex;
            Match match;
            string tmps;

            if (bound.Begin == 1)
            {
                article.Content.Clear();

                tmps = screen.ToString(3);

                if (tmps.StartsWith("───────────────────────────────────────"))
                {
                    header = true;
                }

                if (header)
                {
                    //作者
                    tmps = screen.ToString(0);
                    regex = new Regex(@"作者  [A-Za-z0-9]+ ");
                    match = regex.Match(tmps);
                    if (match.Success)
                    {                        
                        article.Author = tmps.Substring(match.Index + 4, match.Length - 5);
                    }

                    //匿稱
                    article.AuthorNickname = "";
                    regex = new Regex(@"\([\S\s^\(^\)]+\)");
                    match = regex.Match(tmps);
                    if (match.Success)
                    {
                        article.AuthorNickname = tmps.Substring(match.Index + 1, match.Length - 2);
                    }

                    //標題
                    //已讀過 這裡不再Parse

                    //時間
                    //https://msdn.microsoft.com/zh-tw/library/8kb3ddd4(v=vs.110).aspx
                    System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                    tmps = LiPTT.Screen.ToString(2, 7, 24);
                    if (tmps[8] == ' ') tmps = tmps.Remove(8, 1);

                    try
                    {
                        article.Date = DateTimeOffset.ParseExact(tmps, "ddd MMM d HH:mm:ss yyyy", provider);
                    }
                    catch (FormatException)
                    {
                        Debug.WriteLine("時間格式有誤? " + tmps);
                    }

                    line = 3;
                }
                else
                {
                    line = 0;
                    Debug.WriteLine("沒有文章標頭? " + tmps);
                }

                //////////////////////////////////////////////////////////////////////////////////////////
                //第一頁文章內容
                //////////////////////////////////////////////////////////////////////////////////////////

                int o = bound.End - bound.Begin + 1;
                if (o < 23)
                {
                    if (header) o = bound.End + 1;
                    else o = bound.End;
                }

                for (int i = header ? 4 : 0; i < o; i++, line++)
                {
                    article.AppendLine(screen[i]);
                }

                action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    article.ParseBeta();
                });
            }
            else
            {
                int o = header ? 1 : 0;

                for (int i = line - bound.Begin + 1 + (bound.Begin < 5 ? o : 0); i < 23; i++, line++)
                {
                    article.AppendLine(screen[i]);
                }

                action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    article.ParseBeta();
                });
            }

            article.PageDownPercent = bound.Percent;

            //捲到下一頁
            if (bound.Percent < 100)
            {
                if (article.ParsedContent)
                {
                    LiPTT.PttEventEchoed -= BrowseArticle;

                    var task = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        article.Echoes.Percent = bound.Percent;
                        
                        article.LoadCompleted = true;
                        
                        article.Echoes.HasMoreItems = true;

                        UpdateUI();

                        await UpdateDownloadTaskView();
                        
                    });
                }
                else
                {
                    LiPTT.PageDown();
                }
            }
            else
            {
                LiPTT.PttEventEchoed -= BrowseArticle;

                var task = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    article.Echoes.Percent = bound.Percent;

                    article.LoadCompleted = true;
                    
                    article.Echoes.HasMoreItems = false;

                    UpdateUI();

                    await UpdateDownloadTaskView();
                }); 
            }
        }
        /***/

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

        private async Task StopVideo()
        {
            foreach (var o in LiPTT.CurrentArticle.Content)
            {
                if (o is Grid grid)
                {
                    if ((string)(grid.Tag) == "Youtube")
                    {
                        foreach (object e in grid.Children)
                        {
                            if (e.GetType() == typeof(WebView))
                            {
                                WebView youtu = (WebView)e;
                                string script = @"document.getElementById('player').stopVideo();";

                                try
                                {
                                    await youtu.InvokeScriptAsync("eval", new string[] { script });
                                }
                                catch (Exception ex)
                                {
                                    youtu.Navigate(new Uri("ms-appx-web:///Templates/youtube.html"));
                                    Debug.WriteLine(ex.ToString());
                                }
                            }
                        } 
                    }
                    
                }
            }
        }

        private void GoBack()
        {
            if (LiPTT.State == PttState.Article && article.LoadCompleted)
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

        private Bound ReadLineBound(string msg)
        {
            Bound bound = new Bound();
            Regex regex = new Regex(@"\([\d\s]+%\)");
            Match match = regex.Match(msg);

            if (match.Success)
            {
                string percent = msg.Substring(match.Index + 1, match.Length - 3);
                bound.Percent = Convert.ToInt32(percent);
            }

            regex = new Regex(@"第\s[\d~]+\s行");
            match = regex.Match(msg, match.Length);

            if (match.Success)
            {
                string s = msg.Substring(match.Index + 2, match.Length - 4);
                string[] a = s.Split('~');
                bound.Begin = Convert.ToInt32(a[0]);
                bound.End = Convert.ToInt32(a[1]);
            }

            return bound;
        }
    }
}
