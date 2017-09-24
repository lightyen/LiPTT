using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public TimeSpan CacheDuration { get; set; }

        public int MaxMemoryCacheCount { get; set; }

        private List<string> cache_filename;

        private SemaphoreSlim semaphoreSlim;

        public ImageCache()
        {
            MaxMemoryCacheCount = 10000;
            CacheDuration = TimeSpan.FromHours(12);
            cache_filename = new List<string>();
            semaphoreSlim = new SemaphoreSlim(1, 1);
        }

        public async Task ClearAllCache()
        {
            var cache_folder = ApplicationData.Current.LocalCacheFolder;
            Debug.WriteLine("Clear Cache");
            IReadOnlyList<StorageFile> files = await cache_folder.GetFilesAsync();

            foreach (var f in files)
            {
                await f.DeleteAsync();
            }
        }

        private async Task ClearCache(int num)
        {
            if (num <= cache_filename.Count)
            {
                for (int i = 0; i < num; i++)
                {
                    string name = cache_filename.First();
                    cache_filename.Remove(name);
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
            if (cache_filename.Count == MaxMemoryCacheCount)
            {
                await ClearCache(MaxMemoryCacheCount / 2);
            }

            string path = uri.LocalPath;

            string[] arr = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries );

            if (arr.Length > 0)
            {
                string name = arr.Last();

                await semaphoreSlim.WaitAsync();

                if (cache_filename.IndexOf(name) != -1)
                {
                    semaphoreSlim.Release();
                    await Task.Delay(500);
                }
                else
                {
                    cache_filename.Add(name);
                    semaphoreSlim.Release();
                }

                var file = await GetFileFromLocalCache(name);

                if (file == null)
                {
                    IBuffer buffer = await GetBufferAsync(uri);
                    await SaveFile(buffer, name);
                    file = await GetFileFromLocalCache(name);
                }

                return await GetBitmapImage(file);
            }
            else
            {
                return null;
            }
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
        /// <returns>存好的檔案</returns>
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
                memStream.Position = 0;
                bmp.SetSource(memStream.AsRandomAccessStream());
            }

            return bmp;
        }
    }
}
