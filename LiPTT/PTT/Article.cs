using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.Gaming.Input;
using Windows.Storage;

namespace LiPTT
{
    public partial class PTT
    {
        private bool ReadArticleInfomationCompleted;

        private bool BackToArticle;

        public bool HasMoreArticleContent
        {
            get
            {
                if (Bound.Progress < 100)
                    return true;
                else
                    return false;
            }
        }

        public void GetArticleContent()
        {
            if (State == PttState.Article)
            {
                if (Bound.Progress < 100)
                    PageDown();
            }
        }

        public void GoToCurrentArticle()
        {
            if (CurrentArticle != null)
            {
                GoToArticle(CurrentArticle);
            }
        }

        public void GoToArticle(Article article)
        {
            if (article.AID?.Length == 9)
            {
                CurrentArticle = article;
                PTTStateUpdated += ReadArticleInfomation;
                ReadArticleInfomationCompleted = false;
                BackToArticle = false;
                Debug.WriteLine(string.Format("Go To {0}", article.AID));
                Send(article.AID, 0x0D, 'r');
            }
            else if (article.ID != uint.MaxValue)
            {
                CurrentArticle = article;
                PTTStateUpdated += ReadArticleInfomation;
                ReadArticleInfomationCompleted = false;
                BackToArticle = false;
                Debug.WriteLine(string.Format("Go To {0}", article.ID));
                Send(article.ID.ToString(), 0x0D, 'r');
            }
        }

        private async void ReadArticleInfomation(object sender, PTTStateUpdatedEventArgs e)
        {
            if (e.State == PttState.Article)
            {
                if (!ReadArticleInfomationCompleted)
                {
                    Regex regex;
                    Match match;
                    string tmps;

                    tmps = e.Screen.ToString(3);

                    if (tmps.StartsWith("───────────────────────────────────────"))
                    {
                        e.Article.HasHeader = true;
                    }

                    if (e.Article.HasHeader)
                    {
                        //作者
                        tmps = e.Screen.ToString(0);
                        regex = new Regex(@"作者  [A-Za-z0-9]+ ");
                        match = regex.Match(tmps);
                        if (match.Success)
                        {
                            e.Article.Author = tmps.Substring(match.Index + 4, match.Length - 5);
                        }

                        //匿稱
                        e.Article.AuthorNickname = "";
                        regex = new Regex(@"\([\S\s^\(^\)]+\)");
                        match = regex.Match(tmps);
                        if (match.Success)
                        {
                            e.Article.AuthorNickname = tmps.Substring(match.Index + 1, match.Length - 2);
                        }

                        //內文標題
                        tmps = e.Screen.ToString(1).Replace('\0', ' ').Trim();
                        match = new Regex(LiPTT.bracket_regex).Match(tmps);
                        if (match.Success)
                        {
                            if (match.Index + match.Length + 1 < tmps.Length)
                            {
                                e.Article.InnerTitle = tmps.Substring(match.Index + match.Length + 1);
                            }
                            else
                            {
                                e.Article.InnerTitle = e.Article.Title;
                            }
                        }
                        else
                        {
                            if (tmps.StartsWith("標題 "))
                                tmps = tmps.Substring(3).Trim();

                            if (tmps != "")
                                e.Article.InnerTitle = tmps;
                            else
                                e.Article.InnerTitle = e.Article.Title;
                        }

                        //時間
                        //https://msdn.microsoft.com/zh-tw/library/8kb3ddd4(v=vs.110).aspx
                        System.Globalization.CultureInfo provider = new System.Globalization.CultureInfo("en-US");
                        tmps = e.Screen.ToString(2, 7, 24);
                        if (tmps[8] == ' ') tmps = tmps.Remove(8, 1);

                        try
                        {
                            e.Article.Date = DateTimeOffset.ParseExact(tmps, "ddd MMM d HH:mm:ss yyyy", provider);
                        }
                        catch (FormatException)
                        {
                            Debug.WriteLine("時間格式有誤? " + tmps);
                        }

                        //line = 3;
                    }
                    else
                    {
                        //line = 0;
                        Debug.WriteLine("沒有文章標頭? " + tmps);
                        e.Article.InnerTitle = e.Article.Title;
                    }

                    Send('Q');
                }
                else
                {
                    PTTStateUpdated -= ReadArticleInfomation;
                    ArticleInfomationCompleted?.Invoke(this, new ArticleInfomationCompletedEventArgs { Article = e.Article });
                }
            }
            else if (e.State == PttState.AID)
            {
                await LiPTT.RunInUIThread(() =>
                {
                    //AID
                    if (e.Article.AID?.Length != 9)
                    {
                        e.Article.AID = Screen.ToString(e.AIDLine, 18, 9);
                    }
                    //網頁版網址
                    string str = Screen.ToString(e.AIDLine + 1);
                    Regex regex = new Regex(LiPTT.http_regex);
                    Match match1 = regex.Match(str);
                    if (match1.Success)
                    {
                        string url = str.Substring(match1.Index, match1.Length);

                        try
                        {
                            e.Article.WebUri = new Uri(url);
                        }
                        catch (UriFormatException ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }
                    }

                    //P幣
                    string p = Screen.ToString(e.AIDLine + 2);
                    regex = new Regex(@"\d+");
                    Match match2 = regex.Match(p);
                    if (match2.Success)
                        e.Article.PttCoin = Convert.ToInt32(p.Substring(match2.Index, match2.Length));
                    else
                        e.Article.PttCoin = 0;

                    ReadArticleInfomationCompleted = true;
                });

                PressAnyKey();
            }
            else if (e.State == PttState.PressAny && !BackToArticle && ReadArticleInfomationCompleted)
            {
                BackToArticle = true;
                Bound = new Bound();
                Right();
            }
            else if (e.State == PttState.Board && !BackToArticle && ReadArticleInfomationCompleted)
            {
                BackToArticle = true;
                Bound = new Bound();
                Right();
            }
        }
    }
}
