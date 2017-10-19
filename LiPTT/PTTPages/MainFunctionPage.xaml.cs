using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.System;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
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
using System.ComponentModel;
using System.Runtime.CompilerServices;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=234238

namespace LiPTT
{
    public sealed partial class MainFunctionPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        PTT ptt;

        public MainFunctionPage()
        {
            this.DataContext = this;

            this.InitializeComponent();

            ptt = Application.Current.Resources["PTT"] as PTT;

            BoardGridView.Items.Add(new MyKeyValuePair("Windows", "Windows"));
            BoardGridView.Items.Add(new MyKeyValuePair("笨蛋", "StupidClown"));
            BoardGridView.Items.Add(new MyKeyValuePair("Soft Job", "Soft_Job"));
            BoardGridView.Items.Add(new MyKeyValuePair("八卦", "Gossiping"));
            BoardGridView.Items.Add(new MyKeyValuePair("LOL", "LoL"));
            BoardGridView.Items.Add(new MyKeyValuePair("表特", "Beauty"));
            BoardGridView.Items.Add(new MyKeyValuePair("電蝦", "PC_Shopping"));
            BoardGridView.Items.Add(new MyKeyValuePair("少女前線", "GirlsFront"));
            BoardGridView.Items.Add(new MyKeyValuePair("C# 程式設計", "C_Sharp"));

            ControlVisible = Visibility.Visible;
        }

        List<string> RelatedTable = new List<string>();

        private bool searching = false;

        private bool control_visible;

        public Visibility ControlVisible
        {
            get
            {
                if (control_visible)
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
            set
            {
                if ((control_visible && value == Visibility.Collapsed) || (!control_visible && value == Visibility.Visible))
                {
                    if (value == Visibility.Visible)
                        control_visible = true;
                    else
                        control_visible = false;
                    NotifyPropertyChanged("ControlVisible");
                    NotifyPropertyChanged("RingActive");
                }
            }
        }

        public bool RingActive
        {
            get
            {
                return !control_visible;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ItemClick = false;
            ControlVisible = Visibility.Visible;
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
                    RelatedTable.Clear();
                    BoardAutoSuggestBox.ItemsSource = null;
                    return;
                }

                if (!AcceptString(BoardAutoSuggestBox.Text)) return;

                searching = true;

                RelatedTable.Clear();
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
                RelatedTable.Add(s);
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                BoardAutoSuggestBox.ItemsSource = RelatedTable;
            });
            

            searching = false;
        }



        private void BoardAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.QueryText == "") return;

            if (args.ChosenSuggestion is string chosen)
            {
                ptt.GoToBoardCompleted += Ptt_EnterBoardCompleted;
                ptt.GoToBoard(chosen);
            }
            else if (RelatedTable.Count > 0 && RelatedTable.First() == args.QueryText)
            {
                ptt.GoToBoardCompleted += Ptt_EnterBoardCompleted;
                ptt.GoToBoard(args.QueryText);
            }
            else if (RelatedTable.Count > 0)
            {
                BoardAutoSuggestBox.Text = RelatedTable.First();
            }
        }

        private async void Ptt_EnterBoardCompleted(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                ptt.GoToBoardCompleted -= Ptt_EnterBoardCompleted;
                LiPTT.Frame.Navigate(typeof(BoardPage));
            });
        }

        private bool ItemClick = false;

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ItemClick) return;

            ItemClick = true;
            ControlVisible = Visibility.Collapsed;

            if (e.ClickedItem is MyKeyValuePair kv)
            {
                ptt.GoToBoardCompleted += Ptt_EnterBoardCompleted;
                ptt.GoToBoard((string)kv.Value);
            }
        }
    }

    public class MyKeyValuePair : INotifyPropertyChanged
    {
        public object Key
        {
            get { return k; }
            set
            {
                k = value;
                NotifyPropertyChanged("Key");
            }
        }
        public object Value
        {
            get { return v; }
            set
            {
                v = value;
                NotifyPropertyChanged("Value");
            }
        }

        private object k;
        private object v;

        public MyKeyValuePair(object k, object v)
        {
            this.k = k;
            this.v = v;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
