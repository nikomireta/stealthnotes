namespace StealthNotes;

internal sealed class WindowManager : IDisposable
{
    readonly AppState state;
    readonly GhostService ghosts;
    readonly ImageStore images;
    readonly Dictionary<Guid,NoteWindow> windows=[];
    Snapshot? rendered;
    bool saveFailed;
    Guid? lastActive;
    public event Action? NewRequested;
    public event Action? ListRequested;
    public event Action<string>? GhostFailed;
    public WindowManager(AppState state,GhostService ghosts,ImageStore images){this.state=state;this.ghosts=ghosts;this.images=images;state.Changed+=Render;}
    public static Rectangle Fit(Rectangle rectangle)
    {
        var area=Screen.FromRectangle(rectangle).WorkingArea;
        int w=Math.Clamp(rectangle.Width,Math.Min(248,area.Width),area.Width);
        int h=Math.Clamp(rectangle.Height,Math.Min(180,area.Height),area.Height);
        return new(Math.Clamp(rectangle.X,area.Left,area.Right-w),Math.Clamp(rectangle.Y,area.Top,area.Bottom-h),w,h);
    }
    public void Render(Snapshot snapshot)=>Render(snapshot,false);
    public void Render(Snapshot snapshot,bool force)
    {
        bool globalChanged=rendered is null || rendered.GlobalGhost!=snapshot.GlobalGhost || rendered.AllHidden!=snapshot.AllHidden;
        foreach(var id in windows.Keys.Where(id=>!snapshot.Notes.TryGetValue(id,out var n)||!n.IsOpen).ToArray()){windows[id].Destroy();windows.Remove(id);}
        foreach(var note in snapshot.Notes.Values.Where(n=>n.IsOpen)){
            if(!windows.TryGetValue(note.Id,out var window)){
                window=new(note,state,ghosts,images);window.NewRequested+=()=>NewRequested?.Invoke();window.ListRequested+=()=>ListRequested?.Invoke();
                window.BecameActive+=id=>lastActive=id;window.GhostFailed+=error=>GhostFailed?.Invoke(error);windows.Add(note.Id,window);
                window.SetSaveError(saveFailed);
            }
            if(force || globalChanged || rendered is null || !rendered.Notes.TryGetValue(note.Id,out var previous) || previous!=note)
                window.Render(note,snapshot.GlobalGhost,snapshot.AllHidden);
        }
        rendered=snapshot;
    }
    public void SetSaveError(bool failed)
    {
        if(saveFailed==failed)return;saveFailed=failed;
        foreach(var window in windows.Values)window.SetSaveError(failed);
    }
    public Guid? Target()
    {
        Native.GetCursorPos(out var point);
        var byHandle=windows.Values.Where(w=>w.Visible&&w.IsHandleCreated).ToDictionary(w=>w.Handle);
        for(nint h=Native.GetTopWindow(0);h!=0;h=Native.GetWindow(h,2))
            if(byHandle.TryGetValue(h,out var w)&&w.Bounds.Contains(point))return w.NoteId;
        if(lastActive is { } id&&state.Current.Notes.TryGetValue(id,out var note)&&note.IsOpen)return id;
        return state.Current.Notes.Values.LastOrDefault(n=>n.IsOpen)?.Id;
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
