using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SmartCafeteria.Printing;

public static class RawPrinterHelper
{
    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr hPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] DocInfo pDocInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBuf, int cbBuf, out int pcWritten);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pDocName = string.Empty;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pOutputFile = null;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pDatatype = "RAW";
    }

    /// <summary>Sends raw bytes to a printer and throws <see cref="Win32Exception"/>
    /// when any spooler call fails, so the caller can tell the cashier that the
    /// receipt did not print. Unmanaged buffers are always released.</summary>
    public static void SendBytes(string printerName, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);
        ArgumentNullException.ThrowIfNull(bytes);

        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            throw Win32Error($"Cannot open printer '{printerName}'");

        var documentStarted = false;
        var pageStarted = false;
        try
        {
            var docInfo = new DocInfo { pDocName = "Smart Cafeteria Receipt" };
            if (!StartDocPrinter(hPrinter, 1, docInfo))
                throw Win32Error("StartDocPrinter");
            documentStarted = true;

            if (!StartPagePrinter(hPrinter))
                throw Win32Error("StartPagePrinter");
            pageStarted = true;

            if (bytes.Length == 0)
                return;

            var pBytes = Marshal.AllocCoTaskMem(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, pBytes, bytes.Length);
                if (!WritePrinter(hPrinter, pBytes, bytes.Length, out var written) || written != bytes.Length)
                    throw Win32Error("WritePrinter");
            }
            finally
            {
                Marshal.FreeCoTaskMem(pBytes);
            }
        }
        finally
        {
            if (pageStarted && !EndPagePrinter(hPrinter))
            {
                // Best effort: the document cleanup below is what releases the
                // spooler job, and the original failure is more useful.
            }
            if (documentStarted && !EndDocPrinter(hPrinter))
            {
                // Same as above; never mask the original exception.
            }
            ClosePrinter(hPrinter);
        }
    }

    private static Win32Exception Win32Error(string operation)
        => new(Marshal.GetLastWin32Error(), $"Printer operation failed: {operation}.");
}
