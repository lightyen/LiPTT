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
using Windows.System;
using Windows.Storage;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace LiPTT
{
    /// <summary>
    /// 登入頁面。
    /// </summary>
    public sealed partial class LoginPage : Page
    {
        bool resume = false;

        PTT ptt;

        public LoginPage()
        {
            InitializeComponent();

            ptt = Application.Current.Resources["PTT"] as PTT;

            Application.Current.Suspending += (a, b) => { SaveUserAccount(ptt.User, ptt.Password); };
            Application.Current.Resuming += (a, b) => 
            {
                resume = true;
                LiPTT.Frame.Navigate(typeof(LoginPage));
            };
        }

        private const string AccountTableKey = "AccountTable";

        private bool start = true;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (LoadUserAccount() == false)
            {
                DefaultUserAccount();
            }

            if (start && AutoLogin.IsChecked == true)
            {
                Enter();
                UserText.IsEnabled = false;
                PasswordText.IsEnabled = false;
                MemoAcount.IsEnabled = false;
                AutoLogin.IsEnabled = false;
            }
            else if (LiPTT.AlwaysAlive && ptt.IsKicked)
            {
                if (Resources["ViewModel"] is PttPageViewModel viewmodel)
                {
                    viewmodel.State = "被踢了，重新連線中...";
                }

                DelayLogin(TimeSpan.FromMilliseconds(4500));
            }
            else if (resume)
            {
                resume = false;
                if (LiPTT.Logined)
                {
                    DelayLogin(TimeSpan.FromSeconds(3));
                }
            }
            else
            {
                UserText.IsEnabled = true;
                PasswordText.IsEnabled = true;
                MemoAcount.IsEnabled = true;
                if (MemoAcount.IsChecked == true)
                    AutoLogin.IsEnabled = true;
                else
                    AutoLogin.IsEnabled = false;
            }

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        private void DelayLogin(TimeSpan delay)
        {
            Windows.System.Threading.ThreadPoolTimer.CreateTimer((source) => {
                var ac = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    UserText.IsEnabled = false;
                    PasswordText.IsEnabled = false;
                    MemoAcount.IsEnabled = false;
                    AutoLogin.IsEnabled = false;
                    Enter();
                });
            }, delay);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
                
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Escape)
            {
                //微軟說不建議主動關閉應用程式......好ㄅ
                //Application.Current.Exit();
            }
        }

        private void PasswordText_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && UserText.Text.Length >= 0)
            {
                var action = LiPTT.RunInUIThread(() =>
                {
                    UserText.IsEnabled = false;
                    PasswordText.IsEnabled = false;
                    MemoAcount.IsEnabled = false;
                    AutoLogin.IsEnabled = false;
                });


                e.Handled = true;
                FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
                FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
                FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
                Enter();
            }
        }

        private void UserText_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && UserText.Text.Length >= 0)
            {
                e.Handled = true;
                FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
            }
        }

        private void Enter()
        {
            start = false;
            ptt.IsAppExit = false;
            SaveUserAccount(UserText.Text, PasswordText.Password);
            enterAlreadyLogin = false;
            ptt.PTTStateUpdated += EnterAccount;
            ptt.Login(UserText.Text, PasswordText.Password);
        }

        private bool enterAlreadyLogin;
    
        private async void EnterAccount(object sender, PTTStateUpdatedEventArgs e)
        {
            switch (e.State)
            {
                case PttState.WrongPassword:
                    {
                        await LiPTT.RunInUIThread(() => {
                            UserText.IsEnabled = true;
                            PasswordText.IsEnabled = true;
                            MemoAcount.IsEnabled = true;
                            AutoLogin.IsEnabled = true;
                        });
                    }
                    break;
                case PttState.AlreadyLogin:
                    if (!enterAlreadyLogin)
                    {
                        enterAlreadyLogin = true;
                        ptt.Yes();
                    }
                    break;
                case PttState.WrongLog:
                    ptt.Yes();
                    break;
                case PttState.ArticleNotCompleted:
                    ptt.SendQ();
                    break;
                case PttState.Accept:
                    {
                        LiPTT.Logined = true;
                    }
                    break;
                case PttState.Loginning:
                    break;
                case PttState.Synchronizing:
                    break;
                case PttState.LoginSoMany:
                    {
                        await LiPTT.RunInUIThread(() => {
                            UserText.IsEnabled = true;
                            PasswordText.IsEnabled = true;
                            MemoAcount.IsEnabled = true;
                            AutoLogin.IsEnabled = true;
                        });
                    }
                    break;
                case PttState.OverLoading:
                    {
                        await LiPTT.RunInUIThread(() => {
                            UserText.IsEnabled = true;
                            PasswordText.IsEnabled = true;
                            MemoAcount.IsEnabled = true;
                            AutoLogin.IsEnabled = true;
                        });
                    }
                    break;
                case PttState.ConnectFailedTCP:
                case PttState.ConnectFailedWebSocket:
                    {
                        var action = LiPTT.RunInUIThread(() => {
                            UserText.IsEnabled = true;
                            PasswordText.IsEnabled = true;
                            MemoAcount.IsEnabled = true;
                            AutoLogin.IsEnabled = true;
                        });
                    }
                    break;
                case PttState.PressAny:
                    ptt.PressAnyKey();
                    break;
                case PttState.MainPage:
                    ptt.PTTStateUpdated -= EnterAccount;
                    { 
                        await LiPTT.RunInUIThread(() =>
                        {
                            LiPTT.Frame.Navigate(typeof(PTTPage));
                        });
                    }
                    break;
            }
        }

        private void SaveUserAccount(string user, string password)
        {
            var container = ApplicationData.Current.RoamingSettings.CreateContainer(AccountTableKey, ApplicationDataCreateDisposition.Always);

            if (container != null)
            {
                bool? memo = MemoAcount.IsChecked;
                if (memo == true)
                {
                    if (user != null)
                    {
                        container.Values["username"] = user;
                    }
                    else
                    {
                        container.Values["username"] = "";
                    }

                    if (password != null)
                    {
                        container.Values["password"] = password;
                    }
                    else
                    {
                        container.Values["password"] = "";
                    }
                    
                    container.Values["remember"] = true;
                    container.Values["autoLogin"] = AutoLogin.IsChecked;
                }
                else
                {
                    container.Values["username"] = "";
                    container.Values["password"] = "";
                    container.Values["remember"] = false;
                    container.Values["autoLogin"] = false;
                }
            }
        }

        private bool LoadUserAccount()
        {
            try
            {
                var container = ApplicationData.Current.RoamingSettings.Containers[AccountTableKey].Values;

                if (container != null)
                {
                    bool memo = (bool)container["remember"];
                    MemoAcount.IsChecked = memo;
                    if (memo)
                    {
                        ptt.User = (string)container["username"];
                        ptt.Password = (string)container["password"];

                        if (ptt.User != null)
                        {
                            UserText.Text = ptt.User;
                        }

                        if (ptt.Password != null)
                        {
                            PasswordText.Password = ptt.Password;
                        }

                        bool auto = (bool)container["autoLogin"];
                        AutoLogin.IsChecked = auto;
                    }
                    

                    return true;
                }
                else return false;
            
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private void DefaultUserAccount()
        {
            var container = ApplicationData.Current.RoamingSettings.CreateContainer(AccountTableKey, ApplicationDataCreateDisposition.Always);

            if (container != null)
            {
                container.Values["username"] = "";
                container.Values["password"] = "";
                container.Values["remember"] = false;
                container.Values["autoLogin"] = false;
            }
        }

        private void MemoAcount_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
                FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
                Enter();
            }
        }

        private void MemoAcount_Checked(object sender, RoutedEventArgs e)
        {
            AutoLogin.IsEnabled = true;
        }

        private void MemoAcount_Unchecked(object sender, RoutedEventArgs e)
        {
            AutoLogin.IsChecked = false;
            AutoLogin.IsEnabled = false;
        }
    }
}
