namespace StealthNotes;

internal sealed class WindowManager : IDisposable
{
    readonly AppState state;
    readonly GhostService ghosts;
    readonly Dictionary<Guid,NoteWindow> windows=[];
    Guid? lastActive;
    public event Action? NewRequested;
    public event Action<string>? GhostFailed;
    public WindowManager(AppState state,GhostService ghosts){this.state=state;this.ghosts=ghosts;state.Changed+=Render;}
    public static Rectangle Fit(Rectangle rectangle)
    {
        var area=Screen.FromRectangle(rectangle).WorkingArea;
        int w=Math.Clamp(rectangle.Width,Math.Min(248,area.Width),area.Width);
        int h=Math.Clamp(rectangle.Height,Math.Min(180,area.Height),area.Height);
        return new(Math.Clamp(rectangle.X,area.Left,area.Right-w),Math.Clamp(rectangle.Y,area.Top,area.Bottom-h),w,h);
    }
    public void Render(Snapshot snapshot)
    {
        foreach(var id in windows.Keys.Except(snapshot.Notes.Keys).ToArray()){windows[id].Destroy();windows.Remove(id);}
        foreach(var note in snapshot.Notes.Values){
            if(!windows.TryGetValue(note.Id,out var window)){
                window=new(note,state,ghosts);window.NewRequested+=()=>NewRequested?.Invoke();
                window.BecameActive+=id=>lastActive=id;window.GhostFailed+=error=>GhostFailed?.Invoke(error);windows.Add(note.Id,window);
            }
            window.Render(note,snapshot.GlobalGhost,snapshot.AllHidden);
        }
    }
    public Guid? Target()
    {
        Native.GetCursorPos(out var point);
        var byHandle=windows.Values.Where(w=>w.Visible&&w.IsHandleCreated).ToDictionary(w=>w.Handle);
        for(nint h=Native.GetTopWindow(0);h!=0;h=Native.GetWindow(h,2))
            if(byHandle.TryGetValue(h,out var w)&&w.Bounds.Contains(point))return w.NoteId;
        if(lastActive is { } id&&state.Current.Notes.ContainsKey(id))return id;
        var last=state.Current.Notes.Keys.LastOrDefault();return last==Guid.Empty?null:last;
    }
    public void Activate(Guid id)
    {
        lastActive=id;
        if(windows.TryGetValue(id,out var window)&&window.Visible&&!state.Current.GlobalGhost&&!state.Current.Notes[id].Ghost){
            window.Activate();Native.SetForegroundWindow(window.Handle);
        }
    }
    public void Dispose(){state.Changed-=Render;foreach(var w in windows.Values.ToArray())w.Destroy();windows.Clear();}
}
