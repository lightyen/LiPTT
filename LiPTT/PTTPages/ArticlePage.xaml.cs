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

        string http_exp = @"http(s)?://([\w]+\.)+[\w]+(/[\w ./?%&=]*)?";
        //string k1 = @"(?<link>http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?)+";
        //string k2 = "<a href=\"${link}\">${link}</a>";

        public ArticlePage()
        {
            this.InitializeComponent();
        }

        private Article article;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            LoadingIndicator.IsActive = true;
            article = LiPTT.CurrentArticle;

            ArticleHeaderListBox.Items.Clear();

            ParagraphControl.ItemsSource = null;
            EchoView.ItemsSource = null;

            article.DefaultState();

            LoadingExtraData = false;
            pressAny = false;
            article.Echoes.Article = article;


            LiPTT.PttEventEchoed += ReadAIDandExtra;
            LiPTT.Right();

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
        }

        private void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            switch (args.VirtualKey)
            {
                case VirtualKey.PageDown:
                case VirtualKey.Down:
                    ContentScrollViewer.ChangeView(0, ContentScrollViewer.VerticalOffset + ContentScrollViewer.ViewportHeight - 50.0, null);
                    break;
                case VirtualKey.PageUp:
                case VirtualKey.Up:
                    ContentScrollViewer.ChangeView(0, ContentScrollViewer.VerticalOffset - ContentScrollViewer.ViewportHeight + 50.0, null);
                    break;
                case VirtualKey.Home:
                    ContentScrollViewer.ChangeView(0, 0, null);
                    break;
                case VirtualKey.End:
                    ContentScrollViewer.ChangeView(0, ContentScrollViewer.ScrollableHeight, null);
                    break;
                case VirtualKey.Left:
                    var t = StopVideo();
                    GoBack();
                    break;
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
            if (article.LoadCompleted) return;

            switch (e.State)
            {
                case PttState.Article:
                    {
                        LoadArticle(e.Screen);
                    }
                    break;
                case PttState.PressAny:
                    LiPTT.PttEventEchoed -= BrowseArticle;
                    GoBack();
                    break;
            }
        }

        private void UpdateUI()
        {
            ArticleHeaderListBox.Items.Add(article);

            foreach (var x in article.Content)
            {
                if (x is WebView webview)
                {
                    webview.Navigate(new Uri("ms-appx-web:///Templates/youtube.html"));
                }
            }

            EchoView.ItemsSource = article.Echoes;

            ParagraphControl.ItemsSource = article.Content;
        }

        private async Task UpdatePicture()
        {
            LoadingIndicator.IsActive = true;

            ParagraphControl.ItemsSource = null;

            int k = 0;
            foreach (var t in await Task.WhenAll(article.SomeTasks))
            {
                if (t.Item1 < article.Content.Count)
                {
                    article.Content.Insert(t.Item1 + k, t.Item2);
                    k++;
                }
                else
                {
                    article.Content.Add(t.Item2);
                }
            }

            ParagraphControl.ItemsSource = article.Content;

            LoadingIndicator.IsActive = false;
        }

        private void LoadArticle(ScreenBuffer screen)
        {
            //var x = screen.ToStringArray();

            article.ViewWidth = ContentScrollViewer.ViewportWidth;

            IAsyncAction action = null;

            Bound bound = ReadLineBound(screen.ToString(23));

            Regex regex;
            Match match;
            string tmps;

            bool header = false;

            if (bound.Begin == 1)
            {
                article.Content.Clear();
                //作者、看板
                tmps = screen.ToString(0);
                regex = new Regex(@"作者  \S+ ");
                match = regex.Match(tmps);

                if (match.Success)
                {
                    //作者
                    article.Author = tmps.Substring(match.Index + 4, match.Length - 5);

                    //匿稱
                    article.AuthorNickname = "";
                    int a = match.Index + match.Length + 1;
                    for (int j = a; j < tmps.Length; j++)
                    {
                        if (tmps[j] == ')')
                        {
                            article.AuthorNickname = tmps.Substring(a, j - a);
                            break;
                        }
                    }

                    header = true;
                }
                else
                {
                    Debug.WriteLine("作者?" + tmps);
                    goto READ_CONTENT;
                }

                //時間
                //https://msdn.microsoft.com/zh-tw/library/8kb3ddd4(v=vs.110).aspx
                System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                tmps = LiPTT.Current.Screen.ToString(2, 7, 24);
                if (tmps[8] == ' ') tmps = tmps.Remove(8, 1);

                try
                {
                    article.Date = DateTimeOffset.ParseExact(tmps, "ddd MMM d HH:mm:ss yyyy", provider);
                }
                catch (FormatException)
                {
                    Debug.WriteLine("時間?" + tmps);
                    goto READ_CONTENT;
                }

                READ_CONTENT:;
                //////////////////////////////////////////////////////////////////////////////////////////
                //第一頁文章內容
                for (int i = header ? 4 : 0; i <= bound.End - (header ? 0 : 1); i++)
                {
                    article.AppendLine(screen[i]);                   
                }

                action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    article.Parse();
                });

            }
            else if (bound.Percent < 100)
            {
                for (int i = 1; i < 23; i++)
                {
                    article.AppendLine(screen[i]);
                }

                action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    article.Parse();
                });
            }
            else if (bound.Percent == 100)
            {
                //最後一頁
                for (int i = article.RawLines.Count - bound.Begin + 4; i < 23; i++)
                {
                    article.AppendLine(screen[i]);
                }

                action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    article.Parse();
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

                        await UpdatePicture();
                        
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

                    await UpdatePicture();
                }); 
            }
        }

        private void ReadExtraData(ScreenBuffer screen)
        {
            //AID
            article.AID = screen.ToString(19, 18, 9);
            Debug.WriteLine("文章代碼: " + article.AID);
            //網頁版網址
            string str = screen.ToString(20);
            Regex regex = new Regex(http_exp);
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

        private async void GoBack_Click(object sender, RoutedEventArgs e)
        {
            await StopVideo();
            GoBack();
        }

        private async Task StopVideo()
        {
            foreach (var o in LiPTT.CurrentArticle.Content)
            {
                if (o is WebView youtu)
                {
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

        private void GoBack()
        {
            if (LiPTT.State == PttState.Article && article.LoadCompleted)
            {
                
                LiPTT.PttEventEchoed += PttEventEchoed_UpdateArticleTag;
                LiPTT.Left();
            }
        }

        private void PttEventEchoed_UpdateArticleTag(PTTProvider sender, LiPttEventArgs e)
        {
            if (e.State == PttState.Board)
            {
                LiPTT.PttEventEchoed -= PttEventEchoed_UpdateArticleTag;
                ReLoadArticleTag(e.Screen);

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
