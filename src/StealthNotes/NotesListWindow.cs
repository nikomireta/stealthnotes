namespace StealthNotes;

internal sealed class NotesListWindow : Form
{
    readonly AppState state;
    readonly Action newNote;
    readonly Action<Guid> openNote;
    readonly TextBox search=new(){PlaceholderText="Search notes",AccessibleName="Search notes"};
    readonly NoteCards list;
    readonly Panel top=new(){Dock=DockStyle.Top};
    readonly Label brand=new(){Text="StealthNotes",AutoSize=true};
    readonly Button create=new(){Text="New note"},globalOff=new(){Text="Turn off global ghost",Visible=false};
    readonly ContextMenuStrip menu=new(){ShowImageMargin=false};
    readonly ToolStripMenuItem close,ghost;
    readonly Font titleFont=new("Segoe UI",11,FontStyle.Bold),bodyFont=new("Segoe UI",10);
    readonly Icon? ownedIcon;
    Snapshot snapshot;
    bool destroying;

    public NotesListWindow(AppState state,ImageStore images,Action newNote,Action<Guid> openNote,Action settings)
    {
        this.state=state;this.newNote=newNote;this.openNote=openNote;snapshot=state.Current;
        Text="StealthNotes - Notes list";StartPosition=FormStartPosition.CenterScreen;
        Size=new(440,580);MinimumSize=new(360,320);AutoScaleMode=AutoScaleMode.Dpi;
        BackColor=Color.FromArgb(248,248,250);Font=bodyFont;ShowInTaskbar=true;
        ownedIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);if(ownedIcon is not null)Icon=ownedIcon;
        list=new(images){Dock=DockStyle.Fill,BackColor=BackColor,Font=bodyFont};
        brand.Font=titleFont;top.Controls.AddRange([brand,create,search,globalOff]);Controls.Add(list);Controls.Add(top);
        create.Click+=(_,_)=>newNote();globalOff.Click+=(_,_)=>state.Dispatch(new ToggleGlobalGhost());
        menu.Items.Add("Open note",null,(_,_)=>OpenSelected());
        close=new("Close note",null,(_,_)=>{if(list.Selected() is { } row)state.Dispatch(new CloseNote(row.Id));});menu.Items.Add(close);
        ghost=new("Ghost note",null,(_,_)=>{if(list.Selected() is { } row)state.Dispatch(new ToggleNoteGhost(row.Id));});menu.Items.Add(ghost);
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Delete note",null,(_,_)=>DeleteSelected());
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Hotkey settings...",null,(_,_)=>settings());
        menu.Opening+=(_,e)=>{e.Cancel=list.Selected() is null;SelectionChanged();};
        list.ContextMenuStrip=menu;list.OpenRequested+=openNote;list.SelectionChanged+=SelectionChanged;
        search.TextChanged+=(_,_)=>Rebuild();SizeChanged+=(_,_)=>Arrange();Arrange();Render(snapshot);
    }
    void OpenSelected(){if(list.Selected() is { } row)openNote(row.Id);}
    void DeleteSelected()
    {
        if(list.Selected() is not { } row)return;
        if(MessageBox.Show(this,"Permanently delete this note and its images?","StealthNotes",MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes)state.Dispatch(new DeleteNote(row.Id));
    }
    public void Render(Snapshot next)
    {
        snapshot=next;globalOff.Visible=next.GlobalGhost;Arrange();Rebuild();
    }
    void Rebuild()
    {
        string query=search.Text.Trim();var rows=new List<NoteCard>();
        foreach(var note in snapshot.Notes.Values.OrderByDescending(n=>n.UpdatedAt).ThenBy(n=>n.Id)){
            bool redacted=snapshot.GlobalGhost||note.Ghost;
            string searchable=redacted?"Ghost note":note.Text+" "+(note.Images.Count>0?"Image note":"New note");
            if(query.Length>0&&!searchable.Contains(query,StringComparison.OrdinalIgnoreCase))continue;
            string text=redacted?"Ghost note":string.IsNullOrWhiteSpace(note.Text)?(note.Images.Count>0?"Image note":"Empty note"):note.Text.Trim();
            // Search uses the full document; painting/accessibility needs only the short preview.
            if(text.Length>2048)text=text[..2048];
            rows.Add(new(note.Id,text,note.Theme,redacted?null:note.Images.FirstOrDefault(),note.IsOpen,note.Ghost));
        }
        list.SetRows(rows.ToArray());SelectionChanged();
    }
    void SelectionChanged()
    {
        var row=list.Selected();close.Enabled=row?.IsOpen==true;
        ghost.Enabled=row is not null&&GhostService.Supported;ghost.Checked=row?.Ghost==true;
    }
    void Arrange()
    {
        int S(int n)=>(int)Math.Round(n*DeviceDpi/96d);
        top.Height=S(snapshot.GlobalGhost?116:86);
        brand.SetBounds(S(16),S(16),S(180),S(26));create.SetBounds(ClientSize.Width-S(110),S(12),S(94),S(30));
        search.SetBounds(S(16),S(50),Math.Max(S(100),ClientSize.Width-S(32)),S(28));
        globalOff.SetBounds(S(16),S(84),Math.Max(S(100),ClientSize.Width-S(32)),S(26));
    }
    protected override void OnDpiChanged(DpiChangedEventArgs e){base.OnDpiChanged(e);Arrange();}
    public void ShowList(){if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Show();Activate();Native.SetForegroundWindow(Handle);}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(keyData==(Keys.Control|Keys.F)){search.Focus();search.SelectAll();return true;}
        if(keyData==(Keys.Control|Keys.N)){newNote();return true;}
        if(keyData==Keys.Enter&&list.Focused){OpenSelected();return true;}
        if(keyData==Keys.Delete&&list.Focused){DeleteSelected();return true;}
        if(keyData==Keys.Escape){if(search.TextLength>0)search.Clear();else Hide();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    protected override void OnFormClosing(FormClosingEventArgs e){if(!destroying&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}base.OnFormClosing(e);}
    public void Destroy(){destroying=true;Close();Dispose();}
    protected override void Dispose(bool disposing){if(disposing){menu.Dispose();titleFont.Dispose();bodyFont.Dispose();ownedIcon?.Dispose();}base.Dispose(disposing);}
}
