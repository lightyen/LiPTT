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

namespace LiPTT
{
    public sealed partial class EchoContentDialog : ContentDialog
    {
        public Evaluation Evaluation { get; set; }

        public bool Showing { get; private set; }

        public EchoContentDialog()
        {
            InitializeComponent();
            Opened += EchoContentDialog_Opened;
            Closed += EchoContentDialog_Closed;
        }

        private void EchoContentDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            Showing = false;
            EchoTextBox.Text = "";
        }

        private void EchoContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            Showing = true;
        }

        private void OkClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine(EchoTextBox.Text);
        }

        private void CancelClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        } 
    }
}
