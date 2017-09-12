using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;

namespace LiPTT
{
    public class PTTProvider : PTTClient
    {
        public SemaphoreSlim ScreenSemaphore;

        public PTTProvider()
        {
            ScreenSemaphore = new SemaphoreSlim(0, 1);
            ScreenUpdated += (o, e) =>
            {
                if (ScreenSemaphore.CurrentCount == 0) ScreenSemaphore.Release();
            };
        }

        public bool IsConnected
        {
            get
            {
                return IsConnectionAlright();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pattern">關鍵字</param>
        /// <param name="row">從0開始</param>
        /// <returns></returns>
        public bool MatchPattern(string pattern, int row)
        {
            Regex re = new Regex(pattern);

            bool ans = re.Match(Screen.ToString(row)).Success;
            //if (ans) Debug.WriteLine(string.Format("[{0}]<==>;{1};", pattern, Screen.ToString(row)));
            return ans;
        }

        public bool MatchPattern(string pattern, int row, out string first)
        {
            Regex re = new Regex(pattern);
            string line = Screen.ToString(row);
            Match match = re.Match(line);
            bool ans = match.Success;

            if (ans)
            {
                first = line.Substring(match.Index, match.Length);
            }
            else
            {
                first = "";
            }
            
            //if (ans) Debug.WriteLine(string.Format("[{0}]<==>;{1};", pattern, Screen.ToString(row)));
            return ans;
        }

        public bool MatchCurrentLine(string pattern)
        {
            Regex re = new Regex(pattern);
            bool ans = re.Match(Screen.ToString(Screen.CurrentY - 1)).Success;
            if (ans) Debug.WriteLine(string.Format("[{0}]<==>;{1};", pattern, Screen.ToString(Screen.CurrentY - 1)));
            return ans;
        }

        private string ReadLine(int row)
        {
            if (row < 1 || row > Screen.Height) return "";
            byte[] msg = new byte[Screen.Width];
            for (int j = 0; j < Screen.Width; j++) msg[j] = Screen[row - 1][j].Content;
            return LiPTT_Encoding.GetEncoding().GetString(msg);
        }
    }

}
