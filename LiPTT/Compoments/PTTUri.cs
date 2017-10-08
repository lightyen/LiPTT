using System;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.System;
using Windows.UI.Xaml;
using System.Net;
using System.IO;
using Windows.Web.Http;

namespace LiPTT
{
    class PTTUri : Uri
    {
        const string urlshortener = "https://www.googleapis.com/urlshortener/v1/url";
        private string twitchID;
        private string youtubeID;
        
        public PTTUri(string uriString) : base(uriString)
        {
            twitchID = youtubeID = "";
            GetShortUri();
            GetYoutube();
            GetTwitch();
            GetPictureUri();
        }

        public bool IsYoutube
        {
            get
            {
                if (youtubeID.Length > 0) return true;
                else return false;
            }
        }

        public bool IsTwitch
        {
            get
            {
                if (twitchID.Length > 0) return true;
                else return false;
            }
        }

        public bool IsPicture
        {
            get
            {
                if (PictureUri != null) return true;
                else return false;
            }
        }

        public bool IsShort
        {
            get; private set;
        }

        public Uri PictureUri
        {
            get; private set;
        }

        public string TwitchID
        {
            get
            {
                if (twitchID.Length > 0) return twitchID;
                else return "";
            }
        }

        public string YoutubeID
        {
            get
            {
                if (youtubeID.Length > 0) return youtubeID;
                else return "";
            }
        }

        public bool IsMediaUri
        {
            get
            {
                return IsPicture || IsYoutube || IsTwitch;
            }
        }

        public bool IsUriVisible
        {
            get
            {
                if (IsMediaUri)
                {
                    if (IsPicture) return false;
                    if (IsYoutube) return false;
                }
                return true;
            }
        }

        public PTTUri Expand()
        {
            if (!IsShort) return this;

            SettingProperty setting = Application.Current.Resources["SettingProperty"] as SettingProperty;

            if (Host == "goo.gl" && setting.GoogleURLShortenerAPIKey.Length > 0)
            {
                HttpClient httpClient = new HttpClient();
                var headers = httpClient.DefaultRequestHeaders;

                string r = string.Format("{0}?key={1}&shortUrl={2}", urlshortener, setting.GoogleURLShortenerAPIKey, OriginalString);

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
                                return new PTTUri(match.Groups["longUrl"].Value);
                            }
                            else
                                return this;
                        }
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("Timeout: {0}", OriginalString));
                        return ExpandUriWithUntiny();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(string.Format("Exception: {0} - {1}", OriginalString, e.Message));
                    return ExpandUriWithUntiny();
                }
            }
            else
            {
                return ExpandUriWithUntiny();
            }
        }

        private PTTUri Shorten()
        {
            if (IsYoutube)
                return new PTTUri("https://youtu.be/" + youtubeID);

            var post = string.Format(@"{{""longUrl"": ""{0}""}}", OriginalString);
            const string MATCH_PATTERN = @"""id"": ?""(?<id>.+)""";
            SettingProperty setting = Application.Current.Resources["SettingProperty"] as SettingProperty;

            try
            {
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
                        return new PTTUri(url);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("Exception: {0} - {1}", OriginalString, e.Message));
            }

            return null;
        }

        private void GetPictureUri()
        {
            string origin = OriginalString;

            if (origin.EndsWith(".jpg") ||
                origin.EndsWith(".png") ||
                origin.EndsWith(".png") ||
                origin.EndsWith(".gif") ||
                origin.EndsWith(".bmp") ||
                origin.EndsWith(".tiff") ||
                origin.EndsWith(".ico"))
            {
                PictureUri = this;
            }
            else if (Host == "imgur.com")
            {
                if (OriginalString.IndexOf("imgur.com/a/") == -1)
                {
                    Match match = new Regex("imgur.com/").Match(OriginalString);

                    if (match.Success)
                    {
                        string ID = OriginalString.Substring(match.Index + match.Length);

                        if (ID != "")
                        {
                            PictureUri = new Uri("http://i.imgur.com/" + ID + ".png");
                        }
                    }
                }
            }
            else if (Host == "i.imgur.com")
            {
                string str = OriginalString;

                if (str.IndexOf("i.imgur.com/a/") == -1)
                {
                    Match match = new Regex("i.imgur.com/").Match(str);

                    if (match.Success)
                    {
                        string ID = OriginalString.Substring(match.Index + match.Length);
                        if (ID != "")
                        {
                            PictureUri = new Uri("http://i.imgur.com/" + ID + ".png");
                        }
                    }
                }
            }
        }

        private void GetYoutube()
        {
            if (Host == "youtu.be")
            {
                youtubeID = LocalPath.Substring(1);
            }
            else if (Host == "youtube.com" || Host == "www.youtube.com" || Host == "m.youtube.com" || Host == "tw.youtube.com")
            {
                string[] query = Query.Split(new char[] { '?', '&' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string s in query)
                {
                    if (s.StartsWith("v"))
                    {
                        youtubeID = s.Substring(s.IndexOf("=") + 1);
                        break;
                    }
                }
            }
        }

        private void GetTwitch()
        {
            if (Host == "twitch.tv" || Host == "www.twitch.tv" || Host == "go.twitch.tv")
            {
                twitchID = LocalPath.Substring(1);
            }
        }

        private void GetShortUri()
        {
            switch (Host)
            {
                case "goo.gl":
                case "bit.ly":
                case "tinyurl.com":
                case "redd.it":
                    IsShort = true;
                    break;
                default:
                    IsShort =  false;
                    break;
            }
        }

        private PTTUri ExpandUriWithUntiny()
        {
            //http://untiny.com/api/
            string c = string.Format("http://untiny.com/api/1.0/extract/?url={0}&format=text", OriginalString);

            try
            {
                WebRequest webRequest = WebRequest.Create(c);
                Task<WebResponse> t = webRequest.GetResponseAsync();
                if (t.Wait(3000))
                {
                    using (StreamReader sr = new StreamReader(t.Result.GetResponseStream()))
                    {
                        string url = sr.ReadToEnd();
                        return new PTTUri(url);
                    }
                }
                else
                {
                    Debug.WriteLine(string.Format("Timeout: {0}", OriginalString));
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("Exception: {0} - {1}", OriginalString, e.Message));
            }

            return this;
        }
    }
}
