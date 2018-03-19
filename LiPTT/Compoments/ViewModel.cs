using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;

namespace LiPTT
{
    public class PttPageViewModel : INotifyPropertyChanged
    {
        private string state;

        PTT ptt;

        public PttPageViewModel()
        {
            ptt = Application.Current.Resources["PTT"] as PTT;
            ptt.PTTStateUpdated += Ptt_PTTStateUpdated;

            CoreApplication.Resuming += (a, b) =>
            {
                ptt.PTTStateUpdated += Ptt_PTTStateUpdated;
                if (LiPTT.Logined)
                {
                    PTTState = "重新連線中...";
                }
            };
        }

        private void Ptt_PTTStateUpdated(object sender, PTTStateUpdatedEventArgs e)
        {
            switch (e.State)
            {
                case PttState.Disconnected:
                    PTTState = "未連線";
                    break;
                case PttState.Connecting:
                    PTTState = "連線中...";
                    break;
                case PttState.ConnectFailedTCP:
                    PTTState = "TCP 連線失敗";
                    break;
                case PttState.ConnectFailedWebSocket:
                    PTTState = "WebSocket 連線失敗";
                    break;
                case PttState.Board:
                    PTTState = "看板";
                    break;
                case PttState.SearchBoard:
                    PTTState = "搜尋看板";
                    break;
                case PttState.Disconnecting:
                    PTTState = "斷線中...";
                    break;
                case PttState.Login:
                    PTTState = "(請輸入帳號)";
                    break;
                case PttState.Password:
                    PTTState = "(請輸入密碼)";
                    break;
                case PttState.Loginning:
                    PTTState = "登入中...";
                    break;
                case PttState.Synchronizing:
                    PTTState = "更新與同步個人資訊中...";
                    break;
                case PttState.Article:
                    PTTState = "瀏覽文章";
                    break;
                case PttState.Accept:
                    PTTState = "密碼正確";
                    break;
                case PttState.AlreadyLogin:
                    PTTState = "有重複登入，踢掉中...";
                    break;
                case PttState.ArticleNotCompleted:
                    PTTState = "您有一篇文章尚未完成";
                    break;
                case PttState.OverLoading:
                    PTTState = "系統過載, 別擠阿";
                    break;
                case PttState.Maintenanced:
                    PTTState = "系統維護中";
                    break;
                case PttState.LoginSoMany:
                    PTTState = "登入太頻繁 請稍後在試";
                    break;
                case PttState.Kicked:
                    PTTState = "誰踢我";
                    break;
                case PttState.WrongPassword:
                    PTTState = "密碼不對或無此帳號";
                    break;
                case PttState.Angel:
                    PTTState = "小天使?";
                    break;
                case PttState.WrongLog:
                    PTTState = "要刪除登入錯誤資訊嗎?";
                    break;
                case PttState.MainPage:
                    PTTState = "主功能表";
                    break;
                case PttState.PressAny:
                    PTTState = "(請按任意鍵繼續...)";
                    break;
                case PttState.Exit:
                    PTTState = "確定要離開?";
                    break;
                default:
                    PTTState = "未定義狀態";
                    break;
            }
        }

        public string PTTState
        {
            get
            {
                return state;
            }
            set
            {
                state = value;
                var run = PTT.RunInUIThread(() => {
                    OnPropertyChanged(nameof(PTTState));
                });
            }
        }

        private void OnPropertyChanged([CallerMemberName] String propertyName = "")
        {
            var action = PTT.RunInUIThread(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
