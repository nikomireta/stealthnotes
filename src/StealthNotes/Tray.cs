namespace StealthNotes;

internal sealed class Tray : IDisposable
{
    readonly NotifyIcon icon;
    readonly Icon ownedIcon;
    readonly ContextMenuStrip menu=new();
    readonly ToolStripMenuItem ghost;
    DateTimeOffset lastWarning=DateTimeOffset.MinValue;
    public Tray(Action newNote,Action show,Action hide,Action toggleGhost,Action settings,Action quit)
    {
        ownedIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath)??(Icon)SystemIcons.Application.Clone();
        menu.Items.Add("New note",null,(_,_)=>newNote());
        menu.Items.Add("Show all notes",null,(_,_)=>show());
        menu.Items.Add("Hide all notes",null,(_,_)=>hide());
        ghost=new("Ghost all notes") {Enabled=GhostService.Supported};ghost.Click+=(_,_)=>toggleGhost();menu.Items.Add(ghost);
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Hotkey settings…",null,(_,_)=>settings());
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Quit",null,(_,_)=>quit());
        icon=new NotifyIcon {Icon=ownedIcon,Text="StealthNotes",Visible=true,ContextMenuStrip=menu};
        icon.DoubleClick+=(_,_)=>show();
    }
    public void Render(Snapshot state){ghost.Checked=state.GlobalGhost;icon.Text=state.GlobalGhost?"StealthNotes · global ghost":"StealthNotes";}
    public void Warning(string text)
    {
        if(DateTimeOffset.UtcNow-lastWarning<TimeSpan.FromSeconds(5))return;lastWarning=DateTimeOffset.UtcNow;
        icon.ShowBalloonTip(6000,"StealthNotes",text,ToolTipIcon.Warning);
    }
    public void Dispose(){icon.Visible=false;icon.Dispose();menu.Dispose();ownedIcon.Dispose();}
}
