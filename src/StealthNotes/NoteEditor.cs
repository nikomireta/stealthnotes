using System.Runtime.InteropServices;

namespace StealthNotes;

internal sealed class NoteEditor : RichTextBox
{
    const string ClipboardToken="StealthNotes.RichText.Token.v1";
    static string? copiedToken,copiedRtf;
    readonly Font ownedFont=new("Segoe UI",12,FontStyle.Regular,GraphicsUnit.Point);
    readonly ContextMenuStrip editingMenu=new();
    bool suppress;
    int documentHeight=48;
    public int DocumentHeight=>documentHeight;
    public event Action? ContentHeightChanged,CaretMoved;
    public event Action<int>? ScrollRequested;
    public event Action<int,bool>? PageRequested;
    Color ink=Color.Black;
    public event Action? DocumentChanged;
    public event Action? NewRequested,ListRequested;
    public event Action<Image>? ImagePasted;
    public event Action<string[]>? FilesPasted;
    public event Action? FormattingChanged;
    public NoteEditor()
    {
        Multiline=true;BorderStyle=BorderStyle.None;ScrollBars=RichTextBoxScrollBars.None;
        WordWrap=true;AcceptsTab=true;HideSelection=false;DetectUrls=false;EnableAutoDragDrop=false;
        Font=ownedFont;AccessibleName="Note text";
        ContentsResized+=(_,e)=>{int height=Math.Max(Font.Height+8,e.NewRectangle.Height+8);if(documentHeight!=height){documentHeight=height;ContentHeightChanged?.Invoke();}};
        editingMenu.Items.Add("Undo",null,(_,_)=>UndoEdit());editingMenu.Items.Add("Redo",null,(_,_)=>RedoEdit());
        editingMenu.Items.Add(new ToolStripSeparator());
        editingMenu.Items.Add("Cut",null,(_,_)=>CopyInternal(true));editingMenu.Items.Add("Copy",null,(_,_)=>CopyInternal(false));
        editingMenu.Items.Add("Paste",null,(_,_)=>PasteSafe());editingMenu.Items.Add("Select all",null,(_,_)=>SelectAll());
        ContextMenuStrip=editingMenu;
    }
    public void LoadDocument(string text,string? rtf)
    {
        suppress=true;
        try {
            try{if(rtf is null)Text=text;else Rtf=rtf;}
            catch(ArgumentException e){LocalLog.Error("Invalid stored RTF; plain text displayed",e);Text=text;}
            ClearUndo();ApplyInk(ink);
        }finally{suppress=false;}
        FormattingChanged?.Invoke();RequestMeasure();
    }
    public void SetPaper(Color paper,Color foreground)
    {
        if(BackColor!=paper)BackColor=paper;
        ink=foreground;ApplyInk(ink);
    }
    void ApplyInk(Color color)
    {
        if(!IsHandleCreated)return;
        bool previous=suppress;suppress=true;
        try {
            WithoutUndo(()=>{
                var format=Format();format.Mask=0x40000000;format.Color=(uint)(color.R|(color.G<<8)|(color.B<<16));
                SendFormat(Handle,0x444,4,ref format); // CFM_COLOR, SCF_ALL; keep every other character style.
                SendFormat(Handle,0x444,0,ref format);
            });
        }finally{suppress=previous;}
    }
    public bool StyleActive(uint flag)
    {
        var format=Format();SendFormat(Handle,0x43a,1,ref format);
        return(format.Mask&flag)!=0&&(format.Effects&flag)!=0;
    }
    public void ToggleStyle(uint flag)
    {
        Focus();bool active=StyleActive(flag);var format=Format();format.Mask=flag;format.Effects=active?0:flag;
        SendFormat(Handle,0x444,1,ref format);Publish();FormattingChanged?.Invoke();
    }
    public void ToggleBullets(){Focus();SelectionBullet=!SelectionBullet;Publish();FormattingChanged?.Invoke();}
    public void UndoEdit(){if(CanUndo){Undo();ApplyInk(ink);Publish();FormattingChanged?.Invoke();}}
    public void RedoEdit(){if(CanRedo){Redo();ApplyInk(ink);Publish();FormattingChanged?.Invoke();}}
    void Publish(){if(!suppress)DocumentChanged?.Invoke();}
    protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Publish();RequestMeasure();if(!suppress)CaretMoved?.Invoke();}
    protected override void OnSelectionChanged(EventArgs e){base.OnSelectionChanged(e);if(!suppress){FormattingChanged?.Invoke();CaretMoved?.Invoke();}}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ApplyInk(ink);RequestMeasure();}
    void RequestMeasure(){if(IsHandleCreated)Native.SendMessageW(Handle,0x441,0,0);} // EM_REQUESTRESIZE
    protected override void OnResize(EventArgs e){base.OnResize(e);RequestMeasure();}
    public Point CaretPosition()=>Focused&&GetCaretPos(out var point)?point:GetPositionFromCharIndex(SelectionStart);
    public int CaretHeight(){using var font=SelectionFont;return Math.Max(Font.Height,font?.Height??0);}
    public void MovePage(int direction,int distance,bool extend)
    {
        var point=CaretPosition();int head=GetCharIndexFromPosition(point);
        int target=GetCharIndexFromPosition(new(point.X,Math.Max(0,point.Y+direction*distance)));
        if(!extend)Select(target,0);
        else {
            int anchor=head<=SelectionStart?SelectionStart+SelectionLength:SelectionStart;
            // EM_SETSEL supports a backwards selection, retaining the active end.
            Native.SendMessageW(Handle,0xb1,anchor,target);
        }
        CaretMoved?.Invoke();
    }
    [DllImport("user32.dll")]static extern bool GetCaretPos(out Point point);
    void CopyInternal(bool cut)
    {
        if(SelectionLength==0)return;
        try {
            string token=Guid.NewGuid().ToString("N");var data=new DataObject();
            data.SetData(DataFormats.UnicodeText,SelectedText);data.SetData(ClipboardToken,token);
            string rtf=SelectedRtf;Clipboard.SetDataObject(data,true);
            copiedToken=token;copiedRtf=rtf;
            if(cut){SelectedText="";Publish();}
        }catch(ExternalException){MessageBox.Show(FindForm(),"The clipboard is busy. Please try again.","StealthNotes");}
    }
    void PasteSafe()
    {
        try {
            if(Clipboard.ContainsImage()){
                var image=Clipboard.GetImage();if(image is not null){if(ImagePasted is null)image.Dispose();else ImagePasted.Invoke(image);}return;
            }
            if(Clipboard.ContainsFileDropList()){
                FilesPasted?.Invoke(Clipboard.GetFileDropList().Cast<string>().ToArray());return;
            }
            var data=Clipboard.GetDataObject();
            if(copiedToken is not null&&data?.GetData(ClipboardToken) as string==copiedToken&&copiedRtf is not null)
                SelectedRtf=copiedRtf;
            else if(Clipboard.ContainsText(TextDataFormat.UnicodeText))SelectedText=Clipboard.GetText(TextDataFormat.UnicodeText);
            ApplyInk(ink);Publish();
        }catch(ExternalException){MessageBox.Show(FindForm(),"The clipboard is busy. Please try again.","StealthNotes");}
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(keyData is Keys.PageUp or Keys.PageDown or (Keys.Shift|Keys.PageUp) or (Keys.Shift|Keys.PageDown)){
            PageRequested?.Invoke((keyData&Keys.KeyCode)==Keys.PageDown?1:-1,(keyData&Keys.Shift)!=0);return true;
        }
        switch(keyData){
            case Keys.Control|Keys.B:ToggleStyle(1);return true;
            case Keys.Control|Keys.I:ToggleStyle(2);return true;
            case Keys.Control|Keys.U:ToggleStyle(4);return true;
            case Keys.Control|Keys.T:ToggleStyle(8);return true;
            case Keys.Control|Keys.Shift|Keys.L:ToggleBullets();return true;
            case Keys.Control|Keys.Z:UndoEdit();return true;
            case Keys.Control|Keys.Y:RedoEdit();return true;
            case Keys.Control|Keys.C:CopyInternal(false);return true;
            case Keys.Control|Keys.X:CopyInternal(true);return true;
            case Keys.Control|Keys.V:PasteSafe();return true;
            case Keys.Shift|Keys.Insert:PasteSafe();return true;
            case Keys.Control|Keys.Insert:CopyInternal(false);return true;
            case Keys.Shift|Keys.Delete:CopyInternal(true);return true;
            case Keys.Control|Keys.N:NewRequested?.Invoke();return true;
            case Keys.Control|Keys.H:ListRequested?.Invoke();return true;
        }
        return base.ProcessCmdKey(ref msg,keyData);
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x20a&&ScrollRequested is not null){ScrollRequested.Invoke((short)(((long)m.WParam>>16)&0xffff));m.Result=0;return;}
        if(m.Msg==0x302){PasteSafe();m.Result=0;return;}
        if(m.Msg==0x301){CopyInternal(false);m.Result=0;return;}
        if(m.Msg==0x300){CopyInternal(true);m.Result=0;return;}
        base.WndProc(ref m);
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct CharFormat {
        public uint Size,Mask,Effects;public int Height,Offset;public uint Color;
        public byte CharacterSet,Pitch;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Face;
    }
    static CharFormat Format()=>new(){Size=(uint)Marshal.SizeOf<CharFormat>(),Face=""};
    [DllImport("user32.dll",EntryPoint="SendMessageW")]static extern nint SendFormat(nint hwnd,int message,nint w,ref CharFormat value);
    [DllImport("user32.dll",EntryPoint="SendMessageW")]static extern nint SendPointer(nint hwnd,int message,nint w,out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int TomUndo(nint document,int count,out int actual);
    void WithoutUndo(Action action)
    {
        nint ole=0,document=0;bool suspended=false;TomUndo? undo=null;
        try {
            SendPointer(Handle,0x43c,0,out ole);
            var iid=new Guid("8CC497C0-A1DF-11CE-8098-00AA0047BE5D");
            if(ole!=0&&Marshal.QueryInterface(ole,in iid,out document)>=0){
                // ITextDocument::Undo is slot 22 (IDispatch + TOM methods), verified against the Windows SDK tom.h.
                nint function=Marshal.ReadIntPtr(Marshal.ReadIntPtr(document),22*IntPtr.Size);
                undo=Marshal.GetDelegateForFunctionPointer<TomUndo>(function);
                suspended=undo(document,-9999995,out _)>=0; // tomSuspend preserves existing undo/redo history.
            }
            action();
        } finally {
            if(suspended)undo!(document,-9999994,out _); // tomResume
            if(document!=0)Marshal.Release(document);if(ole!=0)Marshal.Release(ole);
        }
    }
    protected override void Dispose(bool disposing){if(disposing){editingMenu.Dispose();ownedFont.Dispose();}base.Dispose(disposing);}
}
