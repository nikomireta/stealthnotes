using System.Drawing.Drawing2D;

namespace StealthNotes;

// Virtual image strip painted into the viewport, never a huge off-screen control/buffer.
internal sealed class NoteGallery : IDisposable
{
    readonly PreviewCache.Session previews;
    readonly ContextMenuStrip menu=new();
    IReadOnlyList<NoteImage> attachments=Array.Empty<NoteImage>();
    readonly List<(NoteImage Image,Rectangle Bounds)> frames=[];
    Guid? contextImage;
    Color paper,ink;
    Rectangle viewport;
    int measuredWidth,measuredGap;
    bool visible,disposed;
    public event Action<Guid>? RemoveRequested;
    public event Action? LayoutRequested,RepaintRequested;
    public NoteGallery(ImageStore images)
    {
        previews=images.Previews.CreateSession(()=>RepaintRequested?.Invoke());
        menu.Items.Add("Remove image",null,(_,_)=>{if(contextImage is { } id)RemoveRequested?.Invoke(id);});
    }
    public void Render(IReadOnlyList<NoteImage> images,Color background,Color foreground)
    {
        attachments=images;paper=background;ink=foreground;measuredWidth=0;
        LayoutRequested?.Invoke();RepaintRequested?.Invoke();
    }
    public int MeasureHeight(int width,int gap)
    {
        if(width==measuredWidth&&gap==measuredGap)return frames.Count==0?0:frames[^1].Bounds.Bottom;
        measuredWidth=width;measuredGap=gap;frames.Clear();int y=0;
        foreach(var image in attachments){
            int height=(int)Math.Clamp(Math.Round((double)width*Math.Max(1,image.Height)/Math.Max(1,image.Width)),1,1_000_000);
            frames.Add((image,new(0,y,width,height)));y=checked(y+height+gap);
        }
        return frames.Count==0?0:frames[^1].Bounds.Bottom;
    }
    public void UpdateViewport(Rectangle bounds,bool isVisible)
    {
        if(disposed)return;viewport=bounds;visible=isVisible;RequestVisible();
    }
    void RequestVisible()=>previews.Request(visible?
        frames.Where(f=>f.Bounds.IntersectsWith(viewport)).Select(f=>PreviewKey.For(f.Image.Id,f.Bounds.Width)):
        Array.Empty<PreviewKey>());
    public void Draw(Graphics graphics,Font font)
    {
        RequestVisible();graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
        foreach(var frame in frames){
            if(!frame.Bounds.IntersectsWith(viewport))continue;
            var key=PreviewKey.For(frame.Image.Id,frame.Bounds.Width);var bitmap=previews.Get(key);
            if(bitmap is not null)graphics.DrawImage(bitmap,ImageStore.Fit(bitmap.Size,frame.Bounds));
            else {
                using var brush=new SolidBrush(NoteChrome.Blend(paper,ink,.06f));graphics.FillRectangle(brush,frame.Bounds);
                var area=Rectangle.Intersect(frame.Bounds,viewport);
                // GDI TextRenderer does not follow Graphics transforms; use DrawString here.
                using var text=new SolidBrush(ink);using var format=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
                graphics.DrawString(previews.Failed(key)?"Image unavailable":"Loading image...",font,text,area,format);
            }
        }
    }
    public void ShowContextMenu(Control owner,Point point,Point origin)
    {
        var imagePoint=new Point(point.X-origin.X,point.Y-origin.Y);
        contextImage=frames.Where(f=>f.Bounds.Contains(imagePoint)).Select(f=>(Guid?)f.Image.Id).FirstOrDefault();
        if(contextImage is not null)menu.Show(owner,point);
    }
    public void Dispose(){if(disposed)return;disposed=true;previews.Dispose();menu.Dispose();}
}
