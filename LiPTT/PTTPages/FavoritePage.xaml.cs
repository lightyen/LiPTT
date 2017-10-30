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

// https://channel9.msdn.com/Series/Windows-Phone-8-1-Development-for-Absolute-Beginners/Part-28-Working-with-Animations-in-XAML

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class FavoritePage : Page
    {
        public FavoritePage()
        {
            InitializeComponent();
        }

        PTT ptt;

        private bool searching = false;

        List<string> ResultTable = new List<string>();

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ptt = Application.Current.Resources["PTT"] as PTT;
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

        private void BoardAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (searching) return;

                if (BoardAutoSuggestBox.Text.Length == 0)
                {
                    ResultTable.Clear();
                    BoardAutoSuggestBox.ItemsSource = null;
                    return;
                }

                if (!AcceptString(BoardAutoSuggestBox.Text)) return;

                searching = true;

                ResultTable.Clear();
                BoardAutoSuggestBox.ItemsSource = null;

                ptt.SearchBoardUpdated += Ptt_SearchBoardUpdated;
                ptt.SearchBoard(BoardAutoSuggestBox.Text);
            }
        }

        private async void Ptt_SearchBoardUpdated(object sender, SearchBoardUpdatedEventArgs e)
        {
            ptt.SearchBoardUpdated -= Ptt_SearchBoardUpdated;

            foreach (var s in e.Boards)
            {
                ResultTable.Add(s);
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                BoardAutoSuggestBox.ItemsSource = ResultTable;
            });

            searching = false;
        }

        private void BoardAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.QueryText == "") return;

            if (args.ChosenSuggestion is string chosen)
            {
                //ptt.GoToBoardCompleted += Ptt_EnterBoardCompleted;
                //ptt.GoToBoard(chosen);
            }
            else if (ResultTable.Count > 0 && ResultTable.First() == args.QueryText)
            {
                //ptt.GoToBoardCompleted += Ptt_EnterBoardCompleted;
                //ptt.GoToBoard(args.QueryText);
            }
            else if (ResultTable.Count > 0)
            {
                BoardAutoSuggestBox.Text = ResultTable.First();
            }
        }

    }
}
