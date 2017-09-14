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

using System.Diagnostics;
using Windows.UI.Core;
using Windows.System;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=234238

namespace LiPTT
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class BBSPage : Page
    {
        public BBSPage()
        {
            this.InitializeComponent();
            LiPTT.Current.Connected += Client_Connected;
            LiPTT.Current.Belled += Client_BellPlayed;
        }

        private async void Client_BellPlayed(object sender, EventArgs e)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                /***
                if (DefaultBell.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Paused)
                {
                    DefaultBell.Play();
                }
                /***/
            });
        }

        private async void Client_Connected(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {

            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            List<string> fontNames = SharpDX.DirectXFactory.GetInstalledFontNames();
            foreach (var s in fontNames) FontsComboBox.Items.Add(s);
 
            Window.Current.CoreWindow.KeyDown += PanelKeyDown;
            
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= PanelKeyDown;
            myPanel.Dispose();
        }

        private void FontsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            myPanel.PreferFont = FontsComboBox.SelectedValue as string;
            myPanel.DrawPTT();
        }

        private void PanelKeyDown(CoreWindow sender, KeyEventArgs e)
        {
            bool Control_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control) &CoreVirtualKeyStates.Down) != 0;
            bool Shift_Down = (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift) &CoreVirtualKeyStates.Down) != 0;
            //bool Alt_Down; //Dispatcher.AcceleratorKeyActivated instead, it handles Alt key.

            bool CapsLocked = (Window.Current.CoreWindow.GetKeyState(VirtualKey.CapitalLock) & CoreVirtualKeyStates.Locked) != 0;

            int key = (int)e.VirtualKey;

            if (e.VirtualKey == VirtualKey.Enter)
            {
                LiPTT.Current.Send(new byte[] { (byte)'\r' });
                return;
            }

            if (!Control_Down && !Shift_Down)
            {
                if (IsDigit(key))
                {
                    LiPTT.Current.Send(new byte[] { (byte)key });
                }
                else if (IsUpperCase(key))
                {
                    if (CapsLocked)
                        LiPTT.Current.Send(new byte[] { (byte)(key) }); //A-Z
                    else
                        LiPTT.Current.Send(new byte[] { (byte)(key + 0x20) }); //a-z
                }
                else
                {
                    switch (key)
                    {
                        case 0xBA: // ';:'
                            LiPTT.Current.Send(new byte[] { (byte)';' });
                            break;
                        case 0xBB: // '=+'
                            LiPTT.Current.Send(new byte[] { (byte)'=' });
                            break;
                        case 0xBC: // ',<'
                            LiPTT.Current.Send(new byte[] { (byte)',' });
                            break;
                        case 0xBD: // '-_'
                            LiPTT.Current.Send(new byte[] { (byte)'-' });
                            break;
                        case 0xBE: // '.>'
                            LiPTT.Current.Send(new byte[] { (byte)'.' });
                            break;
                        case 0xBF: // '/?'
                            LiPTT.Current.Send(new byte[] { (byte)'/' });
                            break;
                        default:
                            //其他特殊鍵位
                            switch (e.VirtualKey)
                            {
                                case VirtualKey.Up:
                                    LiPTT.Current.Send(new byte[] { (byte)0x1B, (byte)'[', (byte)'A' });
                                    break;
                                case VirtualKey.Down:
                                    LiPTT.Current.Send(new byte[] { (byte)0x1B, (byte)'[', (byte)'B' });
                                    break;
                                case VirtualKey.Right:
                                    LiPTT.Current.Send(new byte[] { (byte)0x1B, (byte)'[', (byte)'C' });
                                    break;
                                case VirtualKey.Left:
                                    LiPTT.Current.Send(new byte[] { (byte)0x1B, (byte)'[', (byte)'D' });
                                    break;
                                case VirtualKey.Escape:
                                    LiPTT.Current.Send(new byte[] { (byte)0x1B, (byte)'[', (byte)'D' });
                                    break;
                                case VirtualKey.Space:
                                    LiPTT.Current.Send(new byte[] { (byte)' ' });
                                    break;
                                case VirtualKey.Back:
                                    LiPTT.Current.Send(new byte[] { (byte)0x08 });
                                    break;
                                case VirtualKey.PageUp:
                                    LiPTT.Current.Send(new byte[] { (byte)0x02 });
                                    break;
                                case VirtualKey.PageDown:
                                    LiPTT.Current.Send(new byte[] { (byte)0x06 });
                                    break;
                                case VirtualKey.Insert:
                                    break;
                                case VirtualKey.Home:
                                    LiPTT.Current.Send(new byte[] { (byte)'0', (byte)'\r' });
                                    break;
                                case VirtualKey.End:
                                    LiPTT.Current.Send(new byte[] { (byte)'$' });
                                    break;
                                default:
                                    Debug.WriteLine("Not Implemented KeyDown 0x{0:X2} {1} {2}", key, Control_Down, Shift_Down);
                                    break;
                            }
                            break;
                    }    
                }
            }
            else if (!Control_Down && Shift_Down)
            {
                if (IsDigit(key))
                {
                    switch (e.VirtualKey)
                    {
                        case VirtualKey.Number1:
                            LiPTT.Current.Send(new byte[] { (byte)'!' });
                            break;
                        case VirtualKey.Number2:
                            LiPTT.Current.Send(new byte[] { (byte)'@' });
                            break;
                        case VirtualKey.Number3:
                            LiPTT.Current.Send(new byte[] { (byte)'#' });
                            break;
                        case VirtualKey.Number4:
                            LiPTT.Current.Send(new byte[] { (byte)'$' });
                            break;
                        case VirtualKey.Number5:
                            LiPTT.Current.Send(new byte[] { (byte)'%' });
                            break;
                        case VirtualKey.Number6:
                            LiPTT.Current.Send(new byte[] { (byte)'^' });
                            break;
                        case VirtualKey.Number7:
                            LiPTT.Current.Send(new byte[] { (byte)'&' });
                            break;
                        case VirtualKey.Number8:
                            LiPTT.Current.Send(new byte[] { (byte)'*' });
                            break;
                        case VirtualKey.Number9:
                            LiPTT.Current.Send(new byte[] { (byte)'(' });
                            break;
                        case VirtualKey.Number0:
                            LiPTT.Current.Send(new byte[] { (byte)')' });
                            break;
                        default:
                            break;
                    }
                }
                else if (IsUpperCase(key))
                {
                    if (CapsLocked)
                        LiPTT.Current.Send(new byte[] { (byte)(key + 0x20) }); //a-z
                    else
                        LiPTT.Current.Send(new byte[] { (byte)(key) }); //A-Z
                }
                else
                {
                    switch (key)
                    {
                        case 0xBA: // ';:'
                            LiPTT.Current.Send(new byte[] { (byte)':' });
                            break;
                        case 0xBB: // '=+'
                            LiPTT.Current.Send(new byte[] { (byte)'+' });
                            break;
                        case 0xBC: // ',<'
                            LiPTT.Current.Send(new byte[] { (byte)'<' });
                            break;
                        case 0xBD: // '-_'
                            LiPTT.Current.Send(new byte[] { (byte)'_' });
                            break;
                        case 0xBE: // '.>'
                            LiPTT.Current.Send(new byte[] { (byte)'>' });
                            break;
                        case 0xBF: // '/?'
                            LiPTT.Current.Send(new byte[] { (byte)'?' });
                            break;
                        default:
                            Debug.WriteLine("Not Implemented KeyDown 0x{0:X2} {1} {2}", key, Control_Down, Shift_Down);
                            break;
                    }
                }
            }
            else if (Control_Down && !Shift_Down)
            {
                if (IsUpperCase(key))
                {
                    LiPTT.Current.Send(new byte[] { (byte)(key - 0x40) });
                }
                else if (IsDigit(key))
                {
                    Debug.WriteLine("Not Implemented KeyDown 0x{0:X2} {1} {2}", key, Control_Down, Shift_Down);
                }
                else
                {
                    switch(key)
                    {
                        default:
                            Debug.WriteLine("Not Implemented KeyDown 0x{0:X2} {1} {2}", key, Control_Down, Shift_Down);
                            break;
                    }
                }
            }
            else
            {
                Debug.WriteLine("Not Implemented KeyDown 0x{0:X2} {1} {2}", key, Control_Down, Shift_Down);
            }
        }

        private bool IsDigit(int v)
        {
            return v >= 0x30 && v <= 0x39;
        }

        private bool IsUpperCase(int v)
        {
            return v >= 0x41 && v <= 0x5A;
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            myPanel?.AdjustFontSize((double)e.NewValue);
            myPanel?.DrawPTT();
        }

        private void RadioButtonTCP_Click(object sender, RoutedEventArgs e)
        {
            LiPTT.Current.SSH = false;
        }

        private void RadioButtonSSH_Click(object sender, RoutedEventArgs e)
        {
            LiPTT.Current.SSH = true;
        }

        private void Reconnect_Click(object sender, RoutedEventArgs e)
        {
            LiPTT.Current.Connect();
        }

        private void ReDraw_Click(object sender, RoutedEventArgs e)
        {
            myPanel.DrawPTT();
        }
    }
}
