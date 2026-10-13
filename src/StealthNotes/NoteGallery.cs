namespace StealthNotes;

internal sealed class NoteGallery : BufferedPanel
{
    readonly ImageStore images;
    readonly Button previous=new(){Text="‹",AccessibleName="Previous image"},next=new(){Text="›",AccessibleName="Next image"};
    readonly Button remove=new(){Text="Remove",AutoSize=true},back=new(){Text="Back",AutoSize=true};
    readonly Label counter=new(){TextAlign=ContentAlignment.MiddleCenter};
    readonly BufferedPanel canvas=new(){Cursor=Cursors.Hand,AccessibleName="Image preview. Click to enlarge."};
    IReadOnlyList<NoteImage> items=Array.Empty<NoteImage>();
    int index,requestVersion;
    Bitmap? preview;
    bool expanded,disposed;
    public event Action<Guid>? RemoveRequested;
    public event Action? ExpansionChanged;
    public bool Expanded=>expanded;
    public NoteGallery(ImageStore images)
    {
        this.images=images;Height=96;
        Controls.AddRange([canvas,previous,next,counter,remove,back]);back.Visible=false;
        previous.Click+=(_,_)=>Select(index-1);next.Click+=(_,_)=>Select(index+1);
        remove.Click+=(_,_)=>{if(items.Count>0)RemoveRequested?.Invoke(items[index].Id);};
        back.Click+=(_,_)=>Collapse();canvas.Click+=async(_,_)=>await Expand();
        canvas.Paint+=PaintImage;images.ThumbnailReady+=ThumbnailReady;SizeChanged+=(_,_)=>Arrange();
        foreach(var b in new[]{previous,next,remove,back}){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.Cursor=Cursors.Hand;}
    }
    public void Render(IReadOnlyList<NoteImage> current,Color paper,Color ink)
    {
        Guid? selected=items.Count>0?items[index].Id:null;
        bool changed=!items.Select(i=>i.Id).SequenceEqual(current.Select(i=>i.Id));items=current;
        if(changed){int found=selected is null?-1:current.ToList().FindIndex(i=>i.Id==selected);index=Math.Max(0,found);Collapse();}
        Visible=current.Count>0;BackColor=paper;ForeColor=ink;canvas.BackColor=paper;
        foreach(Control c in new Control[]{previous,next,counter,remove,back}){c.BackColor=paper;c.ForeColor=ink;}
        UpdateCounter();Arrange();canvas.Invalidate();
    }
    void ThumbnailReady(Guid id){if(items.Count>0&&items[index].Id==id)canvas.Invalidate();}
    void UpdateCounter(){counter.Text=items.Count==0?"":"Image "+(index+1)+" / "+items.Count;previous.Enabled=index>0;next.Enabled=index+1<items.Count;}
    void Select(int nextIndex){if(nextIndex<0||nextIndex>=items.Count)return;Collapse();index=nextIndex;UpdateCounter();canvas.Invalidate();}
    void Arrange()
    {
        int S(int n)=>(int)Math.Round(n*DeviceDpi/96d);int bar=S(28);
        previous.SetBounds(S(4),0,S(26),bar);next.SetBounds(S(32),0,S(26),bar);
        remove.SetBounds(Math.Max(0,Width-S(66)),0,S(62),bar);
        back.SetBounds(Math.Max(0,Width-S(122)),0,S(52),bar);
        counter.SetBounds(S(60),0,Math.Max(0,Width-S(expanded?186:130)),bar);
        canvas.SetBounds(S(8),bar,Math.Max(0,Width-S(16)),Math.Max(0,Height-bar-S(6)));
    }
    void PaintImage(object? sender,PaintEventArgs e)
    {
        if(items.Count==0)return;
        Image? image=preview??images.Thumbnail(items[index].Id);
        if(image is null){TextRenderer.DrawText(e.Graphics,images.ThumbnailFailed(items[index].Id)?"Image unavailable — click to retry":"Loading image…",Font,canvas.ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}
        if(canvas.Width>0&&canvas.Height>0)e.Graphics.DrawImage(image,ImageStore.Fit(image.Size,canvas.ClientRectangle));
    }
    async Task Expand()
    {
        if(items.Count==0||expanded)return;
        expanded=true;back.Visible=true;ExpansionChanged?.Invoke();Arrange();
        int version=++requestVersion;Guid id=items[index].Id;
        try {
            var bitmap=await images.Preview(id);
            if(disposed||!expanded||version!=requestVersion){bitmap.Dispose();return;}
            preview?.Dispose();preview=bitmap;canvas.Invalidate();
        }catch(OperationCanceledException){}
        catch(Exception e){if(!disposed&&version==requestVersion){Collapse();MessageBox.Show(FindForm(),e.Message,"Could not open image",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    }
    public void Collapse()
    {
        requestVersion++;preview?.Dispose();preview=null;
        if(expanded){expanded=false;back.Visible=false;ExpansionChanged?.Invoke();}
        if(!disposed){Arrange();canvas.Invalidate();}
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing&&!disposed){disposed=true;requestVersion++;images.ThumbnailReady-=ThumbnailReady;preview?.Dispose();preview=null;}
        base.Dispose(disposing);
    }
}
