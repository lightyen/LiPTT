﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.System;
using Windows.UI.Xaml;
using System.Net;
using System.IO;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

namespace LiPTT
{
    public partial class ArticleContentCollection
    {
        const string urlshortener = @"https://www.googleapis.com/urlshortener/v1/url";

        private Uri ShortenUri(Uri uri)
        {
            if (uri == null) return null;

            if (new Regex("youtube").Match(uri.Host).Success)
                return ShortenYoutubeUri(uri);

            var post = string.Format(@"{{""longUrl"": ""{0}""}}", uri.OriginalString);
            const string MATCH_PATTERN = @"""id"": ?""(?<id>.+)""";

            SettingProperty setting = Application.Current.Resources["SettingProperty"] as SettingProperty;

            var request_uri = new Uri(string.Format("{0}?key={1}", urlshortener, setting.GoogleURLShortenerAPIKey));

            WebRequest webRequest = WebRequest.Create(request_uri);
            webRequest.Method = "POST";
            webRequest.ContentType = "application/json";
            webRequest.Headers[HttpRequestHeader.CacheControl] = "no-cache";

            using (Stream requestStream = webRequest.GetRequestStreamAsync().Result)
            {
                var buffer = Encoding.ASCII.GetBytes(post);
                requestStream.Write(buffer, 0, buffer.Length);
            }

            using (WebResponse response = webRequest.GetResponseAsync().Result)
            using (StreamReader sr = new StreamReader(response.GetResponseStream()))
            {
                string content = sr.ReadToEnd();

                Match match = new Regex(MATCH_PATTERN).Match(content);

                if (match.Success)
                {
                    string url = match.Groups["id"].Value;
                    return new Uri(url);
                }
            }
            return null;
        }

        private Uri ExpandUri(Uri uri)
        {
            if (uri == null) return null;

            if (uri.Host == "youtu.be")
                return new Uri("https://www.youtube.com/watch?v=" + uri.LocalPath);

            SettingProperty setting = Application.Current.Resources["SettingProperty"] as SettingProperty;

            if (uri.Host == "goo.gl" && setting.GoogleURLShortenerAPIKey.Length > 0)
            {
                HttpClient httpClient = new HttpClient();
                var headers = httpClient.DefaultRequestHeaders;

                //https://dotblogs.com.tw/larrynung/2011/08/03/32506
                string r = string.Format("{0}?key={1}&shortUrl={2}", urlshortener, setting.GoogleURLShortenerAPIKey, uri.OriginalString);

                try
                {
                    WebRequest webRequest = WebRequest.Create(r);
                    Task<WebResponse> t = webRequest.GetResponseAsync();
                    if (t.Wait(3000))
                    {
                        using (StreamReader sr = new StreamReader(t.Result.GetResponseStream()))
                        {
                            string code = sr.ReadToEnd();
                            const string MATCH_PATTERN = @"""longUrl"": ?""(?<longUrl>.+)""";
                            Match match = new Regex(MATCH_PATTERN).Match(code);
                            if (match.Success)
                            {
                                return new Uri(match.Groups["longUrl"].Value);
                            }
                            else
                                return null;
                        }
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("Timeout: {0}", uri.OriginalString));
                        return null;
                    }
                }
                catch (Exception)
                {
                    return ExpandUriWithUntiny(uri);
                }
                
            }
            else
            {
                return ExpandUriWithUntiny(uri);
            }
        }

        private Uri ExpandUriWithUntiny(Uri uri)
        {
            //http://untiny.com/api/
            string c = string.Format("http://untiny.com/api/1.0/extract/?url={0}&format=text", uri.OriginalString);
            WebRequest webRequest = WebRequest.Create(c);
            Task<WebResponse> t = webRequest.GetResponseAsync();
            if (t.Wait(3000))
            {
                using (StreamReader sr = new StreamReader(t.Result.GetResponseStream()))
                {
                    string url = sr.ReadToEnd();
                    return new Uri(url);
                }
            }
            else
            {
                Debug.WriteLine(string.Format("Timeout: {0}", uri.OriginalString));
                return null;
            }
        }

        private Uri ShortenYoutubeUri(Uri uri)
        {
            if (uri == null)
                return null;

            string[] query = uri.Query.Split(new char[] { '?', '&' }, StringSplitOptions.RemoveEmptyEntries);
            string youtubeID = "";

            foreach (string s in query)
            {
                if (s.StartsWith("v"))
                {
                    youtubeID = s.Substring(s.IndexOf("=") + 1);
                    break;
                }
            }

            if (youtubeID.Length > 0)
                return new Uri("https://youtu.be/" + youtubeID);
            else
                return null;
        }

        private bool IsPictureUri(Uri uri)
        {
            string origin = uri.OriginalString;
            if (origin.EndsWith(".jpg") ||
                origin.EndsWith(".png") ||
                origin.EndsWith(".png") ||
                origin.EndsWith(".gif") ||
                origin.EndsWith(".bmp") ||
                origin.EndsWith(".tiff") ||
                origin.EndsWith(".ico"))
            {
                return true;
            }

            return false;
        }

        private bool IsYoutubeUri(Uri uri)
        {
            if (uri.Host == "www.youtube.com" || uri.Host == "m.youtube.com")
            {
                string[] query = uri.Query.Split(new char[] { '?', '&' }, StringSplitOptions.RemoveEmptyEntries);
                string youtubeID = "";
                foreach (string s in query)
                {
                    if (s.StartsWith("v"))
                    {
                        youtubeID = s.Substring(s.IndexOf("=") + 1);
                        break;
                    }
                }

                if (youtubeID.Length > 0) return true;
                else return false;
            }
            else
                return false;
        }

        private bool IsTwitchUri(Uri uri)
        {
            if (uri.Host == "www.twitch.tv" || uri.Host == "go.twitch.tv")
            {
                string twitchID = uri.LocalPath.Substring(1);
                if (twitchID.Length > 0)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        private bool IsShortUri(Uri uri)
        {
            SettingProperty setting = Application.Current.Resources["SettingProperty"] as SettingProperty;

            switch (uri.Host)
            {
                case "youtu.be":
                    return true;
                case "goo.gl":
                case "bit.ly":
                case "tinyurl.com":
                case "redd.it":
                    if (setting.OpenShortUri)
                        return true;
                    return false;
                default:
                    return false;
            }
        }

    }
}
