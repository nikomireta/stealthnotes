namespace StealthNotes;

internal sealed class NotesContext : ApplicationContext
{
    readonly AppState state;
    readonly Persistence persistence;
    readonly WindowManager windows;
    readonly Hotkeys hotkeys;
    readonly Tray tray;
    readonly ImageStore images;
    NotesListWindow? notesList;
    readonly Control dispatcher=new();
    readonly RegisteredWaitHandle? wait;
    SettingsDialog? settings;
    bool quitting,disposed;

    public NotesContext(NoteStore store,EventWaitHandle openEvent)
    {
        _=dispatcher.Handle;
        var ui=SynchronizationContext.Current??new WindowsFormsSynchronizationContext();
        var initial=store.Load();
        var fitted=initial.Notes.Values.Select(n=>{var r=WindowManager.Fit(n.Bounds);return n with {X=r.X,Y=r.Y,W=r.Width,H=r.Height};});
        state=new(initial with {Notes=Snapshot.Freeze(fitted)});
        images=new(store.DatabasePath,ui);
        persistence=new(store,images,ui);
        hotkeys=new();
        windows=new(state,new GhostService(),images);
        tray=new(NewNote,ShowList,ActivateNotes,ShowAll,()=>state.Dispatch(new SetAllHidden(true)),()=>ToggleGlobalGhost(),OpenSettings,()=>Quit(false));
        windows.NewRequested+=NewNote;
        windows.ListRequested+=ShowList;
        windows.GhostFailed+=error=>{LocalLog.Error("Ghost failed",new InvalidOperationException(error));tray.Warning("Could not enable ghost mode. "+error);};
        state.Changed+=images.SetLiveReferences;state.Changed+=persistence.Schedule;state.Changed+=tray.Render;state.Changed+=RenderList;
        persistence.StatusChanged+=e=>{
            windows.SetSaveError(e is not null);
            if(e is not null)tray.Warning("Could not save your notes. Resolve the disk issue before closing the app. See errors.log.");
        };
        hotkeys.Pressed+=OnHotkey;
        hotkeys.QueryEndSession=()=>persistence.Flush(state.Current);
        hotkeys.EndSession=()=>Quit(true);
        bool configured=hotkeys.Configure(state.Current.Hotkeys,out string keyError);
        windows.Render(state.Current);tray.Render(state.Current);
        images.SetLiveReferences(state.Current);persistence.Schedule(state.Current);ActivateNotes();
        wait=ThreadPool.RegisterWaitForSingleObject(openEvent,(_,timedOut)=>{
            if(disposed||dispatcher.IsDisposed)return;
            try { dispatcher.BeginInvoke((Action)ActivateNotes); }
            catch(InvalidOperationException){ /* Application is shutting down. */ }
        },null,Timeout.Infinite,false);
        if(!configured)dispatcher.BeginInvoke((Action)(()=>{
            MessageBox.Show("Could not register hotkeys. Choose shortcuts that are not used by another app.\n\n"+keyError,"StealthNotes",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            OpenSettings();
        }));
    }
    void NewNote()
    {
        var screen=Screen.FromPoint(Cursor.Position).WorkingArea;
        int offset=(state.Current.Notes.Count%8)*22;
        var bounds=WindowManager.Fit(new Rectangle(screen.Left+70+offset,screen.Top+70+offset,340,300));
        var now=DateTimeOffset.UtcNow;
        var note=new Note(Guid.NewGuid(),"","yellow",bounds.X,bounds.Y,bounds.Width,bounds.Height,false,1,.35,now,now);
        state.Dispatch(new AddNote(note));windows.Activate(note.Id);
    }
    void RenderList(Snapshot next)=>notesList?.Render(next);
    void ShowList()
    {
        notesList??=new(state,images,NewNote,OpenNote,OpenSettings);
        notesList.ShowList();
    }
    void ActivateNotes()
    {
        if(quitting||disposed)return;
        if(state.Current.Notes.Values.Any(n=>n.IsOpen)){
            state.Dispatch(new SetAllHidden(false));windows.Render(state.Current,true);windows.ActivateRecent();
        }else if(state.Current.Notes.Values.OrderByDescending(n=>n.UpdatedAt).ThenBy(n=>n.Id).FirstOrDefault() is { } latest)
            OpenNote(latest.Id);
        else NewNote();
    }
    void OpenNote(Guid id){state.Dispatch(new OpenNote(id));windows.Render(state.Current,true);windows.Activate(id);}
    void ShowAll(){state.Dispatch(new OpenAllNotes());windows.Render(state.Current,true);}
    void ToggleGlobalGhost()
    {
        if(!GhostService.Supported){tray.Warning("Capture exclusion requires Windows 10 version 2004 or later.");return;}
        state.Dispatch(new ToggleGlobalGhost());
    }
    void OnHotkey(HotkeyAction action)
    {
        if(quitting)return;
        switch(action){
            case HotkeyAction.NewNote:NewNote();break;
            case HotkeyAction.ToggleGlobalGhost:ToggleGlobalGhost();break;
            case HotkeyAction.ToggleVisibility:
                bool anyVisible=!state.Current.AllHidden&&state.Current.Notes.Values.Any(n=>n.IsOpen&&!n.SessionHidden);
                state.Dispatch(new SetAllHidden(anyVisible));break;
            case HotkeyAction.ToggleNoteGhost:
                if(!GhostService.Supported){tray.Warning("Capture exclusion is not supported on this version of Windows.");break;}
                if(windows.Target() is { } target)state.Dispatch(new ToggleNoteGhost(target));break;
            case HotkeyAction.OpacityUp:case HotkeyAction.OpacityDown:
                if(windows.Target() is { } id){var note=state.Current.Notes[id];double delta=action==HotkeyAction.OpacityUp?.05:-.05;
                    state.Dispatch(new ChangeOpacity(id,note.EffectiveOpacity(state.Current.GlobalGhost)+delta));}break;
        }
    }
    void OpenSettings()
    {
        if(settings is {IsDisposed:false}){settings.Activate();return;}
        settings=new(state.Current.Hotkeys.Copy(),proposed=>{
            if(!hotkeys.Configure(proposed,out var error))return error;
            state.Dispatch(new ChangeHotkeys(proposed));return null;
        });
        settings.FormClosed+=(_,_)=>{settings?.Dispose();settings=null;};settings.Show();
    }
    void Quit(bool sessionEnd)
    {
        if(quitting)return;
        if(!persistence.Flush(state.Current)&&!sessionEnd){
            var answer=MessageBox.Show("Your latest changes have not been saved. Quit anyway and lose those changes?","StealthNotes",
                MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2);
            if(answer!=DialogResult.Yes)return;
        }
        quitting=true;ExitThread();
    }
    protected override void ExitThreadCore(){Dispose(true);base.ExitThreadCore();}
    protected override void Dispose(bool disposing)
    {
        if(disposing&&!disposed){
            disposed=true;wait?.Unregister(null);settings?.Dispose();hotkeys.Dispose();windows.Dispose();notesList?.Destroy();tray.Dispose();
            state.Changed-=images.SetLiveReferences;state.Changed-=RenderList;
            state.Changed-=persistence.Schedule;state.Changed-=tray.Render;persistence.Dispose();images.Dispose();dispatcher.Dispose();
        }
        base.Dispose(disposing);
    }
}
