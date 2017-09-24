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
using Windows.UI.Core;
using Windows.ApplicationModel.Core;

//https://stackoverflow.com/questions/5473001/itemscontrol-with-multiple-datatemplates-for-a-viewmodel

namespace LiPTT
{
    //https://www.ptt.cc/bbs/PttNewhand/M.1265292872.A.991.html
    //未讀 + M S
    //已讀   m s
    //被標記 m
    //待處理 s
    //被鎖文 !
    //新推文 ~
    //被標記且有新推文 =

    [Flags]
    public enum ReadType
    {
        無       = 0b0000_0000, //'+' 未讀
        已讀     = 0b0000_0001,
        被標記   = 0b0000_0010,
        有推文   = 0b0000_0100,
        被鎖定   = 0b0000_1000,
        待處理   = 0b0001_0000,
        未定義   = 0b1000_0000,
    }

    public enum ArticleType
    {
        無,
        一般,
        回覆,
        轉文,
    }

    public enum Evaluation
    {
        箭頭 = 0,
        推 = 1,
        噓 = 2,
    }

    public class Bound
    {
        public int Begin { get; set; }
        public int End { get; set; }
        public int Percent { get; set; }
    }

    public class Echo
    {
        public Evaluation Evaluation { get; set; }
        public string Author { get; set; }
        public string Content { get; set; }
        public DateTimeOffset Date { get; set; }
    }

    public class DownloadResult
    {
        public int Index { get; set; }
        public object Item { get; set; }
    }

    public class BoardInfo : INotifyPropertyChanged
    {
        private string name;
        private int popularity;
        private string nick;
        private string description;
        private string[] leaders;
        private string category;
        private int limit_login;
        private int limit_reject;

        public string Name
        {
            get { return name; }
            set { name = value; NotifyPropertyChanged("Name"); }
        }

        public string Category
        {
            get { return category; }
            set { category = value; NotifyPropertyChanged("Category"); }
        }

        public string NickName
        {
            get { return nick; }
            set { nick = value; NotifyPropertyChanged("NickName"); }
        }

        public string Description
        {
            get { return description; }
            set { description = value; NotifyPropertyChanged("Description"); }
        }

        public int Popularity
        {
            get { return popularity; }
            set { popularity = value; NotifyPropertyChanged("Popularity"); }
        }

        public string[] Leaders
        {
            get { return leaders; }
            set { leaders = value; NotifyPropertyChanged("Leaders"); }
        }

        public int LimitLogin
        {
            get { return limit_login; }
            set { limit_login = value; NotifyPropertyChanged("LimitLogin"); }
        }
        public int LimitReject
        {
            get { return limit_reject; }
            set { limit_reject = value; NotifyPropertyChanged("LimitReject"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                var a = LiPTT.RunInUIThread(() => {
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
                
            }
        }
    }

    public class Article : IComparable<Article>, INotifyPropertyChanged
    {
        public uint ID { get; set; } //文章流水號
        public string AID { get; set; } //文章代碼
        public int Star { get; set; } //置底文index
        public bool Deleted { get; set; } //已被刪除
        public int Like { get; set; } //推/噓個數
        public DateTimeOffset Date { get; set; } //時間
        public string Author { get; set; } //作者
        public string AuthorNickname { get; set; } //作者匿名
        public string Category { get; set; } //文章分類
        public string Title { get; set; } //標題
        public ArticleType Type { get; set; } //文章類型
        public int PttCoin { get; set; } //值多少P幣
        public Uri Url { get; set; } //網頁版連結
        private ReadType readtype;
        public ReadType ReadType
        {
            get
            {
                return readtype;
            }
            set
            {
                readtype = value;
                NotifyPropertyChanged("ReadType");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private SolidColorBrush GetForegroundBrush(Block b)
        {
            switch (b.ForegroundColor)
            {
                case 30:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
                case 31:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0x00)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0x00, 0x00));
                case 32:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0x00)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xC0, 0x00));
                case 33:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0x00));
                case 34:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0xFF)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0xC0));
                case 35:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0xFF)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0x00, 0xC0));
                case 36:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0xFF)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xC0, 0xC0));
                case 37:
                    return b.Mode.HasFlag(AttributeMode.Bold) ?
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)):
                        new SolidColorBrush(Color.FromArgb(0xFF, 0xD0, 0xD0, 0xD0));
                default:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0));
            }
        }

        private SolidColorBrush GetBackgroundBrush(Block b)
        {
            switch (b.BackgroundColor)
            {
                case 40:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
                case 41:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x00, 0x00));
                case 42:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x80, 0x00));
                case 43:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x80, 0x00));
                case 44:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x80));
                case 45:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x00, 0x80));
                case 46:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x80, 0x80));
                case 47:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0));
                default:
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
            }
        }
        

        public int CompareTo(Article other)
        {
            if (this.ID != uint.MaxValue && other.ID != uint.MaxValue)
            {
                if (this.ID > other.ID) return -1; //大的排前面
                else if (this.ID < other.ID) return 1;
                else return 0;
            }
            else if (this.ID == uint.MaxValue && other.ID == uint.MaxValue)
            {
                if (this.Star > other.Star) return -1;
                else if (this.Star < other.Star) return 1;
                else return 0;
            }
            else
            {
                if (this.ID == uint.MaxValue) return -1;
                else if (other.ID == uint.MaxValue) return 1;
                else return 0;
            }
        }
    }

    public class ArticleCollection : ObservableCollection<Article>, ISupportIncrementalLoading
    {
        private BoardInfo boardInfo;

        public BoardInfo BoardInfo
        {
            get { return boardInfo; }
            set
            {
                boardInfo = value;
                var a = LiPTT.RunInUIThread(() => { OnPropertyChanged(new PropertyChangedEventArgs("BoardInfo")); });
            }
        }

        public int StarCount { get; set; }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
        }

        private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint count)
        {
            if (CurrentIndex == 0)
            {
                HasMoreItems = false;
            }
            else if(!reading)
            {
                reading = true;

                await Task.Run(() => {
                    LiPTT.PttEventEchoed += ReadBoard_EventEchoed;
                    LiPTT.SendMessage(CurrentIndex.ToString(), 0x0D);
                });
            }

            return new LoadMoreItemsResult { Count = CurrentIndex };
        }

        private bool reading;

        private void ReadBoard_EventEchoed(PTTProvider sender, LiPttEventArgs e)
        {
            if (e.State == PttState.Board)
            {
                ReadBoard(e.Screen);
            }
        }

        private void ReadBoard(ScreenBuffer screen)
        {
            Regex regex;
            Match match;
            string str;
            int star = StarCount + 1;
            /////////////////////////////
            ///文章
            ///
            uint id = uint.MaxValue;

            var x = screen.ToStringArray();

            for (int i = 22; i >= 3; i--)
            {
                Article article = new Article();

                //ID流水號
                str = screen.ToString(i, 0, 8);

                if (str.IndexOf('★') != -1)
                {
                    article.ID = uint.MaxValue;
                    article.Star = star++;
                    StarCount = article.Star;
                }
                else
                {
                    match = new Regex(@"\d+").Match(str);
                    if (match.Success)
                    {
                        id = Convert.ToUInt32(str.Substring(match.Index, match.Length));
                        article.ID = id;

                        if (id > CurrentIndex) continue;

                        else if (id == CurrentIndex) CurrentIndex = article.ID - 1;
                        else //id 被遮住
                        {
                            article.ID = CurrentIndex;
                            CurrentIndex = article.ID - 1;
                        }
                        //
                        //if (this.Any(x => x.ID == article.ID)) continue;
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

                //ReadType
                char c = (char)screen[i][8].Content;
                article.ReadType = LiPTT.GetReadType(c);
                
                //日期
                str = screen.ToString(i, 11, 5);
                try
                {
                    article.Date = DateTimeOffset.Parse(str);
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(str + ex.ToString());
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
                str = screen.ToString(i, 30, screen.Width - 30).Replace('\0', ' ');

                //是否被刪除?
                if (article.Author == "-") article.Deleted = true;
                else article.Deleted = false;

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
                    regex = new Regex(@"[\u005b\uff3b]+?[\w\s]+[\u005d\uff3d]+?");
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

                var action = LiPTT.RunInUIThread(() =>
                {
                    this.Add(article);
                });
            }

            LiPTT.PttEventEchoed -= ReadBoard_EventEchoed;
            reading = false;
        }

        /// <summary>
        /// 當前位置
        /// </summary>
        public uint CurrentIndex;

        public ArticleCollection()
        {
            CurrentIndex = uint.MaxValue;
            StarCount = 0;
            //locker = new SemaphoreSlim(1, 1);
            BoardInfo = new BoardInfo();

            this.CollectionChanged += ArticleCollection_CollectionChanged;
        }

        private void ArticleCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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

        private bool more;

        public bool HasMoreItems
        {
            get
            {
                if (!reading) return more;
                else return false;
            }
            set { more = value; }
        }
    }

}
