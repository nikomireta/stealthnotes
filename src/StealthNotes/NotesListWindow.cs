namespace StealthNotes;

internal sealed class NotesListWindow : Form
{
    readonly AppState state;
    readonly ImageStore images;
    readonly Action newNote,settings;
    readonly Action<Guid> openNote;
    readonly TextBox search=new(){PlaceholderText="Search notes",AccessibleName="Search notes"};
    readonly ListBox list=new(){Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,IntegralHeight=false,AccessibleName="Notes list"};
    readonly Panel top=new(){Dock=DockStyle.Top},bottom=new(){Dock=DockStyle.Bottom};
    readonly Label brand=new(){Text="StealthNotes",AutoSize=true},footer=new(){AutoSize=false,TextAlign=ContentAlignment.MiddleLeft};
    readonly Button create=new(){Text="New note",AutoSize=true},globalOff=new(){Text="Turn off global ghost",Visible=false};
    readonly Button open=new(){Text="Open"},close=new(){Text="Close"},ghost=new(){Text="Ghost"};
    readonly ContextMenuStrip menu=new();
    readonly Font titleFont=new("Segoe UI",11,FontStyle.Bold),bodyFont=new("Segoe UI",10),smallFont=new("Segoe UI",9);
    readonly Icon? ownedIcon;
    Snapshot snapshot;
    Row[]? renderedRows;
    bool destroying;
    sealed record Row(Guid Id,string Title,string Preview,string Status,string Theme,Guid? ImageId,bool IsOpen,bool Ghost)
    {public override string ToString()=>Title+". "+Preview+". "+Status;}

    public NotesListWindow(AppState state,ImageStore images,Action newNote,Action<Guid> openNote,Action settings)
    {
        this.state=state;this.images=images;this.newNote=newNote;this.openNote=openNote;this.settings=settings;snapshot=state.Current;
        Text="StealthNotes — Notes list";StartPosition=FormStartPosition.CenterScreen;
        Size=new(440,580);MinimumSize=new(360,320);AutoScaleMode=AutoScaleMode.Dpi;
        BackColor=Color.FromArgb(248,248,250);Font=bodyFont;TopMost=false;ShowInTaskbar=true;
        ownedIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);if(ownedIcon is not null)Icon=ownedIcon;
        brand.Font=titleFont;top.Controls.AddRange([brand,create,search,globalOff]);
        bottom.Controls.AddRange([open,close,ghost,footer]);Controls.Add(list);Controls.Add(bottom);Controls.Add(top);
        create.Click+=(_,_)=>newNote();globalOff.Click+=(_,_)=>state.Dispatch(new ToggleGlobalGhost());
        open.Click+=(_,_)=>OpenSelected();close.Click+=(_,_)=>{if(Selected() is { } row)state.Dispatch(new CloseNote(row.Id));};
        ghost.Click+=(_,_)=>{if(Selected() is { } row)state.Dispatch(new ToggleNoteGhost(row.Id));};
        menu.Items.Add("Open",null,(_,_)=>OpenSelected());
        menu.Items.Add("Close",null,(_,_)=>{if(Selected() is { } row)state.Dispatch(new CloseNote(row.Id));});
        menu.Items.Add("Toggle ghost",null,(_,_)=>{if(Selected() is { } row)state.Dispatch(new ToggleNoteGhost(row.Id));});
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Delete note",null,(_,_)=>DeleteSelected());
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Hotkey settings…",null,(_,_)=>settings());
        list.ContextMenuStrip=menu;list.DrawItem+=DrawRow;
        list.DoubleClick+=(_,_)=>OpenSelected();list.SelectedIndexChanged+=(_,_)=>SelectionChanged();
        list.MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Right){int index=list.IndexFromPoint(e.Location);if(index>=0)list.SelectedIndex=index;}};
        search.TextChanged+=(_,_)=>Rebuild();SizeChanged+=(_,_)=>Arrange();
        images.ThumbnailReady+=ThumbnailReady;Arrange();Render(snapshot);
    }
    Row? Selected()=>list.SelectedItem as Row;
    void OpenSelected(){if(Selected() is { } row)openNote(row.Id);}
    void DeleteSelected()
    {
        if(Selected() is not { } row)return;
        if(MessageBox.Show(this,"Permanently delete this note and its images?","StealthNotes",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes)
            state.Dispatch(new DeleteNote(row.Id));
    }
    public void Render(Snapshot next)
    {
        snapshot=next;bool changed=globalOff.Visible!=next.GlobalGhost;globalOff.Visible=next.GlobalGhost;
        if(changed)Arrange();Rebuild();
    }
    void Rebuild()
    {
        Guid? selected=Selected()?.Id;int previousTop=list.Items.Count>0?list.TopIndex:0;
        string query=search.Text.Trim();var rows=new List<Row>();
        foreach(var n in snapshot.Notes.Values.OrderByDescending(n=>n.UpdatedAt).ThenBy(n=>n.Id)){
            bool redacted=snapshot.GlobalGhost||n.Ghost;
            if(query.Length>0&&!(redacted?"Ghost note":n.Text+" "+(n.Images.Count>0?"Image note":"New note")).Contains(query,StringComparison.OrdinalIgnoreCase))continue;
            string title=redacted?"Ghost note":string.IsNullOrWhiteSpace(n.Text)?(n.Images.Count>0?"Image note":"New note"):n.Text.Trim().Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries)[0];
            if(title.Length>80)title=title[..80];
            string preview=redacted?"":n.Text.Replace('\r',' ').Replace('\n',' ').Trim();if(preview.Length>180)preview=preview[..180];
            if(!redacted&&preview.Length==0)preview=n.Images.Count>0?n.Images.Count+" image"+(n.Images.Count==1?"":"s"):"Empty draft";
            string visible=!n.IsOpen?"Closed":snapshot.AllHidden||n.SessionHidden?"Hidden":"Open";
            rows.Add(new(n.Id,title,preview,visible+" · "+n.UpdatedAt.ToLocalTime().ToString("g"),n.Theme,
                redacted?null:n.Images.FirstOrDefault()?.Id,n.IsOpen,redacted));
        }
        if(renderedRows is not null&&rows.SequenceEqual(renderedRows))return; // Moving/resizing a note does not rebuild an unchanged list.
        renderedRows=rows.ToArray();
        // Native owner-draw list: metadata items only; controls and images are not created per note.
        list.BeginUpdate();try {
            list.Items.Clear();list.Items.AddRange(rows.Cast<object>().ToArray());
            int index=selected is null?-1:rows.FindIndex(r=>r.Id==selected);
            if(index>=0)list.SelectedIndex=index;else if(rows.Count>0)list.SelectedIndex=0;
            if(rows.Count>0)list.TopIndex=Math.Min(previousTop,rows.Count-1);
        }finally{list.EndUpdate();}
        footer.Text=rows.Count+" notes · v"+Application.ProductVersion.Split('+')[0];SelectionChanged();
    }
    void SelectionChanged(){var row=Selected();open.Enabled=row is not null;close.Enabled=row?.IsOpen==true;ghost.Enabled=row is not null&&GhostService.Supported;}
    void Arrange()
    {
        int S(int n)=>(int)Math.Round(n*DeviceDpi/96d);
        top.Height=S(snapshot.GlobalGhost?116:86);bottom.Height=S(76);
        brand.SetBounds(S(16),S(16),S(220),S(26));create.SetBounds(Math.Max(S(225),ClientSize.Width-S(110)),S(12),S(94),S(30));
        search.SetBounds(S(16),S(50),Math.Max(S(100),ClientSize.Width-S(32)),S(28));
        globalOff.SetBounds(S(16),S(84),Math.Max(S(100),ClientSize.Width-S(32)),S(26));
        open.SetBounds(S(16),S(8),S(72),S(28));close.SetBounds(S(96),S(8),S(72),S(28));ghost.SetBounds(S(176),S(8),S(72),S(28));
        footer.SetBounds(S(16),S(42),Math.Max(0,ClientSize.Width-S(32)),S(24));footer.Font=smallFont;
        list.ItemHeight=S(94);
    }
    void DrawRow(object? sender,DrawItemEventArgs e)
    {
        if(e.Index<0||e.Index>=list.Items.Count||list.Items[e.Index] is not Row row)return;
        bool selected=(e.State&DrawItemState.Selected)!=0;int S(int n)=>(int)Math.Round(n*DeviceDpi/96d);
        using var background=new SolidBrush(selected?Color.FromArgb(231,237,248):Color.White);e.Graphics.FillRectangle(background,e.Bounds);
        var palette=Themes.Get(row.Theme);using var accent=new SolidBrush(palette.Header);
        e.Graphics.FillRectangle(accent,e.Bounds.X+S(8),e.Bounds.Y+S(12),S(4),e.Bounds.Height-S(24));
        int right=e.Bounds.Right-S(14),left=e.Bounds.X+S(24);
        if(row.ImageId is { } imageId){
            var image=images.Thumbnail(imageId);var bounds=new Rectangle(right-S(64),e.Bounds.Y+S(14),S(60),S(60));
            if(image is not null)e.Graphics.DrawImage(image,ImageStore.Fit(image.Size,bounds));right-=S(76);
        }
        var flags=TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics,row.Title,titleFont,new Rectangle(left,e.Bounds.Y+S(12),Math.Max(0,right-left),S(24)),Color.FromArgb(42,43,49),flags);
        TextRenderer.DrawText(e.Graphics,row.Preview,bodyFont,new Rectangle(left,e.Bounds.Y+S(38),Math.Max(0,right-left),S(22)),Color.FromArgb(91,93,104),flags);
        TextRenderer.DrawText(e.Graphics,row.Status,smallFont,new Rectangle(left,e.Bounds.Y+S(65),Math.Max(0,right-left),S(20)),Color.FromArgb(115,117,128),flags);
        if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
    }
    void ThumbnailReady(Guid id){if(list.Items.Cast<Row>().Any(r=>r.ImageId==id))list.Invalidate();}
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
    protected override void Dispose(bool disposing){if(disposing){images.ThumbnailReady-=ThumbnailReady;menu.Dispose();titleFont.Dispose();bodyFont.Dispose();smallFont.Dispose();ownedIcon?.Dispose();}base.Dispose(disposing);}
}
