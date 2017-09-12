using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiPTT
{
    public class ColorHelper
    {
        public static List<Windows.UI.Color> GetColors(Windows.UI.Color baseColor, int max)
        {
            // fill color shades list
            List<Windows.UI.Color> colorShades = new List<Windows.UI.Color>();
            HSVColor hsv = ColorHelper.RGBtoHSV(baseColor);
            hsv.V = 255; // alway use highest brightness to determine collection of shades
            double v = hsv.V / max;
            for (int i = 0; i < max; i++)
            {
                hsv.V = v * i;
                if (hsv.V > 255) hsv.V = 255;
                colorShades.Add(ColorHelper.HSVtoRGB(hsv));
            }
            return colorShades;
        }

        public static HSVColor RGBtoHSV(Windows.UI.Color rgb)
        {
            double max, min, chroma;
            HSVColor hsv = new HSVColor();

            min = Math.Min(Math.Min(rgb.R, rgb.G), rgb.B);
            max = Math.Max(Math.Max(rgb.R, rgb.G), rgb.B);
            chroma = max - min;

            if (chroma != 0)
            {
                if (rgb.R == max)
                {
                    hsv.H = (rgb.G - rgb.B) / chroma;
                    if (hsv.H < 0.0) hsv.H += 6.0;
                }
                else if (rgb.G == max)
                {
                    hsv.H = ((rgb.B - rgb.R) / chroma) + 2.0;
                }
                else
                {
                    hsv.H = ((rgb.R - rgb.G) / chroma) + 4.0;
                }
                hsv.H *= 60.0;
            }

            if (max != 0) hsv.S = chroma / max;

            hsv.V = max / 255.0;
            hsv.A = rgb.A;

            return hsv;
        }

        public static Windows.UI.Color HSVtoRGB(HSVColor hsv)
        {
            double min, chroma, hdash, x;
            Windows.UI.Color rgb = new Windows.UI.Color();

            chroma = hsv.S * hsv.V;
            hdash = hsv.H / 60.0;
            x = chroma * (1.0 - Math.Abs((hdash % 2.0) - 1.0));

            double _R = 0, _G = 0, _B = 0;

            if (hdash < 1.0)
            {
                _R = chroma;
                _G = x;
            }
            else if (hdash < 2.0)
            {
                _R = x;
                _G = chroma;
            }
            else if (hdash < 3.0)
            {
                _G = chroma;
                _B = x;
            }
            else if (hdash < 4.0)
            {
                _G = x;
                _B = chroma;
            }
            else if (hdash < 5.0)
            {
                _R = x;
                _B = chroma;
            }
            else if (hdash < 6.0)
            {
                _R = chroma;
                _B = x;
            }

            min = hsv.V - chroma;

            rgb.R = (byte)((_R + min) * 255.0);
            rgb.G = (byte)((_G + min) * 255.0);
            rgb.B = (byte)((_B + min) * 255.0);
            rgb.A = (byte)hsv.A;

            return rgb;
        }
    }

    public class HSVColor
    {
        public double H { get; set; }
        public double S { get; set; }
        public double V { get; set; }
        public double A { get; set; }

        public HSVColor()
        {
            H = S = V = A = 0.0;
        }
    }
}
