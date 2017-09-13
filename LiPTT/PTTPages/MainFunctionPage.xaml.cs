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

        List<string> RelatedTable = new List<string>();

        private bool searching = false;

        private void BoardAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (searching) return;

                if (BoardAutoSuggestBox.Text.Length == 0)
                {
                    RelatedTable.Clear();
                    BoardAutoSuggestBox.ItemsSource = null;
                    return;
                }

                if (!AcceptString(BoardAutoSuggestBox.Text)) return;

                searching = true;

                RelatedTable.Clear();
                BoardAutoSuggestBox.ItemsSource = null;

                LiPTT.PttEventEchoed += SearchBoard;
                LiPTT.SendMessage('s', BoardAutoSuggestBox.Text, 0x20);
            }
        }

        private void SearchBoard(PTTProvider sender, LiPttEventArgs e)
        {
            Regex regex = new Regex(@"([\w-_]+)");
            Match match;

            switch (e.State)
            {
                case PttState.SearchBoard:
                    LiPTT.PttEventEchoed -= SearchBoard;
                    var msg = e.Screen.ToString(1, 34, 20).Trim();

                    match = regex.Match(msg);

                    if (match.Success)
                    {
                        string suggestion = msg.Substring(match.Index, match.Length);

                        var action = LiPTT.RunInUIThread(() =>
                        {
                            if (BoardAutoSuggestBox.Text.Length <= suggestion.Length) RelatedTable.Add(suggestion);
                        });

                         
                    }

                    ClearSearch();

                    var a = LiPTT.RunInUIThread(() =>
                    {
                        BoardAutoSuggestBox.ItemsSource = RelatedTable;
                    });

                    break;
                case PttState.RelatedBoard:

                    for (int i = 3; i < 23; i++)
                    {
                        string k = e.Screen.ToString(i).Replace('\0', ' ');

                        match = regex.Match(k, 0);
                        if (match.Success) RelatedTable.Add(k.Substring(match.Index, match.Length));

                        match = regex.Match(k, 22);
                        if (match.Success) RelatedTable.Add(k.Substring(match.Index, match.Length));

                        match = regex.Match(k, 44);
                        if (match.Success) RelatedTable.Add(k.Substring(match.Index, match.Length));

                    }

                    if (new Regex("按空白鍵可列出更多項目").Match(e.Screen.ToString(23)).Success)
                    {
                        LiPTT.PressSpace();
                    }
                    else
                    {
                        LiPTT.PttEventEchoed -= SearchBoard;

                        RelatedTable.Sort();    
                        ClearSearch();

                        var t = LiPTT.RunInUIThread(() => 
                        {
                            BoardAutoSuggestBox.ItemsSource = RelatedTable;
                        }); 
                    }
                    break;
            }
        }
       
        private bool AcceptString(string s)
        {
            foreach (char c in s)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c == '-' || c <= '_')))
                {
                    return false;
                }
            }

            return true;
        }

        private void ClearSearch()
        {
            string lastText = "";
            var msg = LiPTT.Current.Screen.ToString(1, 34, 20).Trim();
            Regex regex = new Regex(@"([\w-_]+)");
            Match match = regex.Match(msg);
            if (match.Success)
            {
                lastText = msg.Substring(match.Index, match.Length);
            }
            else return;

            byte[] back = new byte[lastText.Length + 1];
            back[lastText.Length] = 0x0D;
            for (int i = 0; i < lastText.Length; i++) back[i] = 0x08;
            LiPTT.SendMessage(back);

            searching = false;
        }

        private void BoardAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.QueryText == "") return;

            if (args.ChosenSuggestion is string chosen)
            {
                LiPTT.PttEventEchoed += GoToBoard;
                LiPTT.SendMessage('s', chosen, 0x0D);
            }
            else if (RelatedTable.Count > 0 && RelatedTable.First() == args.QueryText)
            {
                LiPTT.PttEventEchoed += GoToBoard;
                LiPTT.SendMessage('s', args.QueryText, 0x0D);
            }
            else if (RelatedTable.Count > 0)
            {
                BoardAutoSuggestBox.Text = RelatedTable.First();
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
