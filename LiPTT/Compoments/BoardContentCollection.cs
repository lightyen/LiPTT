using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using System.Collections.ObjectModel;
using Windows.Foundation;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml;

namespace LiPTT
{
    public class BoardContentCollection : ObservableCollection<Article>, ISupportIncrementalLoading
    {
        PTT ptt;
        SemaphoreSlim Semaphore;
        public bool Loading;
        uint LoadMoreItemsResult;

        public BoardContentCollection()
        {
            CollectionChanged += BoardContentCollection_CollectionChanged;
            ptt = Application.Current.Resources["PTT"] as PTT;
            Semaphore = new SemaphoreSlim(0, 1);
        }

        private void BoardContentCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (Article item in e.NewItems)
                    item.PropertyChanged += MyType_PropertyChanged;

            if (e.OldItems != null)
                foreach (Article item in e.OldItems)
                    item.PropertyChanged -= MyType_PropertyChanged;
        }

        private void MyType_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, sender, sender, IndexOf((Article)sender));

            var t = LiPTT.RunInUIThread(() =>
            {
                OnCollectionChanged(args);
            });
        }

        private SemaphoreSlim sem = new SemaphoreSlim(0, 1);

        public bool HasMoreItems
        {
            get
            {
                if (ptt.HasMoreArticle && !Loading)
                {
                    Loading = true;
                    return true;
                }
                else
                    return false;
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
        }

        private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint count)
        {
            ptt.ArticlesReceived += Ptt_ArticlesReceived;
            ptt.GetArticles();
            await Semaphore.WaitAsync();
            ptt.ArticlesReceived -= Ptt_ArticlesReceived;
            Loading = false;
            return new LoadMoreItemsResult { Count = LoadMoreItemsResult };
        }

        private async void Ptt_ArticlesReceived(object sender, ArticlesReceivedEventArgs e)
        {
            await LiPTT.RunInUIThread(() => {

                foreach (var art in e.Articles)
                {
                    Add(art);
                }

                LoadMoreItemsResult = (uint)e.Articles.Count;
                Semaphore.Release();
            });       
        }

        public bool InitialLoaded { get; private set; }
    }
}
