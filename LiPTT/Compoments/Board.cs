using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using System.Collections.ObjectModel;
using Windows.Foundation;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml;

namespace LiPTT
{
    public class BoardContentCollection : ObservableCollection<Article>, ISupportIncrementalLoading
    {
        private Board board;

        public Board Board
        {
            get { return board; }
            set
            {
                board = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Board"));
            }
        }

        public int StarCount { get; set; }

        /// <summary>
        /// 當前位置
        /// </summary>
        public uint CurrentIndex { get; set; }

        public void BeginLoad()
        {
            Parse();
            InitialLoaded = true;
        }

        protected override void ClearItems()
        {
            StarCount = 0;
            CurrentIndex = uint.MaxValue;
            base.ClearItems();
        }

        public BoardContentCollection()
        {
            CollectionChanged += BoardContentCollection_CollectionChanged;
        }

        private void BoardContentCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (Article item in e.NewItems)
                    item.PropertyChanged += MyType_PropertyChanged;

            if (e.OldItems != null)
                foreach (Article item in e.OldItems)
                    item.PropertyChanged -= MyType_PropertyChanged;
        }

        private void MyType_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, sender, sender, IndexOf((Article)sender));

            var t = LiPTT.RunInUIThread(() =>
            {
                OnCollectionChanged(args);
            });
        }

        private SemaphoreSlim sem = new SemaphoreSlim(0, 1);

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
        }

        public void Parse()
        {
            Regex regex;
            Match match;
            string str;
            uint id = 0;
            ScreenBuffer screen = LiPTT.Screen;

            var x = screen.ToStringArray();

            ///////////////////////////////////
            //人氣
            str = screen.ToString(2);
            regex = new Regex(@"\d+");
            match = regex.Match(str);

            if (match.Success)
            {
                int popu = Convert.ToInt32(str.Substring(match.Index, match.Length));
                Board.Popularity = popu;
            }

            /////////////////////////////
            ///置底文 and 其他文章
            ///
            for (int i = 22; i >= 3; i--)
            {
                Article article = new Article();

                //ID流水號
                str = screen.ToString(i, 0, 8);
                if (str.IndexOf('★') != -1)
                {
                    article.ID = uint.MaxValue;
                    article.Star = StarCount++;
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
                }

                //推文數
                str = screen.ToString(i, 9, 2);

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
                article.State = LiPTT.GetReadSate((char)screen[i][8].Content);

                //文章日期
                str = screen.ToString(i, 11, 5);
                try
                {
                    article.Date = DateTimeOffset.Parse(str);
                }
                catch
                {
                    continue;
                }

                //作者
                str = screen.ToString(i, 17, 13).Replace('\0', ' ');
                regex = new Regex(@"[\w\S]+");
                match = regex.Match(str);
                if (match.Success) article.Author = str.Substring(match.Index, match.Length);

                //文章類型
                str = screen.ToString(i, 30, 2).Replace('\0', ' ');
                if (str.StartsWith("R:")) article.Type = ArticleType.回覆;
                else if (str.StartsWith("□")) article.Type = ArticleType.一般;
                else if (str.StartsWith("轉")) article.Type = ArticleType.轉文;
                else article.Type = ArticleType.無;
                //是否被刪除
                if (article.Author == "-") article.Deleted = true;
                else article.Deleted = false;

                str = screen.ToString(i, 30, screen.Width - 30).Replace('\0', ' ');

                if (article.Deleted)
                {
                    article.Title = str;
                    regex = new Regex(@"\[\S+\]");
                    match = regex.Match(str);
                    if (match.Success)
                    {
                        //刪除的人
                        article.Author = str.Substring(match.Index + 1, match.Length - 2);
                        article.Title = "(本文已被刪除)";
                    }
                    else
                    {
                        //被其他人刪除
                        regex = new Regex(@"\(已被\S+刪除\)");
                        match = regex.Match(str);
                        if (match.Success)
                        {
                            article.Author = str.Substring(match.Index + 3, match.Length - 6);
                        }
                        article.Title = str.Substring(1);
                    }
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

                Add(article);
            }

            CurrentIndex = id - 1;

            if (CurrentIndex > 0)
                more = true;
            else
                more = false;
        }

        private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint count)
        {
            if (InitialLoaded)
            {
                await Task.Run(() => {
                    LiPTT.PttEventEchoed += PttUpdated;
                    LiPTT.SendMessage(CurrentIndex.ToString(), 0x0D);
                });

                await sem.WaitAsync();

                return new LoadMoreItemsResult { Count = CurrentIndex };
            }
            else
            {
                return new LoadMoreItemsResult { Count = 0 };
            }
        }

        private void PttUpdated(PTTClient sender, LiPttEventArgs e)
        {
            LiPTT.PttEventEchoed -= PttUpdated;

            if (e.State == PttState.Board)
            {
                var action = LiPTT.RunInUIThread(() =>
                {
                    Parse();
                    sem.Release();
                });
            }
        }

        private bool more;

        public bool InitialLoaded { get; private set; }

        public bool HasMoreItems
        {
            get
            {
                if (InitialLoaded)
                    return more;
                else
                    return false;
            }
            set { more = value; }
        }
    }
}
