using System;
using System.Text.RegularExpressions;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Controls;

namespace LiPTT
{
    public class ArticleIDStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return uint.MaxValue.ToString();

            if (value is uint v)
            {
                if (v != uint.MaxValue)
                {
                    return v.ToString();
                }
                else
                {
                    return "★";
                }
            }

            return uint.MaxValue.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class LikeStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return "  ";
            }

            if (value is int v)
            {
                if (v > 0)
                {
                    if (v == 100) return "爆";
                    else return string.Format("{0}", v);
                }
                else if (v < 0)
                {
                    if (v == -100)
                        return string.Format("XX");
                    else
                        return string.Format("X{0}", -v / 10);
                }
                else
                {
                    return "  ";
                }
            }
            else
            {
                return "  ";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class LikeColorStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            Color color = Colors.Black;

            if (value is int v)
            {
                if (v == 100)
                {
                    color = Colors.Red;
                }
                else if (v > 0)
                {
                    color = Colors.Yellow;
                }
                else if (v < 0)
                {
                    color = Colors.Gray;
                }
            }

            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class DateTimeOffsetStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            if (parameter == null) return value;

            return string.Format((string)parameter, value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class EchoEvaluationStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            if (value is Evaluation eval)
            {
                switch (eval)
                {
                    case Evaluation.推:
                        return "推";
                    case Evaluation.噓:
                        return "噓";
                    case Evaluation.箭頭:
                        return "→";
                    default:
                        return "";
                }
            }
            else return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class EchoDateStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            if (value is DateTimeOffset date)
            {
                return date.ToString("MM/dd HH:mm");
            }
            else return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class EchoEvaluationColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            Color color = Colors.Black;

            if (value is Evaluation eval)
            {
                switch (eval)
                {
                    case Evaluation.噓:
                        color = Colors.Red;
                        break;
                    case Evaluation.推:
                        color = Colors.Yellow;
                        break;
                    case Evaluation.箭頭:
                        color = Colors.Purple;
                        break;
                }
            }

            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class TitleColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            Color color = Colors.Black;

            if (value is Article article)
            {
                if (article.ID == uint.MaxValue)
                {
                    color = Colors.LightSkyBlue;
                }
                else if (article.Category == "公告")
                {
                    color = Colors.CadetBlue;
                }
                else if (article.Deleted)
                {
                    color = Colors.IndianRed;
                }
                else if (article.ReadType.HasFlag(ReadType.有推文))
                {
                    color = Colors.AliceBlue;
                }
                else if (article.ReadType.HasFlag(ReadType.已讀))
                {
                    color = Colors.Gray;
                }
                else if (Math.Abs(article.Like) > 25)
                {
                    color = Colors.Gold;
                }
                else
                {
                    color = Colors.Goldenrod;
                }
            }

            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ReadTypeStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            if (value is ReadType t)
            {
                if (t == ReadType.無) return "+";
                else if (t.HasFlag(ReadType.被鎖定)) return "!";
                else if (t.HasFlag(ReadType.有推文))
                {
                    if (t.HasFlag(ReadType.被標記)) return "=";
                    else return "~";

                }
                else if (t.HasFlag(ReadType.已讀))
                {
                    if (t.HasFlag(ReadType.被標記)) return "m";
                    else if (t.HasFlag(ReadType.待處理)) return "s";
                    else return " ";
                }
                else
                {
                    if (t.HasFlag(ReadType.被標記)) return "M";
                    else if (t.HasFlag(ReadType.待處理)) return "S";
                    else return null;
                }
            }
            else return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ArticleTypeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            Color color = Colors.Black;

            if (value is ArticleType type)
            {
                switch(type)
                {
                    case ArticleType.一般:
                        color = Colors.DarkGoldenrod;
                        break;
                    case ArticleType.轉文:
                        color = Colors.Gray;
                        break;
                    case ArticleType.回覆:
                        color = Colors.Red;
                        break;
                    default:
                        color = Colors.Black;
                        break;
                }
            }

            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class PopularityColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            Color color = Colors.Black;

            if (value is int popu)
            {
                if (popu >= 100000) color = Colors.Purple;
                else if (popu >= 60000) color = Colors.Yellow;
                else if (popu >= 30000) color = Colors.Green;
                else if (popu >= 10000) color = Colors.Cyan;
                else if (popu >= 5000) color = Colors.Blue;
                else if (popu >= 2000) color = Colors.Red;
                else if (popu >= 1000) color = Colors.White;
                else color = Colors.Gray;
            }
            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ArticleTypeStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            if (value is ArticleType type)
            {
                switch(type)
                {
                    case ArticleType.回覆:
                        return "Re:";
                    case ArticleType.轉文:
                        return "轉";
                    case ArticleType.一般:
                        return "●";
                    default:
                        return "";
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class EchoContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;

            if (value is string str)
            {
                Match match;

                if ((match = new Regex(LiPTT.http_regex).Match(str)).Success)
                {
                    StackPanel sp = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Stretch };

                    if (match.Index > 0)
                    {
                        sp.Children.Add(new TextBlock()
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            FontSize = 22,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = new SolidColorBrush(Colors.Gold),
                            Text = str.Substring(0, match.Index),
                        });
                    }

                    string url = str.Substring(match.Index, match.Length);
                    sp.Children.Add(new HyperlinkButton()
                    {
                        NavigateUri = new Uri(url),
                        FontSize = 22,
                        Content = new TextBlock() { Text = url },
                    });

                    if (match.Index + match.Length < str.Length)
                    {
                        sp.Children.Add(new TextBlock()
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            FontSize = 22,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = new SolidColorBrush(Colors.Gold),
                            Text = str.Substring(match.Index + match.Length, str.Length - (match.Index + match.Length)),
                        });
                    }

                    return sp;
                }
                else
                {
                    return new TextBlock()
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        FontSize = 22,
                        Foreground = new SolidColorBrush(Colors.Gold),
                        Text = str,
                    };
                }




            }
            else return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}