using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using System.Diagnostics;

namespace LiPTT
{
    public class LiPTT_Encoding
    {
        public static string PreferEncoding
        {
            get; set;
        }

        private static Encoding big5_uao;

        public static Encoding GetEncoding(string name)
        {
            string ln = name.ToLower();
            if (ln == "big5-uao" || ln == "big5_uao" || ln == "big5 uao")
            {
                if (big5_uao == null) big5_uao = new Big5_UAO();
                return big5_uao;
            }
            else
            {
                return Encoding.GetEncoding(name);
            }
        }

        public static Encoding GetEncoding()
        {
            if (PreferEncoding == null) PreferEncoding = "Big5-UAO";
            return GetEncoding(PreferEncoding);
        }
    }
    
    public class Big5_UAO : Encoding
    {
        static Hashtable b2u_table;
        static Hashtable u2b_table;

        public Big5_UAO()
        {
            if (b2u_table == null || u2b_table == null)
            {
                b2u_table = new Hashtable();
                u2b_table = new Hashtable();
                Task task = new Task(LoadEncoding);
                task.Start();
                task.Wait();
            }
        }

        private async void LoadEncoding()
        {
            try
            {
                //https://moztw.org/docs/big5/table/uao250-b2u.txt
                var file_b2u = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Encoding/b2u_table.txt"));

                using (var inputStream = await file_b2u.OpenReadAsync())
                using (var classicStream = inputStream.AsStreamForRead())
                using (var streamReader = new StreamReader(classicStream))
                {
                    string line = streamReader.ReadLine();

                    while (streamReader.Peek() >= 0)
                    {
                        line = streamReader.ReadLine();

                        string[] s = line.Split(' ');
                        
                        int k = Int32.Parse(s[0].Substring(2), System.Globalization.NumberStyles.HexNumber);
                        int v = Int32.Parse(s[1].Substring(2), System.Globalization.NumberStyles.HexNumber);
                        try
                        {
                            b2u_table.Add(k, v);
                        }
                        catch (ArgumentException)
                        {
                            Debug.WriteLine("編碼重複?");
                        }
                    }
                }

                var file_u2b = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Encoding/u2b_table.txt"));

                using (var inputStream = await file_u2b.OpenReadAsync())
                using (var classicStream = inputStream.AsStreamForRead())
                using (var streamReader = new StreamReader(classicStream))
                {
                    string line = streamReader.ReadLine();

                    while (streamReader.Peek() >= 0)
                    {
                        line = streamReader.ReadLine();

                        string[] s = line.Split(' ');

                        int k = Int32.Parse(s[0].Substring(2), System.Globalization.NumberStyles.HexNumber);
                        int v = Int32.Parse(s[1].Substring(2), System.Globalization.NumberStyles.HexNumber);
                        try
                        {
                            u2b_table.Add(k, v);
                        }
                        catch (ArgumentException)
                        {
                            Debug.WriteLine("編碼重複?");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            throw new NotImplementedException();
        }

        public override byte[] GetBytes(string s)
        {
            List<byte> list = new List<byte>();

            foreach (char c in s)
            {
                int k = System.Convert.ToInt32(c);
                if (k < 0x7F)
                {
                    list.Add((byte)k);
                }
                else
                {
                    k = (int)u2b_table[k];
                    list.Add((byte)(k >> 8));
                    list.Add((byte)(k & 0xFF));
                }
            }

            return list.ToArray();
        }

        public override byte[] GetBytes(char[] chars)
        {
            return GetBytes(chars, 0, chars.Length);
        }

        public override byte[] GetBytes(char[] chars, int index, int count)
        {
            List<byte> list = new List<byte>();

            for (int i = index; i < index + count; i++)
            {
                int k = System.Convert.ToInt32(chars[index]);
                if (k < 0x7F)
                {
                    list.Add((byte)k);
                }
                else
                {
                    k = (int)u2b_table[k];
                    list.Add((byte)(k >> 8));
                    list.Add((byte)(k & 0xFF));
                }
            }
            return list.ToArray();
        }

        public override string GetString(byte[] bytes)
        {
            return GetString(bytes, 0, bytes.Length);
        }

        public override string GetString(byte[] bytes, int index, int count)
        {
            StringBuilder sb = new StringBuilder();

            int i = index;
            while (i < index + count)
            {
                if (bytes[i] < 0x7F) //ASCII
                {
                    sb.Append((char)bytes[i++]);
                }
                else
                {
                    int k = bytes[i++];
                    if (i < index + count)
                    {
                        k <<= 8;
                        k += bytes[i++];
                        try
                        {
                            int v = (int)b2u_table[k];
                            sb.Append((char)v);
                        }
                        catch (NullReferenceException)
                        {
                            sb.Append('☐');
                            Debug.WriteLine("找不到編碼? ☐☐☐");
                        }
                    }
                    else break;
                }
            }

            return sb.ToString();
        }

        public override int GetByteCount(char[] chars, int index, int count)
        {
            int c = 0;

            for (int i = index; i < index + count; i++)
            {
                if (chars[i] < 0x7F)
                {
                    c++;
                }
                else
                {
                    c += 2;
                }
            }

            return c;
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            int ans = 0;
            int i = index;

            while (i < index + count)
            {
                if (bytes[i] < 0x7F) //ASCII
                {
                    i++;
                }
                else
                {
                    i += 2;
                }
                ans++;
            }

            if (i > index + count) ans--;

            return ans;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            int c = 0;
            int i = byteIndex;

            while (i < byteIndex + byteCount)
            {
                if (bytes[i] < 0x7F) //ASCII
                {
                    chars[c + charIndex] = (char)bytes[i];
                    i++;
                    c++;
                }
                else if (i + 2 < byteIndex + byteCount)
                {
                    int k = bytes[i++];
                    k <<= 8;
                    k += bytes[i++];
                    try
                    {
                        int v = (int)b2u_table[k];
                        chars[c + charIndex] = (char)v;
                    }
                    catch (NullReferenceException)
                    {
                        chars[c + charIndex] = '☐';
                        Debug.WriteLine("找不到編碼? ☐☐☐");
                    }
                    c++;
                }
            }

            return c;
        }

        public override int GetMaxByteCount(int charCount)
        {
            return charCount * 2;
        }

        public override int GetMaxCharCount(int byteCount)
        {
            return byteCount;
        }
    }
}
