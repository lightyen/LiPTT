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
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=234238

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainFunctionPage : Page
    {
        public MainFunctionPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            //LiPTT.PttEventEchoed -= MainFunction;
        }

        SemaphoreSlim sem = new SemaphoreSlim(0, 1);

        List<string> RelatedTable = new List<string>();

        private void MainFunction(PTTProvider sender, LiPttEventArgs e)
        {
            switch (e.State)
            {
                case PttState.SearchBoard:
                    break;
                case PttState.RelatedBoard:

                    break;
                default:
                    break;
            }
        }

        private void SearchBoard(PTTProvider sender, LiPttEventArgs e)
        {
            string[] arr = e.Screen.ToStringArray();

            if (e.State == PttState.SearchBoard)
            {
                var msg = e.Screen.ToString(1).Replace('\0', ' ');
                Regex regex = new Regex(@"([\w\S]+)");
                Match match = regex.Match(msg, 18);

                if (match.Success)
                {
                    string suggestion = msg.Substring(match.Index, match.Length);

                    var action = LiPTT.RunInUIThread(() =>
                    {
                        if (BoardAutoSuggestBox.Text.Length <= suggestion.Length) RelatedTable.Add(suggestion);
                    });
                    sem.Release();
                }
            }
            else if (e.State == PttState.RelatedBoard)
            {
                

                Regex regex = new Regex(@"([\w\S]+)");
                Match match;

                for (int i = 3; i < 23; i++)
                {
                    string k = arr[i].Replace('\0', ' ');

                    var action = LiPTT.RunInUIThread(() =>
                    {
                        match = regex.Match(k, 0);
                        if (match.Success) RelatedTable.Add(k.Substring(match.Index, match.Length));

                        match = regex.Match(k, 22);
                        if (match.Success) RelatedTable.Add(k.Substring(match.Index, match.Length));

                        match = regex.Match(k, 44);
                        if (match.Success) RelatedTable.Add(k.Substring(match.Index, match.Length));
                    });
                    
                }

                if (new Regex("按空白鍵可列出更多項目").Match(arr[23]).Success)
                {
                    LiPTT.PressSpace();
                }
                else
                {
                    var action = LiPTT.RunInUIThread(() =>
                    {
                        RelatedTable.Sort();
                    });

                    sem.Release();
                }
            }

            
        }

        private async void BoardAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                RelatedTable.Clear();
                BoardAutoSuggestBox.ItemsSource = null;

                if (BoardAutoSuggestBox.Text.Length > 0)
                {
                    ///////////////////////////

                    LiPTT.PttEventEchoed += SearchBoard;
                    LiPTT.SendMessage('s', BoardAutoSuggestBox.Text, 0x20);

                    await sem.WaitAsync();
                    BoardAutoSuggestBox.ItemsSource = RelatedTable;

                    LiPTT.PttEventEchoed -= SearchBoard;

                    ///////////////////////////
                    string lastText = "";
                    var msg = LiPTT.Current.Screen.ToString(1).Replace('\0', ' ');
                    Regex regex = new Regex(@"([\w\S]+)");
                    Match match = regex.Match(msg, 18);
                    if (match.Success)
                    {
                        lastText = msg.Substring(match.Index, match.Length);
                    }

                    byte[] back = new byte[lastText.Length + 1];
                    back[lastText.Length] = 0x0D;
                    for (int i = 0; i < lastText.Length; i++) back[i] = 0x08;
                    LiPTT.SendMessage(back);
                }
            }
        }

        private void BoardAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            

            if (args.ChosenSuggestion is string chosen)
            {
                LiPTT.PttEventEchoed += GoToBoard;
                LiPTT.SendMessage('s', chosen, 0x0D);
            }
            else if (RelatedTable?.Count > 0 && RelatedTable?.First() == args.QueryText)
            {
                LiPTT.PttEventEchoed += GoToBoard;
                LiPTT.SendMessage('s', args.QueryText, 0x0D);
            }
            else
            {
                BoardAutoSuggestBox.Text = RelatedTable?.First();
            }
        }

        private bool pressAny = false;

        private void GoToBoard(PTTProvider sender, LiPttEventArgs e)
        {
            switch (e.State)
            {
                case PttState.PressAny:
                    if (!pressAny)
                    {
                        pressAny = true;
                        LiPTT.SendMessage(0x20, 0x24);
                    }
                    
                    break;
                case PttState.Board:
                    LiPTT.PttEventEchoed -= GoToBoard; 
                    var t = LiPTT.RunInUIThread(() => {
                        LiPTT.Frame.Navigate(typeof(BoardPage));
                    });
                    break;
            }
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string s)
            {
                if (s == "八卦")
                {
                    LiPTT.PttEventEchoed += GoToBoard;
                    LiPTT.SendMessage('s', "Gossiping", 0x0D);
                }
                else if (s == "表特")
                {
                    LiPTT.PttEventEchoed += GoToBoard;
                    LiPTT.SendMessage('s', "Beauty", 0x0D);
                }
                else if (s == "LoL")
                {
                    LiPTT.PttEventEchoed += GoToBoard;
                    LiPTT.SendMessage('s', "LoL", 0x0D);
                }
                else if (s == "電蝦")
                {
                    LiPTT.PttEventEchoed += GoToBoard;
                    LiPTT.SendMessage('s', "PC_Shopping", 0x0D);
                }
                else if (s == "少女前線")
                {
                    LiPTT.PttEventEchoed += GoToBoard;
                    LiPTT.SendMessage('s', "GirlsFront", 0x0D);
                }
                else if (s == "C# 程式設計")
                {
                    LiPTT.PttEventEchoed += GoToBoard;
                    LiPTT.SendMessage('s', "C_Sharp", 0x0D);
                }
            }
        }


        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            LiPTT.TestConnectionTimer.Stop();
            LiPTT.Current.IsExit = true;
            LiPTT.SendMessage('g', 0x0D, 'y', 0x0D, 0x20);
            LiPTT.PressAnyKey();

            LiPTT.ArticleCollection = null;
            LiPTT.CurrentArticle = null;
            LiPTT.CurrentWebView = null;

            LiPTT.Frame.Navigate(typeof(LoginPage));
        }
    }
}
