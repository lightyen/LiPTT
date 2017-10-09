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

        public LoginPage()
        {
            this.InitializeComponent();
            Application.Current.Suspending += (a, b) => { SaveUserAccount(LiPTT.UserName, LiPTT.Password); };
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
            else if (LiPTT.AlwaysAlive && LiPTT.State == PttState.Kicked || LiPTT.State == PttState.LoginSoMany)
            {
                DelayLogin(TimeSpan.FromSeconds(3));
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
            UserText.IsEnabled = false;
            PasswordText.IsEnabled = false;
            MemoAcount.IsEnabled = false;
            AutoLogin.IsEnabled = false;

            Windows.System.Threading.ThreadPoolTimer.CreateTimer((source) => {

                var ac = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
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

        private void Enter()
        {
            start = false;
            LiPTT.IsExit = false;
            SaveUserAccount(UserText.Text, PasswordText.Password);
            enterAlreadyLogin = enterWrongLog = enteruser = enterpswd = false;
            LiPTT.PttEventEchoed += EnterAccount;
            LiPTT.TryConnect();
        }

        private void UserText_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && UserText.Text.Length >= 0)
            {
                e.Handled = true;
                FocusManager.TryMoveFocus(FocusNavigationDirection.Next);
            }
        }

        private bool enteruser;
        private bool enterpswd;
        private bool enterWrongLog;
        private bool enterAlreadyLogin;
    
        private void EnterAccount(PTTClient sender, LiPttEventArgs e)
        {
            switch (e.State)
            {
                case PttState.Login:
                    if (!enteruser)
                    {
                        enteruser = true;
                        var action = LiPTT.RunInUIThread(() =>
                        {
                            LiPTT.UserName = UserText.Text;
                            LiPTT.EnterUserName();
                        });
                    }
                    break;
                case PttState.Password:
                    if (!enterpswd)
                    {
                        enterpswd = true;
                        var action = LiPTT.RunInUIThread(() =>
                        {
                            LiPTT.Password = PasswordText.Password;
                            LiPTT.EnterPassword();
                        });            
                    }
                    
                    break;
                case PttState.WrongPassword:
                    {
                        LiPTT.Client.Dispose();

                        var action = LiPTT.RunInUIThread(() => {
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
                        LiPTT.Yes();
                    }
                    break;
                case PttState.WrongLog:
                    if (!enterWrongLog)
                    {
                        enterWrongLog = true;
                        LiPTT.Yes();
                    }                    
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
                        LiPTT.Client.Dispose();

                        var action = LiPTT.RunInUIThread(() => {
                            UserText.IsEnabled = true;
                            PasswordText.IsEnabled = true;
                            MemoAcount.IsEnabled = true;
                            AutoLogin.IsEnabled = true;
                        });

                        DelayLogin(TimeSpan.FromSeconds(1));
                    }
                    break;
                case PttState.OverLoading:
                    {
                        LiPTT.Client.Dispose();

                        var action = LiPTT.RunInUIThread(() => {
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
                    LiPTT.PressAnyKey();
                    break;
                case PttState.MainPage:
                    LiPTT.PttEventEchoed -= EnterAccount;
                    { 
                        var action = LiPTT.RunInUIThread(() =>
                        {
                            LiPTT.Frame.Navigate(typeof(PTTPage));
                        });

                        action.AsTask().Wait();
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
                        LiPTT.UserName = (string)container["username"];
                        LiPTT.Password = (string)container["password"];

                        if (LiPTT.UserName != null)
                        {
                            UserText.Text = LiPTT.UserName;
                        }

                        if (LiPTT.Password != null)
                        {
                            PasswordText.Password = LiPTT.Password;
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
