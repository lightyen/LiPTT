using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using SharpDX;
using SharpDX.DirectWrite;
using SharpDX.DXGI;
using SharpDX.Direct2D1;

using System.Diagnostics;

namespace LiPTT
{
    public sealed partial class CustomSwapChainPanel : SwapChainPanel
    {
        public string PreferFont = "Noto Sans Mono CJK TC";

        private SharpDX.Direct3D11.Device2 device;
        private SwapChain swapChain;
        private SharpDX.Direct3D11.Texture2D backBufferTexture;
        private DeviceContext d2d1DC;

        private TextFormat LeftFormat;
        private TextFormat RightFormat;

        public float FontSize
        {
            get; set;
        }

        public bool Colorful
        {
            get; set;
        }

        public CustomSwapChainPanel()
        {
            InitializeComponent();
            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            Loaded += CustomSwapChainPanel_Loaded;
            ptt.ScreenDrawn += Draw;
            Colorful = true;
        }

        private void CustomSwapChainPanel_Loaded(object sender, RoutedEventArgs e)
        {
            AdjustFontSize();
            CreateDirectXSwapChain();
            DrawPTT();
            SizeChanged += CustomSwapChainPanel_SizeChanged;
        }

        public void Dispose()
        {
            this.SizeChanged -= CustomSwapChainPanel_SizeChanged;

            lock (d2d1DC)
            {
                Utilities.Dispose(ref d2d1DC);
                Utilities.Dispose(ref backBufferTexture);
                Utilities.Dispose(ref swapChain);
                Utilities.Dispose(ref device);
            }
        }

        private void CustomSwapChainPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {

            lock (d2d1DC)
            {
                Utilities.Dispose(ref d2d1DC);
                Utilities.Dispose(ref backBufferTexture);

                try
                {
                    swapChain.ResizeBuffers(0, (int)ActualWidth, (int)ActualHeight, Format.Unknown, SwapChainFlags.None);
                }
                catch (SharpDXException ex)
                {
                    Debug.WriteLine(ex.ToString());
                }

                backBufferTexture = SharpDX.Direct3D11.Resource.FromSwapChain<SharpDX.Direct3D11.Texture2D>(swapChain, 0);

                using (var surface = backBufferTexture.QueryInterface<Surface2>())
                {
                    d2d1DC = new DeviceContext(surface);
                }
            }

            AdjustFontSize();
            DrawPTT();
        }

        public void CreateDirectXSwapChain()
        {
            SwapChainDescription1 swapChainDescription = new SwapChainDescription1() { 
                // Want Transparency.
                AlphaMode = SharpDX.DXGI.AlphaMode.Premultiplied,
                // Double buffer.
                BufferCount = 2,
                // BGRA 32bit pixel format.
                Format = Format.B8G8R8A8_UNorm,
                // Unlike in CoreWindow swap chains, the dimensions must be set.
                Height = (int)(ActualHeight),
                Width = (int)(ActualWidth),
                // Default multisampling.
                SampleDescription = new SampleDescription(1, 0),
                // In case the control is resized, stretch the swap chain accordingly.
                Scaling = Scaling.Stretch,
                // No support for stereo display.
                Stereo = false,
                // Sequential displaying for double buffering.
                SwapEffect = SwapEffect.FlipSequential,
                // This swapchain is going to be used as the back buffer.
                Usage = Usage.BackBuffer | Usage.RenderTargetOutput,
            };

            using (SharpDX.Direct3D11.Device defaultDevice = new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware, SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport))
            {
                this.device = defaultDevice.QueryInterface<SharpDX.Direct3D11.Device2>();
            }

            // Save the context instance
            //this.d3d11DC = this.device.ImmediateContext2;

            using (SharpDX.DXGI.Device3 dxgiDevice3 = device.QueryInterface<SharpDX.DXGI.Device3>())
            {
                using (SharpDX.DXGI.Factory3 dxgiFactory3 = dxgiDevice3.Adapter.GetParent<SharpDX.DXGI.Factory3>())
                {
                    using (SwapChain1 swapChain1 = new SwapChain1(dxgiFactory3, device, ref swapChainDescription))
                    {
                        swapChain = swapChain1.QueryInterface<SwapChain2>();
                    }
                }
            }

            using (ISwapChainPanelNative nativeObject = ComObject.As<ISwapChainPanelNative>(this))
            {
                nativeObject.SwapChain = swapChain;
            }

            backBufferTexture = SharpDX.Direct3D11.Resource.FromSwapChain<SharpDX.Direct3D11.Texture2D>(swapChain, 0);

            using (var surface = backBufferTexture.QueryInterface<Surface2>())
            {
                d2d1DC = new DeviceContext(surface);
            }
        }

        public void TestDraw()
        {
            d2d1DC.BeginDraw();
            d2d1DC.Clear(Color.Black);
            //=============================

            SolidColorBrush brush = new SolidColorBrush(d2d1DC, Color.Yellow);
            SolidColorBrush brush2 = new SolidColorBrush(d2d1DC, Color.YellowGreen);
            TextFormat textFormat = new TextFormat(DirectXFactory.DWFactory, PreferFont, FontSize);
            TextFormat textFormat2 = new TextFormat(DirectXFactory.DWFactory, PreferFont, FontSize);

            String msg = "正";

            TextLayout textLayout = new TextLayout(DirectXFactory.DWFactory, msg, textFormat, textFormat.FontSize * 1.5f, textFormat.FontSize);
            Size2F s = new Size2F(textLayout.Metrics.Width, textLayout.Metrics.Height);

            float d = s.Width / 2.0f;
            RectangleF rect = new RectangleF(0, 0, d, s.Height);


            textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
            textFormat2.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;

            textFormat.WordWrapping = WordWrapping.NoWrap; //遇到邊界時別下移
            DrawTextOptions op = DrawTextOptions.Clip; //遇到邊界時裁切

            float oy = (textLayout.Metrics.Height - textFormat.FontSize) / 2.0f;

            RectangleF rect1 = new RectangleF(0, oy, textLayout.Metrics.Width / 2.0f, textFormat.FontSize);
            RectangleF rect2 = new RectangleF(0, oy + textFormat.FontSize, textLayout.Metrics.Width / 2.0f, textFormat.FontSize);

            d2d1DC.FillRectangle(rect1, new SolidColorBrush(d2d1DC, Color.White));

            d2d1DC.FillRectangle(rect2, new SolidColorBrush(d2d1DC, Color.Blue));

            d2d1DC.DrawText("你", textFormat, rect, brush, op);
            rect.X += d;
            d2d1DC.DrawText("你", textFormat2, rect, brush2, op);
            rect.X += d;
            d2d1DC.DrawText("怎", textFormat, rect, brush, op);
            rect.X += d;
            d2d1DC.DrawText("怎", textFormat2, rect, brush2, op);
            rect.X += d;
            d2d1DC.DrawText("不", textFormat, rect, brush, op);
            rect.X += d;
            d2d1DC.DrawText("不", textFormat2, rect, brush2, op);
            rect.X += d;
            d2d1DC.DrawText("問", textFormat, rect, brush, op);
            rect.X += d;
            d2d1DC.DrawText("問", textFormat2, rect, brush2, op);
            rect.X += d;
            d2d1DC.DrawText("問", textFormat, rect, brush, op);
            rect.X += d;
            d2d1DC.DrawText("問", textFormat2, rect, brush2, op);
            rect.X += d;
            d2d1DC.DrawText("神", textFormat, rect, brush, op);
            rect.X += d;
            d2d1DC.DrawText("神", textFormat2, rect, brush2, op);
            rect.X += d;
            d2d1DC.DrawText("奇", textFormat, rect, brush, op);
            rect.X += d;
            d2d1DC.DrawText("奇", textFormat2, rect, brush2, op);
            rect.X += d;
            d2d1DC.DrawText("海", textFormat, rect, brush, op);
            rect.X += d;
            d2d1DC.DrawText("海", textFormat2, rect, brush2, op);
            rect.X += d;
            d2d1DC.DrawText("螺", textFormat, rect, brush, op);
            rect.X += d;
            d2d1DC.DrawText("螺", textFormat2, rect, brush2, op);

            //=============================
            d2d1DC.EndDraw();
            swapChain.Present(1, PresentFlags.None);
        }

        private async void Draw(object sender, ScreenEventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                DrawPTT(e.Screen);
            });
        }

        public void DrawPTT()
        {
            PTT ptt = Application.Current.Resources["PTT"] as PTT;
            if (ptt.Screen != null)
                DrawPTT(ptt.Screen);
        }

        private void DrawPTT(ScreenBuffer Buffer)
        {
            if (!DirectXFactory.Ready || d2d1DC == null) return;

            lock (d2d1DC)
            {
                LeftFormat = new TextFormat(DirectXFactory.DWFactory, PreferFont, FontSize);
                RightFormat = new TextFormat(DirectXFactory.DWFactory, PreferFont, FontSize);

                LeftFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
                RightFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;

                LeftFormat.WordWrapping = WordWrapping.NoWrap; //遇到邊界時別下移
                RightFormat.WordWrapping = WordWrapping.NoWrap;
                DrawTextOptions option = DrawTextOptions.Clip; //遇到邊界時裁切

                TextLayout textLayout = new TextLayout(DirectXFactory.DWFactory, "遇", LeftFormat, LeftFormat.FontSize, LeftFormat.FontSize);

                float dw = textLayout.MaxWidth / 2.0f;
                float dh = textLayout.MaxHeight + 0;
                float deltaX = -0.7f;
                float deltaY = -0.6f;
                float dx = dw + deltaX;
                float dy = dh + deltaY;

                Vector2 origin = new Vector2((float)((this.ActualWidth - dx * Buffer.Width) / 2.0), 0.0f);

                float oy = 1.0f + (textLayout.Metrics.Height - LeftFormat.FontSize) / 2.0f;
                RectangleF layoutRect = new RectangleF(origin.X, origin.Y, dw, textLayout.Metrics.Height);
                RectangleF backLayoutRect = new RectangleF(origin.X, origin.Y + oy, dw, dh);

                d2d1DC.BeginDraw();
                d2d1DC.Clear(Color.Black);

                if (Colorful) //無彩限的幻影世界
                {
                    //先畫背景
                    for (int i = 0; i < Buffer.Height; i++)
                    {
                        for (int j = 0; j < Buffer.Width; j++)
                        {
                            if (Buffer[i][j].Content < 0x7F) //ascii
                            {
                                SolidColorBrush Backbrush = new SolidColorBrush(d2d1DC, Buffer[i][j].GetBackgroundColor());
                                d2d1DC.FillRectangle(backLayoutRect, Backbrush);
                                backLayoutRect.X += dx;
                            }
                            else
                            {
                                SolidColorBrush Backbrush = new SolidColorBrush(d2d1DC, Buffer[i][j].GetBackgroundColor());

                                if (j + 1 >= Buffer.Width) break;

                                d2d1DC.FillRectangle(backLayoutRect, Backbrush);
                                backLayoutRect.X += dx;

                                Backbrush = new SolidColorBrush(d2d1DC, Buffer[i][++j].GetBackgroundColor());
                                d2d1DC.FillRectangle(backLayoutRect, Backbrush);
                                backLayoutRect.X += dx;
                            }
                        }
                        backLayoutRect.X = origin.X;
                        backLayoutRect.Y += dy;
                    }

                    //再畫文字
                    for (int i = 0; i < Buffer.Height; i++)
                    {
                        for (int j = 0; j < Buffer.Width; j++)
                        {
                            if (Buffer[i][j].Content < 0x7F) //ascii
                            {
                                SolidColorBrush Forebrush = new SolidColorBrush(d2d1DC, Buffer[i][j].GetForegroundColor());
                                d2d1DC.DrawText(Convert.ToString((char)Buffer[i][j].Content), LeftFormat, layoutRect, Forebrush, option);
                                layoutRect.X += dx;
                            }
                            else
                            {
                                SolidColorBrush Forebrush = new SolidColorBrush(d2d1DC, Buffer[i][j].GetForegroundColor());

                                if (j + 1 >= Buffer.Width) break;

                                string word = PTTEncoding.GetEncoding().GetString(new byte[] { Buffer[i][j].Content, Buffer[i][++j].Content });
                                d2d1DC.DrawText(word, LeftFormat, layoutRect, Forebrush, option);
                                layoutRect.X += dx;

                                Forebrush = new SolidColorBrush(d2d1DC, Buffer[i][j].GetForegroundColor());
                                d2d1DC.DrawText(word, RightFormat, layoutRect, Forebrush, option);
                                layoutRect.X += dx;
                            }
                        }
                        layoutRect.X = origin.X;
                        layoutRect.Y += dy;
                    }
                }
                else //沒有色彩的世界
                {
                    SolidColorBrush brush = new SolidColorBrush(d2d1DC, new Color(0xbb, 0xbb, 0xbb));
                    Vector2 o = Vector2.Zero;

                    for (int i = 0; i < Buffer.Height; i++)
                    {
                        List<byte> sb = new List<byte>();

                        for (int j = 0; j < Buffer.Width; j++)
                        {

                            if (Buffer[i][j].Content != 0) sb.Add(Buffer[i][j].Content);
                            else sb.Add((byte)' ');
                        }
                        String msg = PTTEncoding.GetEncoding().GetString(sb.ToArray());
                        TextLayout tl = new TextLayout(DirectXFactory.DWFactory, msg, LeftFormat, (float)this.ActualWidth, LeftFormat.FontSize);

                        RectangleF rect = new RectangleF(o.X, o.Y, tl.MaxWidth, tl.MaxHeight);
                        d2d1DC.DrawText(msg, LeftFormat, rect, brush);
                        o = o + new Vector2(0, LeftFormat.FontSize);
                    }
                }

                d2d1DC.EndDraw();
            
                swapChain.Present(1, PresentFlags.None);
            }
        }

        private void AdjustFontSize()
        {
            float a = (float)(this.ActualHeight * 26.0 / 622.0);
            float b = (float)(this.ActualWidth * 26.0 / 1172.0);
            FontSize = a < b ? a : b;
        }

        public void AdjustFontSize(double i)
        {
            FontSize = (float)(this.ActualWidth * 26.0 / i);
        }
    }
}
