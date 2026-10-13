using System.Runtime.InteropServices;

namespace StealthNotes;

internal static class Native
{
    public const int ExStyle=-20;
    public const long Layered=0x80000, Transparent=0x20, NoActivate=0x08000000, Topmost=8;
    public const uint Exclude=0x11;
    [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowDisplayAffinity(nint hwnd,uint affinity);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool GetWindowDisplayAffinity(nint hwnd,out uint affinity);
    [DllImport("user32.dll",SetLastError=true)] public static extern nint GetWindowLongPtrW(nint hwnd,int index);
    [DllImport("user32.dll",SetLastError=true)] public static extern nint SetWindowLongPtrW(nint hwnd,int index,nint style);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int w,int h,uint flags);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool SetLayeredWindowAttributes(nint hwnd,uint key,byte alpha,uint flags);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool UnregisterHotKey(nint hwnd,int id);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern nint SendMessageW(nint hwnd,int message,nint w,nint l);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern nint GetTopWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern nint GetWindow(nint hwnd,uint command);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool HideCaret(nint hwnd);
}
