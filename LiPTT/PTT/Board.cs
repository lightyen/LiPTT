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
        public void LoadBoardInfomation()
        {
            if (State == PttState.Board)
            {
                LiPTT.CacheBoard = true;

                CurrentBoard = new Board();

                //人氣
                string str = Screen.ToString(2);
                Regex regex = new Regex(@"\d+");
                Match match = regex.Match(str);
                if (match.Success)
                {
                    int popu = Convert.ToInt32(str.Substring(match.Index, match.Length));
                    CurrentBoard.Popularity = popu;
                }

                PTTStateUpdated += ReadBoardInfo;
                PressI();
            }
        }

        private void ReadBoardInfo(object sender, PTTStateUpdatedEventArgs e)
        {
            if (State == PttState.BoardInfomation)
            {
                var Board = CurrentBoard;

                //看板名稱
                string str = Screen.ToString(3);
                Match match = new Regex(LiPTT.bracket_regex).Match(str);
                if (match.Success)
                {
                    Board.Name = str.Substring(match.Index + 1, match.Length - 2);
                    Board.Nick = LiPTT.GetBoardNick(Board.Name);
                }

                //看板分類 中文敘述
                Board.Category = Screen.ToString(5, 15, 4);
                Board.Description = Screen.ToString(5, 22, Screen.Width - 22).Replace('\0', ' ').Trim();

                //版主名單
                str = Screen.ToString(6, 15, Screen.Width - 15).Replace('\0', ' ').Trim();
                if (!new Regex(LiPTT.bound_regex).Match(str).Success) //(無)
                {
                    Board.Leaders = str.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                }

                if (Screen.ToString(7, 25, 4) == "公開")
                    Board.公開 = true;

                if (Screen.ToString(8, 12, 4) == "可以")
                    Board.可進入十大排行榜 = true;

                if (Screen.ToString(9, 5, 4) == "開放")
                    Board.開放非看板會員發文 = true;

                if (Screen.ToString(10, 5, 4) == "開放")
                    Board.開放回文 = true;

                if (Screen.ToString(11, 5, 4) == "開放")
                    Board.開放自刪 = true;

                if (Screen.ToString(12, 5, 4) == "開放")
                    Board.開放推文 = true;

                if (Screen.ToString(13, 5, 4) == "開放")
                    Board.開放噓文 = true;

                if (Screen.ToString(14, 5, 4) == "開放")
                    Board.開放快速連推 = true;

                if (Screen.ToString(15, 12, 4) == "自動")
                    Board.IPVisible = true;

                if (Screen.ToString(16, 12, 4) == "對齊")
                    Board.自動對齊 = true;

                if (Screen.ToString(17, 10, 2) == "可")
                    Board.板主可刪除違規文字 = true;

                if (Screen.ToString(18, 14, 2) == "會")
                    Board.轉文自動記錄 = true;

                if (Screen.ToString(19, 5, 2) == "已")
                    Board.冷靜模式 = true;

                if (Screen.ToString(20, 5, 4) == "允許")
                    Board.允許十八歲進入 = true;

                //發文限制 - 登入次數
                str = Screen.ToString(12);
                match = new Regex(@"\d+").Match(str);
                if (match.Success)
                {
                    try
                    {
                        Board.LimitLogin = Convert.ToInt32(str.Substring(match.Index, match.Length));
                    }
                    catch { Debug.WriteLine(str.Substring(match.Index, match.Length)); }
                }

                //發文限制 - 退文篇數
                str = Screen.ToString(13);
                match = new Regex(@"\d+").Match(str);
                if (match.Success)
                {
                    try
                    {
                        Board.LimitReject = Convert.ToInt32(str.Substring(match.Index, match.Length));
                    }
                    catch { Debug.WriteLine(str.Substring(match.Index, match.Length)); }
                }


                PressAnyKey();
            }
            else if (e.State == PttState.Board)
            {
                PTTStateUpdated -= ReadBoardInfo;
                BoardInfomationCompleted?.Invoke(this, new BoardInfomationCompletedEventArgs { BoardInfomation = CurrentBoard });
            }
        }

        public bool HasMoreArticle
        {
            get
            {
                if (State == PttState.Board && CurrentIndex > 0)
                    return true;
                else
                    return false;
            }
        }

        private void ReadArticleTag()
        {
            //Find Cursor
            //read Tag
            //press Q any
            //add list

            //event

        }

        public void GetArticles()
        {
            if (State == PttState.Board)
            {
                PTTStateUpdated += ReadBoard;
                if (CurrentIndex != uint.MaxValue)
                    Send(CurrentIndex.ToString(), 0x0D);
                else
                    PageEnd();
            }
        }

        private uint CurrentIndex;

        private async void ReadBoard(object sender, PTTStateUpdatedEventArgs e)
        {
            PTTStateUpdated -= ReadBoard;

            if (e.State == PttState.Board)
            {
                List<Article> ArticleTags = new List<Article>();

                Regex regex;
                Match match;
                string str;
                uint id = 0;

                /////////////////////////////
                ///置底文 and 其他文章
                ///
                for (int i = 22; i >= 3; i--)
                {
                    Article article = new Article();

                    //ID流水號
                    str = Screen.ToString(i, 0, 8);
                    if (str.IndexOf('★') != -1)
                    {
                        article.ID = uint.MaxValue;

                        if (string.IsNullOrEmpty(article.AID))
                        {
                            await Task.Run(() => {
                                article.AID = GetTargetAID(i);
                                Debug.WriteLine(string.Format("置底文章代碼: {0}", article.AID));
                            });
                        }

                    }
                    else
                    {
                        regex = new Regex(@"(\d+)");
                        match = regex.Match(str);

                        if (match.Success)
                        {
                            id = Convert.ToUInt32(str.Substring(match.Index, match.Length));
                            article.ID = id;

                            if (id > CurrentIndex) continue;

                            if (id != CurrentIndex && CurrentIndex != uint.MaxValue) //id 被游標遮住
                                article.ID = CurrentIndex;

                            CurrentIndex = article.ID - 1;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    //推文數
                    str = Screen.ToString(i, 9, 2);

                    if (str[0] == '爆')
                    {
                        article.Like = 100;
                    }
                    else if (str[0] == 'X')
                    {
                        if (str[1] == 'X')
                        {
                            article.Like = -100;
                        }
                        else
                        {
                            article.Like = Convert.ToInt32(str[1].ToString());
                            article.Like = -article.Like * 10;
                        }
                    }
                    else
                    {
                        regex = new Regex(@"(\d+)");
                        match = regex.Match(str);
                        if (match.Success) article.Like = Convert.ToInt32(str.Substring(match.Index, match.Length));
                        else article.Like = 0;
                    }

                    //未讀、已讀、M文 等等等
                    article.State = LiPTT.GetReadSate((char)Screen[i][8].Content);

                    //文章日期
                    str = Screen.ToString(i, 11, 5);
                    try
                    {
                        article.Date = DateTimeOffset.Parse(str);
                    }
                    catch
                    {
                        continue;
                    }

                    //作者
                    str = Screen.ToString(i, 17, 13).Replace('\0', ' ');
                    regex = new Regex(@"[\w\S]+");
                    match = regex.Match(str);
                    if (match.Success) article.Author = str.Substring(match.Index, match.Length);

                    //文章類型
                    str = Screen.ToString(i, 30, 2).Replace('\0', ' ');
                    if (str.StartsWith("R:")) article.Type = ArticleType.回覆文;
                    else if (str.StartsWith("□")) article.Type = ArticleType.一般文;
                    else if (str.StartsWith("轉")) article.Type = ArticleType.轉文;
                    else article.Type = ArticleType.無;
                    //是否被刪除
                    if (article.Author == "-") article.Deleted = true;
                    else article.Deleted = false;

                    str = Screen.ToString(i, 30, Screen.Width - 30).Replace('\0', ' ');

                    if (article.Deleted)
                    {
                        article.Title = str;
                        regex = new Regex(LiPTT.bracket_regex);
                        match = regex.Match(str);
                        string s = str.Substring(1);
                        if (match.Success)
                        {
                            article.Author = str.Substring(match.Index + 1, match.Length - 2);
                            s = s.Replace(match.ToString(), "");
                        }
                        article.Title = s;
                        article.Type = ArticleType.無;
                    }
                    else
                    {
                        //標題, 分類
                        regex = new Regex(LiPTT.bracket_regex);
                        match = regex.Match(str);
                        if (match.Success)
                        {
                            article.Category = str.Substring(match.Index + 1, match.Length - 2).Trim();
                            str = str.Substring(match.Index + match.Length);
                            int k = 0;
                            while (k < str.Length && str[k] == ' ') k++;
                            int j = str.Length - 1;
                            while (j >= 0 && str[j] == ' ') j--;
                            if (k <= j) article.Title = str.Substring(k, j - k + 1);
                        }
                        else
                        {
                            article.Title = str.Substring(2);
                        }
                    }

                    ArticleTags.Add(article);
                }

                CurrentIndex = id - 1;
                ArticlesReceived?.Invoke(this, new ArticlesReceivedEventArgs { Articles = ArticleTags });
            }
        }

        public void NavigateToIDorAID(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                NavigateToIDorAIDCompleted?.Invoke(this, new PTTStateUpdatedEventArgs { State = State, Article = null });
                return;
            }
            else if (id.StartsWith("#"))
            {
                Regex regex = new Regex(@"^\#\S{8}");
                Match match = regex.Match(id);
                if (!match.Success)
                {
                    NavigateToIDorAIDCompleted?.Invoke(this, new PTTStateUpdatedEventArgs { State = State, Article = null });
                    return;
                }
            }
            else
            {
                try
                {
                    uint uid = Convert.ToUInt32(id);
                }
                catch (FormatException)
                {
                    NavigateToIDorAIDCompleted?.Invoke(this, new PTTStateUpdatedEventArgs { State = State, Article = null });
                    return;
                }
            }

            PTTStateUpdated += OnNavigateToIDorAIDCompleted;
            Send(id, 0x0D);
        }

        private async void OnNavigateToIDorAIDCompleted(object sender, PTTStateUpdatedEventArgs e)
        {
            if (e.State == PttState.Board)
            {
                PTTStateUpdated -= OnNavigateToIDorAIDCompleted;
                Article article = await ParseArticleTag(e.Screen);
                NavigateToIDorAIDCompleted?.Invoke(this, new PTTStateUpdatedEventArgs { State = e.State, Article = article });
            }
            else if (e.State == PttState.PressAny)
            {
                PTTStateUpdated -= OnNavigateToIDorAIDCompleted;
                NavigateToIDorAIDCompleted?.Invoke(this, new PTTStateUpdatedEventArgs { State = e.State, Article = null });
            }
        }

        private async Task<Article> ParseArticleTag(ScreenBuffer Screen)
        {
            int i = Screen.CurrentY - 1;

            Article article = new Article();
            
            //AID
            string str;
            Regex regex;
            Match match;
            
            if (string.IsNullOrEmpty(article.AID))
            {
                await Task.Run(() =>
                {
                    article.AID = GetTargetAID(i);
                    Debug.WriteLine(string.Format("NavigateTo 文章代碼: {0}", article.AID));
                });

                if (string.IsNullOrEmpty(article.AID)) return null;
            }

            str = Screen.ToString(i, 0, 8);

            if (str.IndexOf('★') == -1)
            {
                regex = new Regex(@"(\d+)");

                if (str[0] == '●')
                {
                    if (i > 3)
                    {
                        string id_str = Screen.ToString(i - 1, 0, 2) + Screen.ToString(i, 2, 6);
                        match = regex.Match(id_str);

                        if (match.Success)
                        {
                            article.ID = Convert.ToUInt32(match.ToString());
                        }
                    }
                }
                else if (str[0] == '>')
                {
                    string id_str = Screen.ToString(i, 0, 8);
                    match = regex.Match(id_str);
                    if (match.Success)
                    {
                        article.ID = Convert.ToUInt32(match.ToString());
                    }
                }
            }

            //推文數
            str = Screen.ToString(i, 9, 2);

            if (str[0] == '爆')
            {
                article.Like = 100;
            }
            else if (str[0] == 'X')
            {
                if (str[1] == 'X')
                {
                    article.Like = -100;
                }
                else
                {
                    article.Like = Convert.ToInt32(str[1].ToString());
                    article.Like = -article.Like * 10;
                }
            }
            else
            {
                regex = new Regex(@"(\d+)");
                match = regex.Match(str);
                if (match.Success) article.Like = Convert.ToInt32(str.Substring(match.Index, match.Length));
                else article.Like = 0;
            }

            //未讀、已讀、M文 等等等
            article.State = LiPTT.GetReadSate((char)Screen[i][8].Content);

            //文章日期
            str = Screen.ToString(i, 11, 5);
            try
            {
                article.Date = DateTimeOffset.Parse(str);
            }
            catch
            {
                return null;
            }

            //作者
            str = Screen.ToString(i, 17, 13).Replace('\0', ' ');
            regex = new Regex(@"[\w\S]+");
            match = regex.Match(str);
            if (match.Success) article.Author = str.Substring(match.Index, match.Length);

            //文章類型
            str = Screen.ToString(i, 30, 2).Replace('\0', ' ');
            if (str.StartsWith("R:")) article.Type = ArticleType.回覆文;
            else if (str.StartsWith("□")) article.Type = ArticleType.一般文;
            else if (str.StartsWith("轉")) article.Type = ArticleType.轉文;
            else article.Type = ArticleType.無;
            //是否被刪除
            if (article.Author == "-") article.Deleted = true;
            else article.Deleted = false;

            str = Screen.ToString(i, 30, Screen.Width - 30).Replace('\0', ' ');

            if (article.Deleted)
            {
                article.Title = str;
                regex = new Regex(LiPTT.bracket_regex);
                match = regex.Match(str);
                string s = str.Substring(1);
                if (match.Success)
                {
                    article.Author = str.Substring(match.Index + 1, match.Length - 2);
                    s = s.Replace(match.ToString(), "");
                }
                article.Title = s;
                article.Type = ArticleType.無;
            }
            else
            {
                //標題, 分類
                regex = new Regex(LiPTT.bracket_regex);
                match = regex.Match(str);
                if (match.Success)
                {
                    article.Category = str.Substring(match.Index + 1, match.Length - 2).Trim();
                    str = str.Substring(match.Index + match.Length);
                    int k = 0;
                    while (k < str.Length && str[k] == ' ') k++;
                    int j = str.Length - 1;
                    while (j >= 0 && str[j] == ' ') j--;
                    if (k <= j) article.Title = str.Substring(k, j - k + 1);
                }
                else
                {
                    article.Title = str.Substring(2);
                }
            }

            return article;
        }

    }
}
