namespace StealthNotes;

// A single scrolling document. The Rich Edit HWND stays alive through every layout change.
internal sealed class NoteContent : BufferedPanel
{
    readonly NoteEditor editor;
    readonly NoteGallery gallery;
    bool arranging,queued,caretQueued;
    public NoteContent(NoteEditor editor,NoteGallery gallery)
    {
        this.editor=editor;this.gallery=gallery;AutoScroll=true;TabStop=false;AccessibleName="Note content";
        Controls.Add(editor);
        editor.ContentHeightChanged+=QueueLayout;
        editor.CaretMoved+=QueueCaret;
        editor.ScrollRequested+=ScrollDocument;
        editor.PageRequested+=(direction,extend)=>{
            int distance=Math.Max(editor.Font.Height,ClientSize.Height-editor.Font.Height);
            if(extend)editor.MovePage(direction,distance,true);else SetScroll(-AutoScrollPosition.Y+direction*distance);
        };
        gallery.LayoutRequested+=RefreshContent;gallery.RepaintRequested+=Invalidate;
        Scroll+=(_,_)=>UpdateViewport();
        VisibleChanged+=(_,_)=>UpdateViewport();
        SizeChanged+=(_,_)=>QueueLayout();
    }
    public void RefreshContent(){PerformLayout();UpdateViewport();}
    Point ImageOrigin()=>new(Padding.Left,Padding.Top+AutoScrollPosition.Y);
    void UpdateViewport()
    {
        var origin=ImageOrigin();
        gallery.UpdateViewport(new(-origin.X,-origin.Y,ClientSize.Width,ClientSize.Height),Visible);Invalidate();
    }
    void QueueLayout()
    {
        if(queued||IsDisposed||!IsHandleCreated)return;queued=true;
        BeginInvoke((Action)(()=>{queued=false;if(!IsDisposed)PerformLayout();}));
    }
    void QueueCaret()
    {
        if(caretQueued||IsDisposed||!IsHandleCreated||!editor.Focused)return;caretQueued=true;
        BeginInvoke((Action)(()=>{caretQueued=false;if(!IsDisposed){PerformLayout();RevealCaret();}}));
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        if(arranging)return;arranging=true;
        try {
            // Reserve the scrollbar width even before it appears: wrapping cannot oscillate.
            int scrollbar=ScrollbarWidth(DeviceDpi);
            int width=Math.Max(1,ClientSize.Width+(VerticalScroll.Visible?scrollbar:0)-Padding.Horizontal-scrollbar);
            int gap=(int)Math.Round(12*DeviceDpi/96d);
            int imageHeight=gallery.MeasureHeight(width,gap);
            int imageGap=imageHeight>0?gap:0;
            int textHeight=Math.Max(editor.DocumentHeight,Math.Max(48,ClientSize.Height-Padding.Vertical-imageHeight-imageGap));
            var scroll=AutoScrollPosition;
            editor.SetBounds(Padding.Left+scroll.X,Padding.Top+scroll.Y+imageHeight+imageGap,width,textHeight);
            AutoScrollMinSize=new(0,Padding.Vertical+imageHeight+imageGap+textHeight);
            base.OnLayout(e);
        } finally {arranging=false;}
        UpdateViewport();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int GetSystemMetricsForDpi(int index,uint dpi);
    internal static int ScrollbarWidth(int dpi)=>GetSystemMetricsForDpi(2,(uint)dpi);
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var saved=e.Graphics.Save();var origin=ImageOrigin();
        try{e.Graphics.TranslateTransform(origin.X,origin.Y);gallery.Draw(e.Graphics,Font);}
        finally{e.Graphics.Restore(saved);}
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button==MouseButtons.Right)gallery.ShowContextMenu(this,e.Location,ImageOrigin());
    }
    protected override Point ScrollToControl(Control activeControl)=>DisplayRectangle.Location;
    protected override void OnMouseWheel(MouseEventArgs e){ScrollDocument(e.Delta);if(e is HandledMouseEventArgs handled)handled.Handled=true;}
    void ScrollDocument(int delta)
    {
        int distance=SystemInformation.MouseWheelScrollLines<0?ClientSize.Height:
            Math.Max(1,SystemInformation.MouseWheelScrollLines)*Math.Max(16,editor.Font.Height);
        SetScroll(-AutoScrollPosition.Y-(int)Math.Round(delta/120d*distance));
    }
    void SetScroll(int y)
    {
        AutoScrollPosition=new(0,Math.Clamp(y,0,Math.Max(0,AutoScrollMinSize.Height-ClientSize.Height)));
        UpdateViewport();
    }
    void RevealCaret()
    {
        if(arranging||!editor.Focused||IsDisposed)return;
        // Query the native caret rather than altering selection or the undo buffer.
        var caret=editor.CaretPosition();
        int top=editor.Top+caret.Y,bottom=top+editor.CaretHeight()+4;
        int scroll=-AutoScrollPosition.Y;
        if(top<Padding.Top)SetScroll(scroll+top-Padding.Top);
        else if(bottom>ClientSize.Height-Padding.Bottom)SetScroll(scroll+bottom-ClientSize.Height+Padding.Bottom);
    }
    protected override void Dispose(bool disposing){if(disposing)gallery.Dispose();base.Dispose(disposing);}
}
