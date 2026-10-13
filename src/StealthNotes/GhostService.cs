using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StealthNotes;

internal readonly record struct GhostResult(bool Success, string? Error);

internal sealed class GhostService
{
    public static bool Supported => OperatingSystem.IsWindowsVersionAtLeast(10,0,19041);
    static GhostResult Failed(string api) => new(false,$"{api}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");

    public GhostResult Apply(nint hwnd,bool ghost,double opacity)
    {
        if(ghost&&!Supported) return new(false,"Capture exclusion requires Windows 10 version 2004 or later.");
        uint desired=ghost?Native.Exclude:0;
        if(!Native.SetWindowDisplayAffinity(hwnd,desired)) return Failed("SetWindowDisplayAffinity");
        if(!Native.GetWindowDisplayAffinity(hwnd,out uint readback)) return Failed("GetWindowDisplayAffinity");
        if(readback!=desired) return new(false,$"Affinity mismatch: got 0x{readback:x}, expected 0x{desired:x}.");
        long style=Native.GetWindowLongPtrW(hwnd,Native.ExStyle).ToInt64()|Native.Layered|Native.Topmost;
        style=ghost?style|Native.Transparent|Native.NoActivate:style&~(Native.Transparent|Native.NoActivate);
        Marshal.SetLastPInvokeError(0);
        nint previous=Native.SetWindowLongPtrW(hwnd,Native.ExStyle,(nint)style);
        if(previous==0&&Marshal.GetLastWin32Error()!=0) return Failed("SetWindowLongPtr");
        if(!Native.SetWindowPos(hwnd,-1,0,0,0,0,0x33)) return Failed("SetWindowPos");
        if(!Native.SetLayeredWindowAttributes(hwnd,0,(byte)Math.Round(Math.Clamp(opacity,.1,1)*255),2)) return Failed("SetLayeredWindowAttributes");
        long actual=Native.GetWindowLongPtrW(hwnd,Native.ExStyle).ToInt64();
        if((actual&Native.Topmost)==0 || (actual&Native.Layered)==0 || ((actual&Native.Transparent)!=0)!=ghost)
            return new(false,"Window styles did not match the requested settings.");
        return new(true,null);
    }
}
