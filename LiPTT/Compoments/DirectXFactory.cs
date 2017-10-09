using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpDX
{
    public static class DirectXFactory
    {
        private static Direct2D1.Factory d2d1factory;
        private static WIC.ImagingFactory imageFactory;
        private static DirectWrite.Factory writeFactory;

        public static Direct2D1.Factory D2D1Factory => d2d1factory;
        public static WIC.ImagingFactory ImageFactory => imageFactory;
        public static DirectWrite.Factory DWFactory => writeFactory;

        public static bool Ready
        {
            get; set;
        }

        public static void CreateIndependentResource()
        {
            d2d1factory = new Direct2D1.Factory();
            imageFactory = new WIC.ImagingFactory();
            writeFactory = new DirectWrite.Factory();
            Ready = true;
        }

        public static void ReleaseIndependentResource()
        {
            Utilities.Dispose(ref imageFactory);
            Utilities.Dispose(ref writeFactory);
            Utilities.Dispose(ref d2d1factory);
            Ready = false;
        }

        public static IList<string> EnumAdpter()
        {
            DXGI.Factory4 dxgiFactory = new DXGI.Factory4();
            List<string> AdapterList = new List<string>();
            foreach (var a in dxgiFactory.Adapters)
            {
                AdapterList.Add(a.Description.Description);
            }
            return AdapterList;
        }

        public static List<string> GetInstalledFontNames()
        {
            List<string> li = new List<string>();
            var fonts = DWFactory.GetSystemFontCollection(false);

            int count = fonts.FontFamilyCount;

            for (int i = 0; i < fonts.FontFamilyCount; i++)
            {
                var FontFamily = fonts.GetFontFamily(i);
                var FamilyNames = FontFamily.FamilyNames;

                string name;
                int index;

                if (FamilyNames.FindLocaleName("zh-TW", out index))
                    name = FamilyNames.GetString(index);
                else if (FamilyNames.FindLocaleName("zh-CN", out index))
                    name = FamilyNames.GetString(index);
                else if (FamilyNames.FindLocaleName("ja", out index))
                    name = FamilyNames.GetString(index);
                else if (FamilyNames.FindLocaleName("ko", out index))
                    name = FamilyNames.GetString(index);
                else if (FamilyNames.FindLocaleName("en-US", out index))
                    name = FamilyNames.GetString(index);
                else
                    name = FamilyNames.GetString(index);
                li.Add(name);
            }

            li.Sort();

            return li;
        }
    }
}
