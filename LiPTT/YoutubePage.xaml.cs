using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.UI.ViewManagement;

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

        private async void WebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
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

            YoutubeWebView.DOMContentLoaded += async (a, b) =>
            {
                string script = GetYoutubeScript("oXp2oE0xQcE", YoutubeWebView.ActualWidth, YoutubeWebView.ActualHeight);
                try
                {
                    string returnStr = await YoutubeWebView.InvokeScriptAsync("eval", new string[] { script });

                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Script Running Error" + ex.ToString() + script);
                }


                // YoutubeWebView.Visibility = Visibility.Visible;
            };

            YoutubeWebView.Navigate(new Uri("ms-appx-web:///Templates/youtube.html"));
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
