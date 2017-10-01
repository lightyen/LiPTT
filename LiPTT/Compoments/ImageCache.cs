using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

using Windows.UI.Xaml.Media.Imaging;
using System.IO;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;
using System.Threading;

namespace LiPTT
{
    public class ImageCache
    {
        /// <summary>
        /// 還沒實作，先放著
        /// </summary>
        public TimeSpan CacheDuration { get; set; }

        public int MaxMemoryCacheCount { get; set; }

        private List<Uri> cache_file_uri;

        private Dictionary<Uri, Guid> guid_table;

        private Dictionary<Uri, Task<StorageFile>> cache_task;

        private SemaphoreSlim semaphoreSlim;

        public ImageCache()
        {
            MaxMemoryCacheCount = 10000;
            CacheDuration = TimeSpan.FromHours(12);
            cache_file_uri = new List<Uri>();
            guid_table = new Dictionary<Uri, Guid>();
            cache_task = new Dictionary<Uri, Task<StorageFile>>();
            semaphoreSlim = new SemaphoreSlim(1, 1);
            Task.Run(async () => { await ClearAllCache(); });
        }

        public async Task ClearAllCache()
        {
            var cache_folder = ApplicationData.Current.LocalCacheFolder;
            IReadOnlyList<StorageFile> files = await cache_folder.GetFilesAsync();

            foreach (var f in files)
            {
                await f.DeleteAsync();
            }
        }

        private async Task ClearCache(int num)
        {
            if (num <= cache_file_uri.Count)
            {
                for (int i = 0; i < num; i++)
                {
                    Uri uri = cache_file_uri.First();
                    string name = guid_table[uri].ToString();
                    cache_file_uri.Remove(uri);
                    try
                    {
                        StorageFile file = await ApplicationData.Current.LocalCacheFolder.GetFileAsync(name);
                        await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    catch (FileNotFoundException)
                    {

                    }
                }
            }
        }

        /// <summary>
        /// 獲得圖片。有cache就抓cache，沒有就去download一個新的回來儲存
        /// </summary>
        /// <param name="uri">遠端位址</param>
        public async Task<BitmapImage> GetFromCacheAsync(Uri uri)
        {
            if (cache_file_uri.Count == MaxMemoryCacheCount)
            {
                await ClearCache(MaxMemoryCacheCount / 2);
            }

            await semaphoreSlim.WaitAsync();

            if (cache_task.Keys.Contains(uri))
            {
                semaphoreSlim.Release();
            }
            else
            {
                cache_file_uri.Add(uri);
                guid_table[uri] = Guid.NewGuid();
                //用GUID當檔名了，我就不信你會衝突
                Debug.WriteLine(string.Format("Create GUID: {0}", guid_table[uri]));
                cache_task[uri] = DownloadAndGetFile(uri, guid_table[uri].ToString());
                semaphoreSlim.Release();
            }

            var f = await cache_task[uri];

            if (f != null)
                return await GetBitmapImage(f);
            else
                return null;
        }

        private async Task<StorageFile> DownloadAndGetFile(Uri uri, string name)
        {
            IBuffer buffer = await GetBufferAsync(uri);
            Debug.WriteLine(string.Format("Downloaded: {0}", uri.OriginalString));
            if (buffer != null)
            {
                await SaveFile(buffer, name);
                Debug.WriteLine(string.Format("Save File: {0}", name));
                return await GetFileFromLocalCache(name);
            }
            else return null;
        }

        /// <summary>
        /// 從LocalCache獲得檔案
        /// </summary>
        private async Task<StorageFile> CreateFileFromeLocalCache(string filename)
        {
            return await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(filename);
        }

        /// <summary>
        /// 從網路下載圖片
        /// </summary>
        private async Task<IBuffer> GetBufferAsync(Uri uri)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                var buffer = await httpClient.GetBufferAsync(uri);
                return buffer;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                Debug.WriteLine("圖片下載失敗");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// 把檔案存到LocalCache
        /// </summary>
        private async Task SaveFile(IBuffer buffer, string filename)
        {
            var cache_folder = ApplicationData.Current.LocalCacheFolder;
            StorageFile file = await cache_folder.CreateFileAsync(filename);
            var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            using (var outputStream = stream.GetOutputStreamAt(0))
            {
                await outputStream.WriteAsync(buffer);
                await outputStream.FlushAsync();
            }
            await stream.FlushAsync();
            stream.Dispose();
        }

        /// <summary>
        /// 從LocalCache獲得檔案
        /// </summary>
        /// <returns>檔案，若沒有則傳回null</returns>
        private async Task<StorageFile> GetFileFromLocalCache(string filename)
        {
            var cache_folder = ApplicationData.Current.LocalCacheFolder;
            try
            {
                StorageFile file = await cache_folder.GetFileAsync(filename);
                return file;
            }
            catch (FileNotFoundException)
            {
                return null;
            }


        }

        /// <summary>
        /// 從檔案獲得BitmapImage
        /// </summary>
        /// <param name="file">已存在的StorageFile</param>
        private async Task<BitmapImage> GetBitmapImage(StorageFile file)
        {
            BitmapImage bmp = new BitmapImage();

            using (FileRandomAccessStream fras = (FileRandomAccessStream)await file.OpenAsync(FileAccessMode.Read))
            {
                //把圖像複製到記憶體中，脫離對StorageFile的依賴
                Stream stream = fras.AsStream();
                MemoryStream memStream = new MemoryStream();
                await stream.CopyToAsync(memStream);
                stream.Dispose();
                memStream.Position = 0;
                bmp.SetSource(memStream.AsRandomAccessStream());
            }

            return bmp;
        }
    }
}
