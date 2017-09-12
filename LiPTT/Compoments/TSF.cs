using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Security;

namespace TSF
{
    public class TSFWapper
    {
        public static short[] GetLangIDs()
        {
            List<short> langIDs = new List<short>();

            if (TSF_NativeAPI.TF_CreateInputProcessorProfiles(out ITfInputProcessorProfiles profiles) == 0)
            {
                if (profiles.GetLanguageList(out IntPtr langPtrs, out int fetchCount) == 0)
                {
                    for (int i = 0; i < fetchCount; i++)
                    {
                        short id = Marshal.ReadInt16(langPtrs, sizeof(short) * i);
                        langIDs.Add(id);
                    }
                }
                Marshal.ReleaseComObject(profiles);
            }
            return langIDs.ToArray();
        }

        public static string[] GetInputMethodList(short langID)
        {
            List<string> imeList = new List<string>();
            if (TSF_NativeAPI.TF_CreateInputProcessorProfiles(out ITfInputProcessorProfiles profiles) == 0)
            {
                try
                {
                    if (profiles.EnumLanguageProfiles(langID, out IEnumTfLanguageProfiles enumerator) == 0)
                    {
                        if (enumerator != null)
                        {
                            TF_LANGUAGEPROFILE[] langProfile = new TF_LANGUAGEPROFILE[1];
                            int fetchCount = 0;
                            while (enumerator.Next(1, langProfile, out fetchCount) == 0)
                            {
                                if (profiles.GetLanguageProfileDescription(ref langProfile[0].clsid,
                                    langProfile[0].langid, ref langProfile[0].guidProfile, out IntPtr ptr) == 0)
                                {
                                    if (profiles.IsEnabledLanguageProfile(ref langProfile[0].clsid,
                                        langProfile[0].langid, ref langProfile[0].guidProfile, out bool enabled) == 0)
                                    {
                                        if (enabled)
                                            imeList.Add(Marshal.PtrToStringBSTR(ptr));
                                    }
                                }
                                Marshal.FreeBSTR(ptr);
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(profiles);
                }
            }
            return imeList.ToArray();
        }

        public static void GetCurrentLang(out string[] lang)
        {
            List<short> langIDs = new List<short>();
            ITfInputProcessorProfiles profiles;
            short id = -1;
            if (TSF_NativeAPI.TF_CreateInputProcessorProfiles(out profiles) == 0)
            {
                //IntPtr langPtrs;
                //int fetchCount = 0;
                profiles.GetCurrentLanguage(out id);
                Marshal.ReleaseComObject(profiles);
            }
            lang = id > -1 ? GetInputMethodList(id) : null;
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    internal struct TF_LANGUAGEPROFILE
    {
        internal Guid clsid;
        internal short langid;
        internal Guid catid;
        [MarshalAs(UnmanagedType.Bool)]
        internal bool fActive;
        internal Guid guidProfile;
    }

    [ComImport, SecurityCritical, Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfInputProcessorProfiles
    {
        [SecurityCritical]
        void Register(); //non-implement!! may be is wrong declaration.
        [SecurityCritical]
        void Unregister(); //non-implement!! may be is wrong declaration.
        [SecurityCritical]
        void AddLanguageProfile(); //non-implement!! may be is wrong declaration.
        [SecurityCritical]
        void RemoveLanguageProfile(); //non-implement!! may be is wrong declaration.
        [SecurityCritical]
        void EnumInputProcessorInfo(); //non-implement!! may be is wrong declaration.
        [SecurityCritical]
        int GetDefaultLanguageProfile(short langid, ref Guid catid, out Guid clsid, out Guid profile);
        [SecurityCritical]
        void SetDefaultLanguageProfile(); //non-implement!! may be is wrong declaration.
        [SecurityCritical]
        int ActivateLanguageProfile(ref Guid clsid, short langid, ref Guid guidProfile);
        [PreserveSig, SecurityCritical]
        int GetActiveLanguageProfile(ref Guid clsid, out short langid, out Guid profile);
        [SecurityCritical]
        int GetLanguageProfileDescription(ref Guid clsid, short langid, ref Guid profile, out IntPtr desc);
        [SecurityCritical]
        void GetCurrentLanguage(out short langid); //non-implement!! may be is wrong declaration.
        [PreserveSig, SecurityCritical]
        int ChangeCurrentLanguage(short langid); //non-implement!! may be is wrong declaration.
        [PreserveSig, SecurityCritical]
        int GetLanguageList(out IntPtr langids, out int count);
        [SecurityCritical]
        int EnumLanguageProfiles(short langid, out IEnumTfLanguageProfiles enumIPP);
        [SecurityCritical]
        int EnableLanguageProfile();
        [SecurityCritical]
        int IsEnabledLanguageProfile(ref Guid clsid, short langid, ref Guid profile, out bool enabled);
        [SecurityCritical]
        void EnableLanguageProfileByDefault(); //non-implement!! may be is wrong declaration.
        [SecurityCritical]
        void SubstituteKeyboardLayout(); //non-implement!! may be is wrong declaration.
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("3d61bf11-ac5f-42c8-a4cb-931bcc28c744")]
    internal interface IEnumTfLanguageProfiles
    {
        void Clone(out IEnumTfLanguageProfiles enumIPP);
        [PreserveSig]
        int Next(int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
        TF_LANGUAGEPROFILE[] profiles, out int fetched);
        void Reset();
        void Skip(int count);
    }

    internal static class TSF_NativeAPI
    {
        public static readonly Guid GUID_TFCAT_TIP_KEYBOARD;

        static TSF_NativeAPI()
        {
            GUID_TFCAT_TIP_KEYBOARD = new Guid(0x34745c63, 0xb2f0,
            0x4784, 0x8b, 0x67, 0x5e, 0x12, 200, 0x70, 0x1a, 0x31);
        }

        [SecurityCritical, DllImport("msctf.dll")]
        public static extern int TF_CreateInputProcessorProfiles(out ITfInputProcessorProfiles profiles);
    }
}
