using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StealthNotes;

internal sealed class Hotkeys : NativeWindow, IDisposable
{
    KeyBinding[] registered=[];
    public event Action<HotkeyAction>? Pressed;
    public Func<bool>? QueryEndSession { get; set; }
    public Action? EndSession { get; set; }
    public Hotkeys() => CreateHandle(new CreateParams { Caption="StealthNotes commands", Width=0,Height=0,Style=0 });
    public static bool Validate(HotkeySettings? settings,out string error)
    {
        error="";
        if(settings?.Bindings is null || settings.Bindings.Length!=Enum.GetValues<HotkeyAction>().Length || settings.Bindings.Any(b=>b is null)
            || settings.Bindings.Select(b=>b.Action).Distinct().Count()!=settings.Bindings.Length
            || settings.Bindings.Any(b=>!Enum.IsDefined(b.Action) || b.Modifiers==0 || (b.Modifiers&~7u)!=0
                || b.Key==Keys.None || (b.Key&Keys.KeyCode)!=b.Key || b.Key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin))
        { error="Each action needs Ctrl, Alt, or Shift and a regular key.";return false; }
        if(settings.Bindings.Select(b=>(b.Modifiers,b.Key)).Distinct().Count()!=settings.Bindings.Length)
        { error="Each action must use a different shortcut.";return false; }
        return true;
    }
    public bool Configure(HotkeySettings settings,out string error)
    {
        if(!Validate(settings,out error)) return false;
        var old=registered.ToArray();Unregister();var added=new List<KeyBinding>();
        foreach(var binding in settings.Bindings)
        {
            if(Native.RegisterHotKey(Handle,(int)binding.Action,binding.Modifiers|0x4000,(uint)binding.Key)) { added.Add(binding);continue; }
            error=$"{SettingsDialog.ActionName(binding.Action)} ({binding}): {new Win32Exception(Marshal.GetLastWin32Error()).Message}";
            foreach(var b in added) Native.UnregisterHotKey(Handle,(int)b.Action);
            var restored=new List<KeyBinding>();
            foreach(var b in old) {
                if(Native.RegisterHotKey(Handle,(int)b.Action,b.Modifiers|0x4000,(uint)b.Key))restored.Add(b);
                else error+=$"\nCould not restore {b} either.";
            }
            registered=restored.ToArray();return false;
        }
        registered=added.ToArray();return true;
    }
    void Unregister(){foreach(var b in registered)Native.UnregisterHotKey(Handle,(int)b.Action);registered=[];}
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x312){Pressed?.Invoke((HotkeyAction)(int)m.WParam);m.Result=0;return;}
        if(m.Msg==0x11){m.Result=QueryEndSession?.Invoke()==false?0:1;return;}
        if(m.Msg==0x16&&m.WParam!=0){EndSession?.Invoke();m.Result=0;return;}
        base.WndProc(ref m);
    }
    public void Dispose(){Unregister();DestroyHandle();}
}
