using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using System.Diagnostics;
using Windows.UI.Xaml.Navigation;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media.Imaging;
using System.Text;

//https://developers.google.com/youtube/player_parameters
//https://developers.google.com/youtube/iframe_api_reference#Playback_quality

namespace LiPTT
{
    public sealed partial class YoutubePage : Page
    {
        public YoutubePage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //PaneThemeTransition
            SvgImageSource source = new SvgImageSource();
        }

        private void WebView_ContainsFullScreenElementChanged(WebView sender, object args)
        {
            var applicationView = ApplicationView.GetForCurrentView();

            if (sender.ContainsFullScreenElement)
            {
                LiPTT.CurrentWebView = sender;
                LiPTT.IsYoutubeFullScreen = true;
                ///////////////////////////
                foreach (var ui in ArticleGrid.Children)
                {
                    ui.Visibility = Visibility.Collapsed;
                }

                ArticleGrid.Tag = ArticleGrid.Height;
                ArticleGrid.Height = Double.NaN;

                YoutubeGrid.Tag = new YoutubeBorderInfo() {
                    Width = YoutubeGrid.Width,
                    Height = YoutubeGrid.Height,
                    Margin = YoutubeGrid.Margin,
                    HorizontalAlignment = YoutubeGrid.HorizontalAlignment,
                    VerticalAlignment = YoutubeGrid.VerticalAlignment,
                };
                YoutubeGrid.Width = Double.NaN;
                YoutubeGrid.Height = Double.NaN;
                YoutubeGrid.Margin = new Thickness(1, 0, -1, 0);
                YoutubeGrid.VerticalAlignment = VerticalAlignment.Stretch;
                YoutubeGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
                YoutubeGrid.Visibility = Visibility.Visible;

                ///////////////////////////
                var collection = nana.RowDefinitions;
                collection.First().Height = new GridLength(0);

                applicationView.TryEnterFullScreenMode();
            }
            else if (applicationView.IsFullScreenMode)
            {
                LiPTT.CurrentWebView = null;
                LiPTT.IsYoutubeFullScreen = false;
                ///////////////////////////
                ArticleGrid.Height = (Double)ArticleGrid.Tag;

                YoutubeBorderInfo info = (YoutubeBorderInfo)YoutubeGrid.Tag;
                YoutubeGrid.Width = info.Width;
                YoutubeGrid.Height = info.Height;
                YoutubeGrid.Margin = info.Margin;
                YoutubeGrid.HorizontalAlignment = info.HorizontalAlignment;
                YoutubeGrid.VerticalAlignment = info.VerticalAlignment;

                foreach (var ui in ArticleGrid.Children)
                {
                    ui.Visibility = Visibility.Visible;
                }
                ///////////////////////////
                var collection = nana.RowDefinitions;
                collection.First().Height = new GridLength(1, GridUnitType.Star);

                applicationView.ExitFullScreenMode();
            }
        }

        public string GetYoutubeIframeHtml(string videoID, double width, double height, bool autoplay)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(@"<iframe ");
            sb.AppendFormat(@"width=""{0}"" ", (int)Math.Round(width));
            sb.AppendFormat(@"height=""{0}"" ", (int)Math.Round(height));
            sb.AppendFormat(@"src=""http://www.youtube.com/embed/{0}"" ", videoID);

            sb.AppendFormat(@"?rel=0 ");

            sb.AppendFormat(@"frameborder=""0"" ");
            sb.AppendFormat(@"autoplay=""{0}"" ", 0);
            sb.AppendFormat(@"allowfullscreen "); 
            sb.Append(@"></iframe>");

            return sb.ToString();
        }

        private void WebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            //***
            //D6tC1pyrsTM&t=4287s
            //string frame = GetYoutubeIframeHtml("oXp2oE0xQcE", YoutubeWebView.ActualWidth, YoutubeWebView.ActualHeight, true);

            //YoutubeWebView.Visibility = Visibility.Collapsed;


            

            //string SetBodyOverFlowHiddenString = string.Format(format, frame);



            /***/
        }

        private string GetYoutubeScript(string YoutubeID, double width, double height)
        {
            //sb.Append("function onYouTubeIframeAPIReady() { var player = new YT.Player('player', { height: '300', width: '535', videoId: 'oXp2oE0xQcE'}); }");

            string script = "function onYouTubeIframeAPIReady() { var player = new YT.Player('player', { height: '@Height', width: '@Width', videoId: '@YoutubeID'}); }";

            script = script.Replace("@YoutubeID", YoutubeID);
            script = script.Replace("@Width", ((int)Math.Round(width)).ToString());
            script = script.Replace("@Height", ((int)Math.Round(height)).ToString());

            return script;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //string html = GetYoutubeIframeHtml("Bvojzrq9Ad4", myWebView.ActualWidth, myWebView.ActualHeight, true);
            //string html = "https://forum.gamer.com.tw/A.php?bsn=60030";
            //myWebView.NavigateToString(GGG());

            //MyPanel.Children.Clear();
            //https://stackoverflow.com/questions/1602148/binding-to-actualwidth-does-not-work
            Grid MainGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Colors.Gray) };

            ColumnDefinition c1, c2, c3;

            double space = 0.2; //也就是佔總寬的80%

            c1 = new ColumnDefinition { Width = new GridLength(space / 2.0, GridUnitType.Star) };
            c2 = new ColumnDefinition { Width = new GridLength((1 - space), GridUnitType.Star) };
            c3 = new ColumnDefinition { Width = new GridLength(space / 2.0, GridUnitType.Star) };

            MainGrid.ColumnDefinitions.Add(c1);
            MainGrid.ColumnDefinitions.Add(c2);
            MainGrid.ColumnDefinitions.Add(c3);

            Grid InnerGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Colors.Black) };     
            InnerGrid.SetValue(Grid.ColumnProperty, 1);
            MainGrid.Children.Add(InnerGrid);

            Grid youtuGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };
            youtuGrid.SetBinding(HeightProperty, new Binding { ElementName = "proxy", Path = new PropertyPath("ActualWidthValue"), Converter = Application.Current.Resources["MyRatioConverter"] as RatioConverter });
            InnerGrid.Children.Add(youtuGrid);

            MyGrid.Children.Add(MainGrid);
            /***
            Grid grid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,};

            Binding bind = new Binding()
            {
                RelativeSource = new RelativeSource() { Mode = RelativeSourceMode.TemplatedParent },
                Path = new PropertyPath("ActualWidth"),
                Mode = BindingMode.OneWay,
            };

            grid.SetBinding(HeightProperty, bind);

            
          

            grid.Children.Add(wwvv);
            /***/
            //MyPanel.Children.Add(grid);

            ProgressRing ring = new ProgressRing() { IsActive = true, Foreground = new SolidColorBrush(Colors.Orange) };
            Binding ringBinding = new Binding { ElementName = "proxy", Path = new PropertyPath("ActualWidthValue"), Converter = Application.Current.Resources["MyRingRatioConverter"] as RingRatioConverter };
            ring.SetBinding(WidthProperty, ringBinding);
            ring.SetBinding(HeightProperty, ringBinding);
            wwvv = new WebView() { DefaultBackgroundColor = Colors.Gray };


            wwvv.ContentLoading += (a, b) =>
            {
                wwvv.Visibility = Visibility.Collapsed;
            };

            wwvv.FrameDOMContentLoaded += (a, b) =>
            {
                ring.IsActive = false;
                wwvv.Visibility = Visibility.Visible;
            };

            wwvv.DOMContentLoaded += async (a, b) =>
            {
                try
                {
                    string returnStr = await wwvv.InvokeScriptAsync("LoadYoutube", new string[] { "oXp2oE0xQcE" });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Script Error" + ex.ToString());
                }
            };

            youtuGrid.Children.Add(wwvv);
            youtuGrid.Children.Add(ring);

            wwvv.Navigate(new Uri("ms-appx-web:///Templates/youtube/youtube.html"));
        }

        WebView wwvv;

        private void StopVideo(object sender, RoutedEventArgs e)
        {
            if (wwvv != null)
            { 
}

            string script = @"player.stopVideo();";
            try
            {
                //string returnStr = await webview.InvokeScriptAsync("eval", new string[] { script });

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Script Running Error" + ex.ToString() + script);
            }
        }

        private void AddImgur_Click(object sender, RoutedEventArgs e)
        {
            //MyPanel.Children.Clear();

            WebView wwvv = new WebView() { Width = 800, Height = 800, DefaultBackgroundColor = Windows.UI.Colors.Gray };

            Grid grid = new Grid() { Width = 800, Height = 800 };

            grid.Children.Add(wwvv);

            //MyPanel.Children.Add(grid);

            wwvv.Navigate(new Uri("ms-appx-web:///Templates/imgur/imgur.html"));
        }
    }

    public struct YoutubeBorderInfo
    {
        public Double Width;
        public Double Height;
        public Thickness Margin;
        public HorizontalAlignment HorizontalAlignment;
        public VerticalAlignment VerticalAlignment;
    }
}
