using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace BrailleUrdu
{
    public class RawHelper
    {
        // Structure and API declarions:
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDataType;
        }
        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, Int32 level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, Int32 dwCount, out Int32 dwWritten);

        public static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, int dwCount)
        {
            int dwWritten = 0;
            IntPtr hPrinter = new IntPtr(0);
            RawHelper.DOCINFOA di = new RawHelper.DOCINFOA();
            bool printer = false;
            di.pDocName = "RAW Document";
            di.pDataType = "RAW";
            if (RawHelper.OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
            {
                if (RawHelper.StartDocPrinter(hPrinter, 1, di))
                {
                    if (RawHelper.StartPagePrinter(hPrinter))
                    {
                        printer = RawHelper.WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                        RawHelper.EndPagePrinter(hPrinter);
                    }
                    RawHelper.EndDocPrinter(hPrinter);
                }
                RawHelper.ClosePrinter(hPrinter);
            }
            if (!printer)
                Marshal.GetLastWin32Error();
            return printer;
        }

        public static bool SendFileToPrinter(string szPrinterName, string szFileName)
        {
            FileStream input = new FileStream(szFileName, FileMode.Open);
            BinaryReader binaryReader = new BinaryReader((Stream)input);
            byte[] numArray = new byte[input.Length];
            IntPtr num1 = new IntPtr(0);
            int int32 = Convert.ToInt32(input.Length);
            byte[] source = binaryReader.ReadBytes(int32);
            IntPtr num2 = Marshal.AllocCoTaskMem(int32);
            IntPtr destination = num2;
            int length = int32;
            Marshal.Copy(source, 0, destination, length);
            int num3 = RawHelper.SendBytesToPrinter(szPrinterName, num2, int32) ? 1 : 0;
            Marshal.FreeCoTaskMem(num2);
            input.Close();
            input.Dispose();
            return num3 != 0;
        }

        public static bool SendStringToPrinter(string szPrinterName, string szString)
        {
            int length = szString.Length;
            IntPtr coTaskMemAnsi = Marshal.StringToCoTaskMemAnsi(szString);
            RawHelper.SendBytesToPrinter(szPrinterName, coTaskMemAnsi, length);
            Marshal.FreeCoTaskMem(coTaskMemAnsi);
            return true;
        }
    }
}
