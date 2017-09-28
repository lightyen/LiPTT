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

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=234238

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class PTTPage : Page
    {
        public PTTPage()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (Symbol bol in Enum.GetValues(typeof(Symbol)))
            {
                AppBarButton button = new AppBarButton() { Icon = new SymbolIcon(bol), Label = bol.ToString(), Foreground = new SolidColorBrush(Windows.UI.Colors.AntiqueWhite) };
                Panel.Children.Add(button);

            }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
            switch (pivot.SelectedIndex)
            {
                case 0:
                    {
                        if (pivot.SelectedItem is PivotItem item && item.Content is Frame frame)
                            frame.Navigate(typeof(MainFunctionPage));
                    }
                    break;
                case 1:
                    {
                        if (pivot.SelectedItem is PivotItem item && item.Content is Frame frame)
                            frame.Navigate(typeof(FavoritePage));
                    }
                    break;
                case 2:
                    {
                        if (pivot.SelectedItem is PivotItem item && item.Content is Frame frame)
                            frame.Navigate(typeof(SettingPage));
                    }
                    break;
            }
        }
    }
}
