using System.Drawing.Drawing2D;

namespace StealthNotes;

internal sealed class NoteWindow : Form
{
    readonly AppState state;
    readonly GhostService ghosts;
    readonly ImageStore images;
    readonly NoteEditor editor;
    readonly BufferedPanel header,paper,toolbar;
    readonly NoteGallery gallery;
    readonly List<Button> formattingButtons=[];
    readonly List<Font> formattingFonts=[];
    readonly Label title;
    readonly GlyphButton add,ghost,more,hide;
    readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem ghostItem;
    readonly TrackBar opacitySlider;
    readonly ToolStripLabel opacityLabel;
    readonly ToolTip tips=new() {ShowAlways=true};
    readonly Dictionary<string,ToolStripMenuItem> themeItems=[];
    Note note;
    bool applying,effectiveGhost,ready,destroying,sliderUpdating,lastGlobal,hasRendered,saveFailed,layingOut;
    bool? lastRequested;
    double lastOpacity=-1;
    string? lastError;
    Font? headerFont;
    Size roundedSize;
    int roundedDpi;
    public Guid NoteId=>note.Id;
    public event Action? NewRequested;
    public event Action? ListRequested;
    public event Action<Guid>? BecameActive;
    public event Action<string>? GhostFailed;

    public NoteWindow(Note initial,AppState state,GhostService ghosts,ImageStore images)
    {
        note=initial;this.state=state;this.ghosts=ghosts;this.images=images;
        Text=$"StealthNotes · {initial.Id.ToString("N")[..8]}";
        AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;
        StartPosition=FormStartPosition.Manual;Bounds=initial.Bounds;
        TopMost=true;ShowInTaskbar=false;MinimumSize=new(248,180);
        SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);
        header=new BufferedPanel {Dock=DockStyle.Top,Height=36,TabIndex=1};
        title=new Label {Text="Note",AutoSize=false,TextAlign=ContentAlignment.MiddleLeft};
        add=HeaderButton(NoteGlyph.Add,"New note");add.TabIndex=0;
        ghost=HeaderButton(NoteGlyph.Ghost,"Ghost this note (click-through)");ghost.TabIndex=1;
        more=HeaderButton(NoteGlyph.More,"Color and opacity");more.TabIndex=2;
        hide=HeaderButton(NoteGlyph.Close,"Close note (discard empty draft)");hide.TabIndex=3;
        header.Controls.AddRange([title,add,ghost,more,hide]);
        editor=new NoteEditor {Dock=DockStyle.Fill,Margin=Padding.Empty,TabIndex=0};
        paper=new BufferedPanel {Dock=DockStyle.Fill,Padding=new(16),TabIndex=0};paper.Controls.Add(editor);
        gallery=new(images){Dock=DockStyle.Bottom,Visible=false};paper.Controls.Add(gallery);
        gallery.ExpansionChanged+=()=>{editor.Visible=!gallery.Expanded;gallery.Dock=gallery.Expanded?DockStyle.Fill:DockStyle.Bottom;paper.PerformLayout();};
        gallery.RemoveRequested+=id=>{
            if(MessageBox.Show(this,"Remove this image?","StealthNotes",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes)
                state.Dispatch(new RemoveImage(NoteId,id));
        };
        toolbar=new BufferedPanel {Dock=DockStyle.Bottom,Height=36,TabIndex=2};
        AddFormatButton("B","Bold (Ctrl+B)",FontStyle.Bold,()=>editor.ToggleStyle(1));
        AddFormatButton("I","Italic (Ctrl+I)",FontStyle.Italic,()=>editor.ToggleStyle(2));
        AddFormatButton("U","Underline (Ctrl+U)",FontStyle.Underline,()=>editor.ToggleStyle(4));
        AddFormatButton("S","Strikethrough (Ctrl+T)",FontStyle.Strikeout,()=>editor.ToggleStyle(8));
        AddFormatButton("•","Bullets (Ctrl+Shift+L)",FontStyle.Regular,()=>editor.ToggleBullets());
        var imageButton=new GlyphButton(NoteGlyph.Image,"Add image");tips.SetToolTip(imageButton,"Add image");
        imageButton.Click+=async(_,_)=>await ChooseImages();formattingButtons.Add(imageButton);toolbar.Controls.Add(imageButton);
        Controls.Add(paper);Controls.Add(toolbar);Controls.Add(header);Padding=new(1,1,1,8);
        menu=new ContextMenuStrip {ShowImageMargin=false};
        menu.Items.Add("Notes list",null,(_,_)=>ListRequested?.Invoke());
        menu.Items.Add("Undo",null,(_,_)=>editor.UndoEdit());menu.Items.Add("Redo",null,(_,_)=>editor.RedoEdit());
        menu.Items.Add(new ToolStripSeparator());
        ghostItem=new("Ghost this note") {CheckOnClick=false,Enabled=GhostService.Supported};
        ghostItem.Click+=(_,_)=>state.Dispatch(new ToggleNoteGhost(NoteId));
        menu.Items.Add(ghostItem);
        var colors=new ToolStripMenuItem("Color");
        foreach(var palette in Themes.All){
            var item=new ToolStripMenuItem("●  "+palette.Name) {ForeColor=palette.Id=="dark"?Color.DimGray:ControlPaint.Dark(palette.Header)};
            item.Click+=(_,_)=>state.Dispatch(new ChangeTheme(NoteId,palette.Id));colors.DropDownItems.Add(item);themeItems[palette.Id]=item;
        }
        menu.Items.Add(colors);menu.Items.Add(new ToolStripSeparator());
        opacityLabel=new ToolStripLabel("Opacity");menu.Items.Add(opacityLabel);
        opacitySlider=new TrackBar {Minimum=10,Maximum=100,TickStyle=TickStyle.None,AutoSize=false,Size=new(190,30),SmallChange=5,LargeChange=10,AccessibleName="Note opacity"};
        menu.Items.Add(new ToolStripControlHost(opacitySlider) {Margin=new(6,0,6,6),AutoSize=false,Size=new(190,32)});
        opacitySlider.ValueChanged+=(_,_)=>{if(!sliderUpdating)state.Dispatch(new ChangeOpacity(NoteId,opacitySlider.Value/100d));};
        menu.Items.Add(new ToolStripSeparator());
        var delete=new ToolStripMenuItem("Delete note") {ForeColor=Color.Firebrick};
        delete.Click+=(_,_)=>{
            if(note.HasContent&&MessageBox.Show(this,"Permanently delete this note?","StealthNotes",MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            state.Dispatch(new DeleteNote(NoteId));
        };
        menu.Items.Add(delete);
        more.Click+=(_,_)=>menu.Show(more,new Point(0,more.Height));
        add.Click+=(_,_)=>NewRequested?.Invoke();
        ghost.Click+=(_,_)=>state.Dispatch(new ToggleNoteGhost(NoteId));
        hide.Click+=(_,_)=>state.Dispatch(new CloseNote(NoteId));
        foreach(var c in new Control[]{header,title})c.MouseDown+=DragHeader;
        editor.DocumentChanged+=()=>{if(!applying)state.Dispatch(new UpdateDocument(NoteId,editor.Text,editor.Rtf??""));};
        editor.NewRequested+=()=>NewRequested?.Invoke();editor.ListRequested+=()=>ListRequested?.Invoke();
        editor.ImagePasted+=async image=>await ImportClipboard(image);
        editor.FilesPasted+=async files=>await ImportFiles(files);
        editor.FormattingChanged+=UpdateFormatting;
        AllowDrop=true;editor.AllowDrop=true;
        foreach(var control in new Control[]{this,editor,paper,gallery}){
            control.AllowDrop=true;
            control.DragEnter+=(_,e)=>{if(!effectiveGhost&&e.Data?.GetDataPresent(DataFormats.FileDrop)==true)e.Effect=DragDropEffects.Copy;};
            control.DragDrop+=async(_,e)=>{if(!effectiveGhost&&e.Data?.GetData(DataFormats.FileDrop) is string[] files)await ImportFiles(files);};
        }
        LocationChanged+=(_,_)=>BoundsChanged();SizeChanged+=(_,_)=>{LayoutHeader();BoundsChanged();};
        Activated+=(_,_)=>BecameActive?.Invoke(NoteId);
        ready=true;LayoutHeader();
    }

    void AddFormatButton(string text,string name,FontStyle style,Action action)
    {
        var font=new Font("Segoe UI",10,style);formattingFonts.Add(font);
        var button=new Button {Text=text,AccessibleName=name,FlatStyle=FlatStyle.Flat,TabStop=true,Font=font,Cursor=Cursors.Hand};
        button.FlatAppearance.BorderSize=0;tips.SetToolTip(button,name);button.Click+=(_,_)=>action();
        formattingButtons.Add(button);toolbar.Controls.Add(button);
    }
    void UpdateFormatting()
    {
        if(destroying||formattingButtons.Count<6)return;
        var palette=Themes.Get(note.Theme);
        for(int i=0;i<5;i++){
            bool active=i==4?editor.SelectionBullet:editor.StyleActive(1u<<i);
            Color color=active?NoteChrome.Blend(palette.Header,palette.Ink,.12f):palette.Paper;
            if(formattingButtons[i].BackColor!=color)formattingButtons[i].BackColor=color;
        }
    }
    async Task ChooseImages()
    {
        using var dialog=new OpenFileDialog {Title="Add images",Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",Multiselect=true};
        if(dialog.ShowDialog(this)==DialogResult.OK)await ImportFiles(dialog.FileNames);
    }
    async Task ImportFiles(string[] files)
    {
        foreach(string path in files){
            if(destroying)return;
            try{Attach(await images.ImportFile(path));}
            catch(OperationCanceledException){return;}
            catch(Exception e){if(!destroying)MessageBox.Show(this,e.Message,"Could not add image",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
    }
    async Task ImportClipboard(Image image)
    {
        try{Attach(await images.ImportClipboard(image));}
        catch(OperationCanceledException){}
        catch(Exception e){if(!destroying)MessageBox.Show(this,e.Message,"Could not paste image",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    void Attach(NoteImage image)
    {
        if(destroying||!state.Current.Notes.ContainsKey(NoteId)){images.Discard(image.Id);return;}
        state.Dispatch(new AddImage(NoteId,image));
    }

    GlyphButton HeaderButton(NoteGlyph glyph,string tooltip)
    {
        var button=new GlyphButton(glyph,tooltip);tips.SetToolTip(button,tooltip);return button;
    }
    void DragHeader(object? sender,MouseEventArgs e)
    { if(e.Button==MouseButtons.Left&&!effectiveGhost){Native.ReleaseCapture();Native.SendMessageW(Handle,0xa1,2,0);} }
    void BoundsChanged()
    {
        if(ready&&!applying&&!destroying&&WindowState==FormWindowState.Normal&&IsHandleCreated)
            state.Dispatch(new MoveNote(NoteId,Bounds));
    }
    void LayoutHeader()
    {
        if(!ready||destroying||IsDisposed||layingOut)return;
        layingOut=true;SuspendLayout();
        try {
        int S(int n)=>(int)Math.Round(n*DeviceDpi/96d);
        header.Height=S(36);toolbar.Height=S(36);Padding=new(1,1,1,S(8));MinimumSize=new(S(248),S(180));
        int width=S(30),height=S(30),gap=S(2),right=ClientSize.Width-2-S(5);
        foreach(var b in new[]{hide,more,ghost}){right-=width;b.SetBounds(right,S(3),width,height);right-=gap;}
        add.SetBounds(S(5),S(3),width,height);title.SetBounds(S(43),S(3),Math.Max(0,right-S(47)),height);
        paper.Padding=new(S(16));
        for(int i=0;i<formattingButtons.Count;i++)formattingButtons[i].SetBounds(S(8+i*32),S(3),S(30),S(30));
        if(!gallery.Expanded)gallery.Height=Math.Min(S(96),Math.Max(S(48),ClientSize.Height-S(36+36+8+32+44)));
        float headerSize=(float)(12*DeviceDpi/96d);
        if(headerFont is null||Math.Abs(headerFont.Size-headerSize)>.1){
            var oldHeader=headerFont;
            headerFont=new("Segoe UI",(float)(12*DeviceDpi/96d),FontStyle.Regular,GraphicsUnit.Pixel);
            title.Font=headerFont;oldHeader?.Dispose();
        }
        UpdateRoundedRegion();
        } finally {ResumeLayout(true);layingOut=false;}
    }
    void UpdateRoundedRegion()
    {
        if(ClientSize.Width<=0||ClientSize.Height<=0||(roundedSize==ClientSize&&roundedDpi==DeviceDpi))return;
        using var path=NoteChrome.RoundedPath(new RectangleF(0,0,ClientSize.Width,ClientSize.Height),8*DeviceDpi/96f);
        var previous=Region;Region=new Region(path);previous?.Dispose();roundedSize=ClientSize;roundedDpi=DeviceDpi;
    }
    public void SetSaveError(bool failed)
    {
        if(saveFailed==failed)return;saveFailed=failed;UpdateTitle();
    }
    void UpdateTitle()
    {
        var palette=Themes.Get(note.Theme);
        title.Text=saveFailed?"Not saved":"Note";
        title.ForeColor=saveFailed?(note.Theme=="dark"?Color.Salmon:Color.Firebrick):NoteChrome.Blend(palette.Header,palette.Ink,.75f);
        tips.SetToolTip(title,saveFailed?"Could not save your latest changes. Check disk space and permissions. See errors.log.":"Drag to move this note");
    }
    public void Render(Note next,bool globalGhost,bool hidden)
    {
        applying=true;
        try {
            var previous=note;bool previousGlobal=lastGlobal;string? previousError=lastError;
            note=next;lastGlobal=globalGhost;
            if(!hidden&&!next.SessionHidden&&WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;
            if(Bounds!=next.Bounds&&WindowState==FormWindowState.Normal)Bounds=next.Bounds;
            if(!hasRendered||previous.Text!=next.Text||previous.Rtf!=next.Rtf){
                if(!hasRendered||editor.Text!=next.Text||(next.Rtf is not null&&editor.Rtf!=next.Rtf))editor.LoadDocument(next.Text,next.Rtf);
            }
            if(!hasRendered||previous.Theme!=next.Theme) {
                var palette=Themes.Get(next.Theme);BackColor=palette.Paper;editor.SetPaper(palette.Paper,palette.Ink);
                paper.BackColor=palette.Paper;header.BackColor=palette.Header;
                toolbar.BackColor=palette.Paper;
                foreach(var b in formattingButtons){b.BackColor=palette.Paper;b.ForeColor=palette.Ink;b.FlatAppearance.MouseOverBackColor=palette.Header;}
                foreach(var b in new[]{add,ghost,more,hide}){b.BackColor=palette.Header;b.ForeColor=NoteChrome.Blend(palette.Header,palette.Ink,.82f);}
                foreach(var pair in themeItems)pair.Value.Checked=pair.Key==next.Theme;
                UpdateTitle();Invalidate();
            }
            if(!hasRendered||!ReferenceEquals(previous.Attachments,next.Attachments)||previous.Theme!=next.Theme){
                var palette=Themes.Get(next.Theme);gallery.Render(next.Images,palette.Paper,palette.Ink);LayoutHeader();
            }
            _=Handle; // Hidden HWND; configure before its first Show.
            double opacity=next.EffectiveOpacity(globalGhost);
            ApplyNative(next.Ghost||globalGhost,opacity);
            if(!hasRendered||previous.Ghost!=next.Ghost||previousGlobal!=globalGhost||previous.EffectiveOpacity(previousGlobal)!=opacity||previousError!=lastError) {
                ghost.Enabled=GhostService.Supported;ghostItem.Checked=next.Ghost;
                sliderUpdating=true;
                try {int value=(int)Math.Round(opacity*100);if(opacitySlider.Value!=value)opacitySlider.Value=value;}
                finally {sliderUpdating=false;}
                opacityLabel.Text=$"Opacity {(int)Math.Round(opacity*100)}% · {(globalGhost||next.Ghost?"ghost":"normal")}";
                ghost.Active=effectiveGhost;ghost.HasError=lastError is not null;
                tips.SetToolTip(ghost,lastError??(globalGhost?"Global ghost is on. Use your global ghost hotkey or the tray menu to turn it off.":next.Ghost?"Ghost is on. Use your note ghost hotkey to edit this note again.":"Enable ghost for this note"));
            }
            if(hidden||next.SessionHidden){if(Visible)Hide();}else if(!Visible)Show();
            hasRendered=true;
            UpdateFormatting();
            if(Bounds!=note.Bounds)BeginInvoke((Action)BoundsChanged);
        } finally {applying=false;}
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(keyData==(Keys.Control|Keys.N)){NewRequested?.Invoke();return true;}
        if(keyData==(Keys.Control|Keys.H)){ListRequested?.Invoke();return true;}
        if(keyData==Keys.Escape&&gallery.Expanded){gallery.Collapse();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    void ApplyNative(bool desired,double opacity,bool force=false)
    {
        if(!force&&lastRequested==desired&&Math.Abs(lastOpacity-opacity)<.0001)return;
        lastRequested=desired;lastOpacity=opacity;effectiveGhost=desired;
        // WinForms updates layered styles when Opacity changes; CreateParams
        // preserves our flags, and the final native call verifies their readback.
        if(Opacity!=opacity)Opacity=opacity;
        var result=ghosts.Apply(Handle,desired,opacity);
        if(result.Success){lastError=null;return;}
        effectiveGhost=false;if(Opacity!=note.Opacity)Opacity=note.Opacity;
        var fallback=ghosts.Apply(Handle,false,note.Opacity);
        string error=result.Error??"Could not configure ghost mode.";
        if(!fallback.Success)error+=" Window recovery: "+fallback.Error;
        if(lastError!=error){lastError=error;GhostFailed?.Invoke(error);}
    }
    protected override CreateParams CreateParams
    {
        get {var p=base.CreateParams;p.ExStyle|=(int)(Native.Layered|Native.Topmost);
            if(effectiveGhost)p.ExStyle|=(int)(Native.Transparent|Native.NoActivate);return p;}
    }
    protected override bool ShowWithoutActivation=>effectiveGhost;
    protected override void OnShown(EventArgs e){base.OnShown(e);if(!effectiveGhost)editor.Focus();}
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);lastRequested=null;
        if(ready){LayoutHeader();ApplyNative(note.Ghost||lastGlobal,note.EffectiveOpacity(lastGlobal),true);}
    }
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {base.OnDpiChanged(e);LayoutHeader();lastRequested=null;if(ready)ApplyNative(note.Ghost||lastGlobal,note.EffectiveOpacity(lastGlobal),true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(ClientSize.Width<2||ClientSize.Height<2)return;
        var palette=Themes.Get(note.Theme);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var pen=new Pen(NoteChrome.Blend(palette.Paper,palette.Ink,.18f));
        using var border=NoteChrome.RoundedPath(new RectangleF(.5f,.5f,ClientSize.Width-1,ClientSize.Height-1),8*DeviceDpi/96f);
        e.Graphics.DrawPath(pen,border);
        int s=Math.Max(1,DeviceDpi/96);for(int i=1;i<=2;i++)e.Graphics.DrawLine(pen,Width-7*s-i*3*s,Height-4*s,Width-7*s,Height-4*s-i*3*s);
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if(m.Msg!=0x84||effectiveGhost||!ready)return;
        var p=PointToClient(Cursor.Position);int edge=(int)Math.Round(6*DeviceDpi/96d);
        bool l=p.X<edge,r=p.X>=ClientSize.Width-edge,t=p.Y<edge,b=p.Y>=ClientSize.Height-edge;
        if(l&&t)m.Result=13;else if(r&&t)m.Result=14;else if(l&&b)m.Result=16;else if(r&&b)m.Result=17;
        else if(l)m.Result=10;else if(r)m.Result=11;else if(t)m.Result=12;else if(b)m.Result=15;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if(!destroying&&e.CloseReason==CloseReason.UserClosing){
            e.Cancel=true;
            // Avoid destroying an empty draft recursively inside FormClosing.
            BeginInvoke((Action)(()=>{if(!destroying)state.Dispatch(new CloseNote(NoteId));}));
        }
        base.OnFormClosing(e);
    }
    public void Destroy(){destroying=true;Close();Dispose();}
    protected override void Dispose(bool disposing)
    {
        if(disposing){
            destroying=true;ready=false;menu.Dispose();tips.Dispose();headerFont?.Dispose();foreach(var font in formattingFonts)font.Dispose();
            var previous=Region;Region=null;previous?.Dispose();
        }
        base.Dispose(disposing);
    }
}
