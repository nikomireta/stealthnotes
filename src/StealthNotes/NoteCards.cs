using System.Drawing.Drawing2D;

namespace StealthNotes;

internal sealed record NoteCard(Guid Id,string Text,string Theme,NoteImage? Image,bool IsOpen,bool Ghost);

// Variable-height owner draw without native ListBox's item-height limit at high DPI.
internal sealed class NoteCards : ScrollableControl
{
    readonly PreviewCache.Session previews;
    NoteCard[] rows=[];
    readonly List<Rectangle> bounds=[];
    int selected=-1;
    Guid? pressed;
    bool arranging;
    public event Action<Guid>? OpenRequested;
    public event Action? SelectionChanged;
    public NoteCard? Selected()=>selected>=0&&selected<rows.Length?rows[selected]:null;
    public NoteCards(ImageStore images)
    {
        AutoScroll=true;TabStop=true;AccessibleName="Notes list";AccessibleRole=AccessibleRole.List;
        SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|
            ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);
        previews=images.Previews.CreateSession(()=>Invalidate());
        VisibleChanged+=(_,_)=>RequestVisible();Scroll+=(_,_)=>{RequestVisible();Invalidate();};
    }
    int S(int value)=>(int)Math.Round(value*DeviceDpi/96d);
    int ScrollY=>-AutoScrollPosition.Y;
    int IndexAt(Point point)=>bounds.FindIndex(r=>r.Contains(point.X,point.Y+ScrollY));
    (Guid? Id,int Offset,int Index) CaptureAnchor()
    {
        int index=bounds.FindIndex(r=>r.Bottom>ScrollY);
        return index<0?(null,0,0):(rows[index].Id,ScrollY-bounds[index].Top,index);
    }
    public void SetRows(NoteCard[] next)
    {
        if(rows.SequenceEqual(next))return;
        var anchor=CaptureAnchor();Guid? chosen=Selected()?.Id;int oldIndex=selected;
        rows=next;selected=chosen is null?-1:Array.FindIndex(rows,r=>r.Id==chosen);
        if(selected<0&&rows.Length>0)selected=Math.Clamp(oldIndex,0,rows.Length-1);
        ArrangeCards(anchor);SelectionChanged?.Invoke();AccessibilityNotifyClients(AccessibleEvents.Reorder,-1);
    }
    void ArrangeCards((Guid? Id,int Offset,int Index) anchor)
    {
        if(arranging||IsDisposed)return;arranging=true;
        try {
            int scrollbar=NoteContent.ScrollbarWidth(DeviceDpi);
            bounds.Clear();int width=Math.Max(S(32),ClientSize.Width+(VerticalScroll.Visible?scrollbar:0)-scrollbar-S(24));
            int y=S(8),inset=S(14),textWidth=Math.Max(1,width-inset*2);
            int line=TextRenderer.MeasureText("Ag",Font,Size.Empty,TextFormatFlags.NoPadding).Height;
            foreach(var row in rows){
                int textHeight=Math.Min(line*5,TextRenderer.MeasureText(row.Text,Font,new Size(textWidth,int.MaxValue),
                    TextFormatFlags.WordBreak|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Height);
                int imageHeight=row.Image is null?0:Math.Max(S(48),Math.Min(S(160),
                    (int)Math.Min(S(160),(double)textWidth*Math.Max(1,row.Image.Height)/Math.Max(1,row.Image.Width))));
                int height=Math.Max(S(72),inset*2+Math.Max(line,textHeight)+(imageHeight>0?imageHeight+S(12):0));
                bounds.Add(new(S(12),y,width,height));y+=height+S(12);
            }
            AutoScrollMinSize=new(0,y);
            int index=anchor.Id is null?-1:Array.FindIndex(rows,r=>r.Id==anchor.Id);
            if(index<0&&rows.Length>0)index=Math.Clamp(anchor.Index,0,rows.Length-1);
            AutoScrollPosition=new(0,index<0?0:Math.Max(0,bounds[index].Top+anchor.Offset));
        }finally{arranging=false;}
        RequestVisible();Invalidate();
    }
    protected override void OnSizeChanged(EventArgs e){var anchor=CaptureAnchor();base.OnSizeChanged(e);ArrangeCards(anchor);}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(previews is not null)ArrangeCards(CaptureAnchor());}
    protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);ArrangeCards(CaptureAnchor());}
    Rectangle ImageBounds(int index)
    {
        var card=bounds[index];var image=rows[index].Image!;int inset=S(14),width=card.Width-inset*2;
        int height=Math.Max(S(48),Math.Min(S(160),(int)Math.Min(S(160),(double)width*Math.Max(1,image.Height)/Math.Max(1,image.Width))));
        return new(card.Left+inset,card.Top+inset,width,height);
    }
    void RequestVisible()
    {
        if(previews is null||IsDisposed)return;
        var viewport=new Rectangle(0,ScrollY,ClientSize.Width,ClientSize.Height);
        previews.Request(Visible?Enumerable.Range(0,rows.Length).Where(i=>rows[i].Image is not null&&bounds[i].IntersectsWith(viewport))
            .Select(i=>PreviewKey.For(rows[i].Image!.Id,ImageBounds(i).Width)):Array.Empty<PreviewKey>());
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);RequestVisible();e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
        if(rows.Length==0){TextRenderer.DrawText(e.Graphics,"No notes found",Font,ClientRectangle,ForeColor,
            TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);return;}
        for(int i=0;i<rows.Length;i++){
            var rect=bounds[i];rect.Offset(0,-ScrollY);if(!rect.IntersectsWith(e.ClipRectangle))continue;
            var row=rows[i];var palette=Themes.Get(row.Theme);
            using var path=NoteChrome.RoundedPath(rect,S(8));using var brush=new SolidBrush(palette.Paper);
            e.Graphics.FillPath(brush,path);using var pen=new Pen(NoteChrome.Blend(palette.Paper,palette.Ink,i==selected ? .65f : .15f),i==selected?2:1);
            e.Graphics.DrawPath(pen,path);
            int inset=S(14),top=rect.Top+inset;
            if(row.Image is { } image){
                var imageRect=ImageBounds(i);imageRect.Offset(0,-ScrollY);var key=PreviewKey.For(image.Id,imageRect.Width);
                var bitmap=previews.Get(key);
                if(bitmap is not null)e.Graphics.DrawImage(bitmap,ImageStore.Fit(bitmap.Size,imageRect));
                else TextRenderer.DrawText(e.Graphics,previews.Failed(key)?"Image unavailable":"Loading image...",Font,imageRect,palette.Ink,
                    TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
                top=imageRect.Bottom+S(12);
            }
            TextRenderer.DrawText(e.Graphics,row.Text,Font,new Rectangle(rect.Left+inset,top,rect.Width-inset*2,rect.Bottom-inset-top),palette.Ink,
                TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
            if(i==selected&&Focused){var focus=rect;focus.Inflate(-S(4),-S(4));ControlPaint.DrawFocusRectangle(e.Graphics,focus,palette.Ink,palette.Paper);}
        }
    }
    void SelectIndex(int index,bool reveal)
    {
        if(index<0||index>=rows.Length)return;selected=index;Invalidate();SelectionChanged?.Invoke();
        AccessibilityNotifyClients(AccessibleEvents.Selection,index+1);
        AccessibilityNotifyClients(AccessibleEvents.Focus,index+1);
        if(reveal){var rect=bounds[index];if(rect.Top<ScrollY)AutoScrollPosition=new(0,rect.Top);
            else if(rect.Bottom>ScrollY+ClientSize.Height)AutoScrollPosition=new(0,Math.Min(rect.Top,rect.Bottom-ClientSize.Height));RequestVisible();}
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);int index=IndexAt(e.Location);pressed=null;
        if(index>=0){Focus();SelectIndex(index,false);if(e.Button==MouseButtons.Left)pressed=rows[index].Id;}
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);int index=IndexAt(e.Location);
        if(e.Button==MouseButtons.Left&&index>=0&&rows[index].Id==pressed)OpenRequested?.Invoke(rows[index].Id);
        pressed=null;
    }
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);int index=e.KeyCode switch {
            Keys.Up=>selected-1,Keys.Down=>selected+1,Keys.Home=>0,Keys.End=>rows.Length-1,
            Keys.PageUp=>bounds.FindLastIndex(r=>r.Top<Math.Max(0,(selected<0?0:bounds[selected].Top)-ClientSize.Height)),
            Keys.PageDown=>bounds.FindIndex(r=>r.Top>(selected<0?0:bounds[selected].Top)+ClientSize.Height),_=>-2};
        if(index!=-2&&rows.Length>0){SelectIndex(Math.Clamp(index<0&&e.KeyCode==Keys.PageDown?rows.Length-1:index,0,rows.Length-1),true);e.Handled=true;}
    }
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    protected override AccessibleObject CreateAccessibilityInstance()=>new CardsAccessibility(this);
    sealed class CardsAccessibility(NoteCards owner) : ControlAccessibleObject(owner)
    {
        public override int GetChildCount()=>owner.rows.Length;
        public override AccessibleObject? GetChild(int index)=>index>=0&&index<owner.rows.Length?new CardAccessibility(owner,owner.rows[index].Id):null;
        public override AccessibleObject? GetSelected()=>GetChild(owner.selected);
        public override AccessibleObject? GetFocused()=>owner.Focused?GetSelected():null;
        public override AccessibleObject? HitTest(int x,int y)=>GetChild(owner.IndexAt(owner.PointToClient(new(x,y))))??this;
    }
    sealed class CardAccessibility(NoteCards owner,Guid id) : AccessibleObject
    {
        int Index=>Array.FindIndex(owner.rows,r=>r.Id==id);
        public override string? Name {get=>Index is var i&&i>=0?owner.rows[i].Text:"Note";set {}}
        public override AccessibleRole Role=>AccessibleRole.ListItem;
        public override AccessibleObject? Parent=>owner.AccessibilityObject;
        public override string? DefaultAction=>"Open note";
        public override Rectangle Bounds {get {int i=Index;if(i<0)return Rectangle.Empty;var rect=owner.bounds[i];rect.Offset(0,-owner.ScrollY);return owner.RectangleToScreen(Rectangle.Intersect(rect,owner.ClientRectangle));}}
        public override AccessibleStates State=>AccessibleStates.Selectable|AccessibleStates.Focusable|
            (Index==owner.selected?AccessibleStates.Selected:AccessibleStates.None)|
            (Index==owner.selected&&owner.Focused?AccessibleStates.Focused:AccessibleStates.None);
        public override void DoDefaultAction(){if(Index>=0)owner.OpenRequested?.Invoke(id);}
        public override void Select(AccessibleSelection flags){if(Index>=0){owner.Focus();owner.SelectIndex(Index,true);}}
    }
    protected override void Dispose(bool disposing){if(disposing)previews.Dispose();base.Dispose(disposing);}
}
