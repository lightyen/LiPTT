using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

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

    /// <summary>
    /// 文章狀態
    /// </summary>
    [Flags]
    public enum ReadState
    {
        無 = 0b0000_0000, //'+' 未讀
        已讀 = 0b0000_0001,
        被標記 = 0b0000_0010,
        有推文 = 0b0000_0100,
        被鎖定 = 0b0000_1000,
        待處理 = 0b0001_0000,
        未定義 = 0b1000_0000,
    }

    /// <summary>
    /// 文章類型
    /// </summary>
    public enum ArticleType
    {
        無,
        一般,
        回覆,
        轉文,
    }

    /// <summary>
    /// 文章評價
    /// </summary>
    public enum Evaluation
    {
        箭頭 = 0,
        推 = 1,
        噓 = 2,
    }

    public class DownloadResult
    {
        public int Index { get; set; }
        public object Item { get; set; }
    }

    /// <summary>
    /// 瀏覽狀態
    /// </summary>
    public class Bound
    {
        /// <summary>
        /// 開頭
        /// </summary>
        public int Begin { get; set; }

        /// <summary>
        /// 結尾
        /// </summary>
        public int End { get; set; }

        /// <summary>
        /// 瀏覽文章比例
        /// </summary>
        public int Percent { get; set; }
    }

    /// <summary>
    /// 回響
    /// </summary>
    public class Echo
    {
        public Evaluation Evaluation { get; set; }
        public string Author { get; set; }
        public string Content { get; set; }
        public DateTimeOffset Date { get; set; }
        public bool DateFormated { get; set; }
        public uint Floor { get; set; }
    }

    /// <summary>
    /// 看板資訊
    /// </summary>
    public class Board : INotifyPropertyChanged
    {
        /// <summary>
        /// 看板名稱
        /// </summary>
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
                NotifyPropertyChanged("Name");
            }
        }

        /// <summary>
        /// 看板分類
        /// </summary>
        public string Category
        {
            get
            {
                return category;
            }
            set
            {
                category = value;
                NotifyPropertyChanged("Category");
            }
        }

        /// <summary>
        /// 看板短名、通稱
        /// </summary>
        public string Nick
        {
            get
            {
                return nick;
            }
            set
            {
                nick = value;
                NotifyPropertyChanged("Nick");
            }
        }

        /// <summary>
        /// 看板描述
        /// </summary>
        public string Description
        {
            get
            {
                return description;
            }
            set
            {
                description = value;
                NotifyPropertyChanged("Description");
            }
        }

        /// <summary>
        /// 人氣
        /// </summary>
        public int Popularity
        {
            get
            {
                return popularity;
            }
            set
            {
                popularity = value;
                NotifyPropertyChanged("Popularity");
            }
        }

        /// <summary>
        /// 版主群
        /// </summary>
        public string[] Leaders
        {
            get
            {
                return leaders;
            }
            set
            {
                leaders = value;
                NotifyPropertyChanged("Leaders");
            }
        }

        /// <summary>
        /// 發文限制-登入次數
        /// </summary>
        public int LimitLogin
        {
            get
            {
                return limitLogin;
            }
            set
            {
                limitLogin = value;
                NotifyPropertyChanged("LimitLogin");
            }
        }

        /// <summary>
        /// 發文限制-被退文次數
        /// </summary>
        public int LimitReject
        {
            get
            {
                return limitReject;
            }
            set
            {
                limitReject = value;
                NotifyPropertyChanged("LimitReject");
            }
        }

        private string name;
        private string category;
        private string nick;
        private string description;
        private int popularity;
        private string[] leaders;
        private int limitLogin;
        private int limitReject;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public class Article : IComparable<Article>, INotifyPropertyChanged
    {
        /// <summary>
        /// 文章流水編號
        /// </summary>
        public uint ID
        {
            get
            {
                return id;
            }
            set
            {
                id = value;
                NotifyPropertyChanged("ID");
            }
        }

        /// <summary>
        /// 文章代碼
        /// </summary>
        public string AID
        {
            get
            {
                return aid;
            }
            set
            {
                aid = value;
                NotifyPropertyChanged("AID");
            }
        }

        /// <summary>
        /// 置底文index
        /// </summary>
        public int Star
        {
            get
            {
                return star;
            }
            set
            {
                star = value;
                NotifyPropertyChanged("Star");
            }
        }

        /// <summary>
        /// 已刪除文章
        /// </summary>
        public bool Deleted
        {
            get
            {
                return deleted;
            }
            set
            {
                deleted = value;
                NotifyPropertyChanged("Deleted");
            }
        }

        /// <summary>
        /// 推噓數
        /// </summary>
        public int Like
        {
            get
            {
                return like;
            }
            set
            {
                like = value;
                NotifyPropertyChanged("Like");
            }
        }

        /// <summary>
        /// 文章日期
        /// </summary>
        public DateTimeOffset Date
        {
            get
            {
                return date;
            }
            set
            {
                date = value;
                NotifyPropertyChanged("Date");
            }
        }

        /// <summary>
        /// 文章作者
        /// </summary>
        public string Author
        {
            get
            {
                return author;
            }
            set
            {
                author = value;
                NotifyPropertyChanged("Author");
            }
        }

        /// <summary>
        /// 文章作者(匿名)
        /// </summary>
        public string AuthorNickname
        {
            get
            {
                return authorNick;
            }
            set
            {
                authorNick = value;
                NotifyPropertyChanged("AuthorNickname");
            }
        }

        /// <summary>
        /// 文章分類 
        /// </summary>
        public string Category
        {
            get
            {
                return category;
            }
            set
            {
                category = value;
                NotifyPropertyChanged("Category");
            }
        }

        /// <summary>
        /// 文章標題
        /// </summary>
        public string Title
        {
            get
            {
                return title;
            }
            set
            {
                title = value;
                NotifyPropertyChanged("Title");
            }
        }

        public string InnerTitle
        {
            get
            {
                return innerTitle;
            }
            set
            {
                innerTitle = value;
                NotifyPropertyChanged("InnerTitle");
            }
        }

        /// <summary>
        /// 文章類型
        /// </summary>
        public ArticleType Type
        {
            get
            {
                return atype;
            }
            set
            {
                atype = value;
                NotifyPropertyChanged("Type");
            }
        }

        /// <summary>
        /// P幣數
        /// </summary>
        public int PttCoin
        {
            get
            {
                return coin;
            }
            set
            {
                coin = value;
                NotifyPropertyChanged("PttCoin");
            }
        }

        /// <summary>
        /// 網頁版連結
        /// </summary>
        public Uri WebUri
        {
            get
            {
                return uri;
            }
            set
            {
                uri = value;
                NotifyPropertyChanged("WebUri");
            }
        }

        /// <summary>
        /// 文章狀態
        /// </summary>
        public ReadState State
        {
            get
            {
                return rstate;
            }
            set
            {
                rstate = value;
                NotifyPropertyChanged("State");
            }
        }


        private uint id;
        private string aid;
        private int star;
        private bool deleted;
        private int like;
        private DateTimeOffset date;
        private string author;
        private string authorNick;
        private string category;
        private string title;
        private string innerTitle;
        private ArticleType atype;
        private int coin;
        private Uri uri;
        private ReadState rstate;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

    public class ActualSizePropertyProxy : FrameworkElement, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public FrameworkElement Element
        {
            get { return (FrameworkElement)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        public double ActualHeightValue
        {
            get { return Element == null ? 0 : Element.ActualHeight; }
        }

        public double ActualWidthValue
        {
            get { return Element == null ? 0 : Element.ActualWidth; }
        }

        public static readonly DependencyProperty ElementProperty =
            DependencyProperty.Register("Element", typeof(FrameworkElement), typeof(ActualSizePropertyProxy),
                                        new PropertyMetadata(null, OnElementPropertyChanged));

        private static void OnElementPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ActualSizePropertyProxy)d).OnElementChanged(e);
        }

        private void OnElementChanged(DependencyPropertyChangedEventArgs e)
        {
            FrameworkElement oldElement = (FrameworkElement)e.OldValue;
            FrameworkElement newElement = (FrameworkElement)e.NewValue;

            newElement.SizeChanged += new SizeChangedEventHandler(Element_SizeChanged);
            if (oldElement != null)
            {
                oldElement.SizeChanged -= new SizeChangedEventHandler(Element_SizeChanged);
            }

            NotifyPropertyChanged("ActualWidthValue");
            NotifyPropertyChanged("ActualHeightValue");
        }

        private void Element_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            NotifyPropertyChanged("ActualWidthValue");
            NotifyPropertyChanged("ActualHeightValue");
        }

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RatioConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            if (value is double width)
            {
                return (1-0.2) * width * 0.5625;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class RingRatioConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            if (value is double width)
            {
                return width * 0.075;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
