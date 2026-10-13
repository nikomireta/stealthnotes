namespace StealthNotes;

internal sealed class NoteWindow : Form
{
    readonly AppState state;
    readonly GhostService ghosts;
    readonly TextBox editor;
    readonly Panel header;
    readonly Label title;
    readonly Button add,ghost,more,hide;
    readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem ghostItem;
    readonly TrackBar opacitySlider;
    readonly ToolStripLabel opacityLabel;
    readonly ToolTip tips=new() {ShowAlways=true};
    readonly Dictionary<string,ToolStripMenuItem> themeItems=[];
    Note note;
    bool applying,effectiveGhost,ready,destroying,sliderUpdating,lastGlobal;
    bool? lastRequested;
    double lastOpacity=-1;
    string? lastError;
    Font? editorFont,headerFont,buttonFont;
    public Guid NoteId=>note.Id;
    public event Action? NewRequested;
    public event Action<Guid>? BecameActive;
    public event Action<string>? GhostFailed;

    public NoteWindow(Note initial,AppState state,GhostService ghosts)
    {
        note=initial;this.state=state;this.ghosts=ghosts;
        Text=$"StealthNotes · {initial.Id.ToString("N")[..8]}";
        AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;
        StartPosition=FormStartPosition.Manual;Bounds=initial.Bounds;
        TopMost=true;ShowInTaskbar=false;MinimumSize=new(248,180);
        SetStyle(ControlStyles.ResizeRedraw,true);
        header=new Panel {Dock=DockStyle.Top,Height=34};
        title=new Label {Text="Note",AutoSize=false,TextAlign=ContentAlignment.MiddleLeft};
        add=HeaderButton("+","New note");
        ghost=HeaderButton("◌","Ghost this note (click-through)");
        more=HeaderButton("···","Color and opacity");
        hide=HeaderButton("×","Hide note");
        header.Controls.AddRange([title,add,ghost,more,hide]);
        editor=new TextBox {Multiline=true,BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,
            ScrollBars=ScrollBars.None,WordWrap=true,AcceptsReturn=true,AcceptsTab=true,MaxLength=0,
            Text=initial.Text,Margin=Padding.Empty};
        var paper=new Panel {Dock=DockStyle.Fill,Padding=new(14,10,14,10)};paper.Controls.Add(editor);
        Controls.Add(paper);Controls.Add(header);Padding=new(0,0,0,8);
        menu=new ContextMenuStrip {ShowImageMargin=false};
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
        opacitySlider=new TrackBar {Minimum=10,Maximum=100,TickStyle=TickStyle.None,AutoSize=false,Size=new(190,30),SmallChange=5,LargeChange=10};
        menu.Items.Add(new ToolStripControlHost(opacitySlider) {Margin=new(6,0,6,6),AutoSize=false,Size=new(190,32)});
        opacitySlider.ValueChanged+=(_,_)=>{if(!sliderUpdating)state.Dispatch(new ChangeOpacity(NoteId,opacitySlider.Value/100d));};
        menu.Items.Add(new ToolStripSeparator());
        var delete=new ToolStripMenuItem("Delete note") {ForeColor=Color.Firebrick};
        delete.Click+=(_,_)=>{
            if(note.Text.Length>0&&MessageBox.Show(this,"Permanently delete this note?","StealthNotes",MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            state.Dispatch(new DeleteNote(NoteId));
        };
        menu.Items.Add(delete);
        more.Click+=(_,_)=>menu.Show(more,new Point(0,more.Height));
        add.Click+=(_,_)=>NewRequested?.Invoke();
        ghost.Click+=(_,_)=>state.Dispatch(new ToggleNoteGhost(NoteId));
        hide.Click+=(_,_)=>state.Dispatch(new HideNote(NoteId));
        foreach(var c in new Control[]{header,title})c.MouseDown+=DragHeader;
        editor.TextChanged+=(_,_)=>{if(!applying)state.Dispatch(new EditText(NoteId,editor.Text));};
        LocationChanged+=(_,_)=>BoundsChanged();SizeChanged+=(_,_)=>{LayoutHeader();BoundsChanged();};
        Activated+=(_,_)=>BecameActive?.Invoke(NoteId);
        ready=true;LayoutHeader();
    }

    Button HeaderButton(string text,string tooltip)
    {
        var button=new Button {Text=text,FlatStyle=FlatStyle.Flat,TabStop=false,UseVisualStyleBackColor=false,Cursor=Cursors.Hand};
        button.FlatAppearance.BorderSize=0;tips.SetToolTip(button,tooltip);return button;
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
        if(!ready||destroying||IsDisposed)return;
        int S(int n)=>(int)Math.Round(n*DeviceDpi/96d);
        header.Height=S(34);Padding=new(0,0,0,S(8));MinimumSize=new(S(248),S(180));
        int width=S(30),height=S(28),gap=S(2),right=ClientSize.Width-S(3);
        foreach(var b in new[]{hide,more,ghost}){right-=width;b.SetBounds(right,S(3),width,height);right-=gap;}
        add.SetBounds(S(3),S(3),width,height);title.SetBounds(S(39),S(3),Math.Max(0,right-S(42)),height);
        ((Panel)editor.Parent!).Padding=new(S(14),S(10),S(14),S(10));
        float editorSize=(float)(16*DeviceDpi/96d);
        if(editorFont is null||Math.Abs(editorFont.Size-editorSize)>.1){
            var oldEditor=editorFont;var oldHeader=headerFont;var oldButton=buttonFont;
            editorFont=new("Segoe UI",editorSize,FontStyle.Regular,GraphicsUnit.Pixel);
            headerFont=new("Segoe UI",(float)(12*DeviceDpi/96d),FontStyle.Regular,GraphicsUnit.Pixel);
            buttonFont=new("Segoe UI",(float)(20*DeviceDpi/96d),FontStyle.Regular,GraphicsUnit.Pixel);
            editor.Font=editorFont;title.Font=headerFont;
            foreach(var b in new[]{add,ghost,more,hide})b.Font=buttonFont;
            oldEditor?.Dispose();oldHeader?.Dispose();oldButton?.Dispose();
        }
    }
    public void Render(Note next,bool globalGhost,bool hidden)
    {
        applying=true;
        try {
            note=next;lastGlobal=globalGhost;
            if(!hidden&&!next.SessionHidden&&WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;
            if(Bounds!=next.Bounds&&WindowState==FormWindowState.Normal)Bounds=next.Bounds;
            if(editor.Text!=next.Text){int selection=editor.SelectionStart;editor.Text=next.Text;editor.SelectionStart=Math.Min(selection,editor.TextLength);}
            var palette=Themes.Get(next.Theme);BackColor=palette.Paper;editor.BackColor=palette.Paper;editor.ForeColor=palette.Ink;
            editor.Parent!.BackColor=palette.Paper;header.BackColor=palette.Header;title.ForeColor=palette.Ink;
            foreach(var b in new[]{add,ghost,more,hide}){b.BackColor=palette.Header;b.ForeColor=palette.Ink;b.FlatAppearance.MouseOverBackColor=ControlPaint.Dark(palette.Header,.04f);}
            foreach(var pair in themeItems)pair.Value.Checked=pair.Key==next.Theme;
            ghost.Enabled=GhostService.Supported;
            ghostItem.Checked=next.Ghost;
            _=Handle; // Hidden HWND; configure before its first Show.
            ApplyNative(next.Ghost||globalGhost,next.EffectiveOpacity(globalGhost));
            sliderUpdating=true;opacitySlider.Value=(int)Math.Round(next.EffectiveOpacity(globalGhost)*100);sliderUpdating=false;
            opacityLabel.Text=$"Opacity {(int)Math.Round(next.EffectiveOpacity(globalGhost)*100)}% · {(globalGhost||next.Ghost?"ghost":"normal")}";
            ghost.Text=lastError is not null?"!":effectiveGhost?"●":"◌";
            tips.SetToolTip(ghost,lastError??(globalGhost?"Global ghost is on. Use your global ghost hotkey or the tray menu to turn it off.":next.Ghost?"Ghost is on. Use your note ghost hotkey to edit this note again.":"Enable ghost for this note"));
            if(hidden||next.SessionHidden)Hide();else if(!Visible)Show();
            Invalidate();
            if(Bounds!=note.Bounds)BeginInvoke((Action)BoundsChanged);
        } finally {applying=false;}
    }
    void ApplyNative(bool desired,double opacity,bool force=false)
    {
        if(!force&&lastRequested==desired&&Math.Abs(lastOpacity-opacity)<.0001)return;
        lastRequested=desired;lastOpacity=opacity;effectiveGhost=desired;
        // WinForms updates layered styles when Opacity changes; CreateParams
        // preserves our flags, and the final native call verifies their readback.
        Opacity=opacity;
        var result=ghosts.Apply(Handle,desired,opacity);
        if(result.Success){lastError=null;return;}
        effectiveGhost=false;Opacity=note.Opacity;
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
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);lastRequested=null;
        if(ready){LayoutHeader();ApplyNative(note.Ghost||lastGlobal,note.EffectiveOpacity(lastGlobal),true);}
    }
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {base.OnDpiChanged(e);LayoutHeader();lastRequested=null;if(ready)ApplyNative(note.Ghost||lastGlobal,note.EffectiveOpacity(lastGlobal),true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);using var pen=new Pen(Color.FromArgb(85,Themes.Get(note.Theme).Ink));
        int s=Math.Max(1,DeviceDpi/96);for(int i=1;i<=3;i++)e.Graphics.DrawLine(pen,Width-3*s-i*3*s,Height-3*s,Width-3*s,Height-3*s-i*3*s);
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
        if(!destroying&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;state.Dispatch(new HideNote(NoteId));}
        base.OnFormClosing(e);
    }
    public void Destroy(){destroying=true;Close();Dispose();}
    protected override void Dispose(bool disposing)
    {
        if(disposing){destroying=true;ready=false;menu.Dispose();tips.Dispose();editorFont?.Dispose();headerFont?.Dispose();buttonFont?.Dispose();}
        base.Dispose(disposing);
    }
}
