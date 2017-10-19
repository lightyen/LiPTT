using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LiPTT
{
    //https://docs.microsoft.com/en-us/windows/uwp/debug-test-perf/listview-and-gridview-data-optimization

    public sealed partial class BoardPage : Page, INotifyPropertyChanged
    {
        public BoardPage()
        {
            InitializeComponent();
            
            CurrentBoard = new Board();
            LiPTT.ArticleCollection = ContentCollection;
            clipboardsem = new SemaphoreSlim(1, 1);
        }

        public Board CurrentBoard
        {
            get; set;
        }

        SemaphoreSlim clipboardsem;

        private async void Clipboard_ContentChanged(object sender, object e)
        {
            DataPackageView dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                string text = await dataPackageView.GetTextAsync();
                Clipboard.Clear();
                // To output the text from this example, you need a TextBlock control
                Debug.WriteLine("Clipboard now contains: " + text);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool control_visible;

        public bool ControlVisible
        {
            get
            {
                return control_visible;
            }
            private set
            {
                control_visible = value;
                NotifyPropertyChanged("ControlVisible");
                NotifyPropertyChanged("RingActive");
            }
        }

        public bool RingActive
        {
            get
            {
                return !control_visible;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            PTT ptt = Application.Current.Resources["PTT"] as PTT;

            ControlVisible = false;
            //冷靜一下，先喝杯咖啡
            await Task.Delay(100);

            if (!LiPTT.CacheBoard)
            {
                ContentCollection.Clear();

                ptt.BoardInfomationCompleted += async (a, b) =>
                {
                    CurrentBoard = b.BoardInfomation;
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                        NotifyPropertyChanged("CurrentBoard");
                        ControlVisible = true;
                    });
                };

                ptt.LoadBoardInfomation();
            }
            else
            {
                ControlVisible = true;
            }

            Debug.WriteLine("Board 訂閱事件");
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed += Board_PointerPressed;

            //追蹤剪貼簿
            //Clipboard.ContentChanged += Clipboard_ContentChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Debug.WriteLine("Board 取消訂閱");
            //取消訂閱剪貼簿
            //Clipboard.ContentChanged -= Clipboard_ContentChanged;
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed -= Board_PointerPressed;
        }

        private async void Ptt_ArticlesReceived(object sender, ArticlesReceivedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                PTT ptt = Application.Current.Resources["PTT"] as PTT;
                ptt.ArticlesReceived -= Ptt_ArticlesReceived;
                foreach (var a in e.Articles)
                    ContentCollection.Add(a);

                if (ArticleListView.Items.Count > 0)
                    ArticleListView.ScrollIntoView(ArticleListView.Items[0]);
            });
        }

        //進入文章
        private void ArticleList_ItemClick(object sender, ItemClickEventArgs e)
        {
            PTT ptt = Application.Current.Resources["PTT"] as PTT;

            Article article = e.ClickedItem as Article;
            if (article.Deleted) return;

            ptt.CurrentArticle = article;
            LiPTT.CacheBoard = true;

            LiPTT.Frame.Navigate(typeof(ArticlePage));
        }

        private void GoBack()
        {
            if (!control_visible || ContentCollection.Loading || LiPTT.Frame.CurrentSourcePageType != typeof(BoardPage)) return;
            LiPTT.CacheBoard = false;
            Debug.WriteLine("Board Page: 離開看板");

            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            ptt.StateChangedCompleted += async (a, b) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    LiPTT.Frame.Navigate(typeof(PTTPage));
                });
            };

            ptt.GoToMain();
        }

        private bool pressRight = false;

        private bool ctrlv = false;

        private async void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            bool Control_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;
            bool Shift_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0;

            if (Control_Down)
            {               
                if (!ctrlv && args.VirtualKey == VirtualKey.V && SearchIDTextBox != FocusManager.GetFocusedElement())
                {
                    ctrlv = true;
                    await clipboardsem.WaitAsync();
                    DataPackageView dataPackageView = Clipboard.GetContent();
                    if (dataPackageView.Contains(StandardDataFormats.Text))
                    {
                        string ClipboardText = await dataPackageView.GetTextAsync();
                        Debug.WriteLine("Clipboard now contains: " + ClipboardText);

                        PTT ptt = Application.Current.Resources["PTT"] as PTT;
                        ptt.NavigateToIDorAIDCompleted += SearchIDEnter;
                        ptt.NavigateToIDorAID(ClipboardText);

                        if (ClipboardText.StartsWith("#"))
                        {
                            //LiPTT.SendMessage(ClipboardText, 0x0D);
                            //LiPTT.PttEventEchoed += SearchIDEnter;
                        }
                        else
                        {
                            try
                            {
                                //uint id = Convert.ToUInt32(ClipboardText);
                                //LiPTT.SendMessage(id.ToString(), 0x0D);
                                //LiPTT.PttEventEchoed += SearchIDEnter;
                            }
                            catch { }
                        }
                    }
                    clipboardsem.Release();
                    return;
                }
            }
            else if (args.VirtualKey == VirtualKey.Escape || args.VirtualKey == VirtualKey.Left)
            {
                GoBack();
            }
            else if (args.VirtualKey >= VirtualKey.Number0 && args.VirtualKey <= VirtualKey.Number9 || args.VirtualKey >= VirtualKey.NumberPad0 && args.VirtualKey <= VirtualKey.NumberPad9)
            {
                if (Shift_Down)
                {
                    if (args.VirtualKey == VirtualKey.Number3 && SearchIDTextBox != FocusManager.GetFocusedElement())
                    {
                        SearchIDTextBox.Text = '#'.ToString();
                        SearchIDTextBox.SelectionStart = 1;
                        SearchIDTextBox.Focus(FocusState.Programmatic);
                    }
                }
                else
                {
                    if (args.VirtualKey >= VirtualKey.Number0 && args.VirtualKey <= VirtualKey.Number9)
                        SearchIDTextBox.Text = ((char)args.VirtualKey).ToString();
                    else
                        SearchIDTextBox.Text = ((args.VirtualKey - VirtualKey.NumberPad0)).ToString();

                    SearchIDTextBox.SelectionStart = 1;
                    SearchIDTextBox.Focus(FocusState.Programmatic);
                }
            }
        }

        private void Board_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            if (pressRight == false && args.CurrentPoint.Properties.IsRightButtonPressed)
            {
                pressRight = true;
                Window.Current.CoreWindow.PointerReleased += Board_PointerReleased;
            }
        }

        private void Board_PointerReleased(CoreWindow sender, PointerEventArgs args)
        {
            if (pressRight)
            {
                pressRight = false;
                Window.Current.CoreWindow.PointerPressed -= Board_PointerPressed;
                Window.Current.CoreWindow.PointerReleased -= Board_PointerReleased;
                GoBack();
            }
        }

        private void SearchIDTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }


        private void SearchIDTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
        }

        private void SearchIDTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            PTT ptt = Application.Current.Resources["PTT"] as PTT;

            if (e.Key == VirtualKey.Escape)
            {
                SearchIDTextBox.Text = "";
                FocusManager.TryMoveFocus(FocusNavigationDirection.Previous);
                FocusManager.TryMoveFocus(FocusNavigationDirection.Previous);
            }
            else if (e.Key == VirtualKey.Enter)
            {
                FocusManager.TryMoveFocus(FocusNavigationDirection.Previous);
                FocusManager.TryMoveFocus(FocusNavigationDirection.Previous);

                ptt.NavigateToIDorAIDCompleted += SearchIDEnter;
                ptt.NavigateToIDorAID(SearchIDTextBox.Text);
            }
        }

        private async void SearchIDEnter(object sender, PTTStateUpdatedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                PTT ptt = Application.Current.Resources["PTT"] as PTT;
                ptt.NavigateToIDorAIDCompleted -= SearchIDEnter;

                if (e.State == PttState.PressAny)
                {
                    ptt.PressAnyKey();
                }
                else if (e.State == PttState.Board && e.Article != null)
                {
                    ptt.CurrentArticle = e.Article;
                    ctrlv = false;
                    LiPTT.Frame.Navigate(typeof(ArticlePage));
                }
            });

        }
    }
}
