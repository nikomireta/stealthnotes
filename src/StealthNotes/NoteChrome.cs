using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace StealthNotes;

internal static class NoteChrome
{
    public static Color Blend(Color background,Color foreground,float amount)=>Color.FromArgb(
        (int)Math.Round(background.R+(foreground.R-background.R)*amount),
        (int)Math.Round(background.G+(foreground.G-background.G)*amount),
        (int)Math.Round(background.B+(foreground.B-background.B)*amount));

    public static GraphicsPath RoundedPath(RectangleF bounds,float radius)
    {
        var path=new GraphicsPath();
        float diameter=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));
        if(diameter<=0)return path;
        path.AddArc(bounds.Left,bounds.Top,diameter,diameter,180,90);
        path.AddArc(bounds.Right-diameter,bounds.Top,diameter,diameter,270,90);
        path.AddArc(bounds.Right-diameter,bounds.Bottom-diameter,diameter,diameter,0,90);
        path.AddArc(bounds.Left,bounds.Bottom-diameter,diameter,diameter,90,90);
        path.CloseFigure();return path;
    }
}

internal class BufferedPanel : Panel
{
    public BufferedPanel()=>SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);
}

internal enum NoteGlyph { Add,Ghost,More,Close,Image }

// Original vector drawings; no font glyph or external icon asset is used.
internal sealed class GlyphButton : Button
{
    readonly NoteGlyph glyph;
    bool hovered,pressed,active,error;
    [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Active {get=>active;set {if(active==value)return;active=value;Invalidate();}}
    [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasError {get=>error;set {if(error==value)return;error=value;Invalidate();}}
    public GlyphButton(NoteGlyph glyph,string accessibleName)
    {
        this.glyph=glyph;AccessibleName=accessibleName;AccessibleRole=AccessibleRole.PushButton;
        Text="";FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        TabStop=true;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
    }
    protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnMouseCaptureChanged(EventArgs e){pressed=false;Invalidate();base.OnMouseCaptureChanged(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
        float scale=DeviceDpi/96f;
        using var background=NoteChrome.RoundedPath(new RectangleF(1,1,Width-2,Height-2),5*scale);
        if(Enabled&&(hovered||pressed||active)) {
            using var brush=new SolidBrush(NoteChrome.Blend(BackColor,ForeColor,pressed?.14f:active?.10f:.07f));
            g.FillPath(brush,background);
        }
        if(Focused&&ShowFocusCues) {
            using var focusPen=new Pen(NoteChrome.Blend(BackColor,ForeColor,.55f),scale);
            g.DrawPath(focusPen,background);
        }
        var saved=g.Save();
        g.TranslateTransform((Width-30*scale)/2,(Height-30*scale)/2);g.ScaleTransform(scale,scale);
        Color ink=Enabled?ForeColor:NoteChrome.Blend(BackColor,ForeColor,.4f);
        using var pen=new Pen(ink,1.5f) {StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        using var fill=new SolidBrush(ink);
        switch(glyph) {
            case NoteGlyph.Image:
                g.DrawRectangle(pen,7,8,16,14);g.DrawEllipse(pen,16,11,3,3);
                g.DrawLines(pen,[new PointF(8,20),new(12,15),new(16,19),new(19,16),new(22,20)]);break;
            case NoteGlyph.Add:g.DrawLine(pen,8,15,22,15);g.DrawLine(pen,15,8,15,22);break;
            case NoteGlyph.Close:g.DrawLine(pen,10,10,20,20);g.DrawLine(pen,20,10,10,20);break;
            case NoteGlyph.More:
                for(int x=9;x<=21;x+=6)g.FillEllipse(fill,x-1.2f,13.8f,2.4f,2.4f);break;
            case NoteGlyph.Ghost:
                using(var shape=new GraphicsPath()) {
                    shape.AddArc(8,7,14,14,180,180);shape.AddLine(22,14,22,23);
                    shape.AddLines([new PointF(22,23),new(18.5f,20),new(15,23),new(11.5f,20),new(8,23),new(8,14)]);
                    shape.CloseFigure();g.DrawPath(pen,shape);
                }
                g.FillEllipse(fill,11,13,2,2);g.FillEllipse(fill,17,13,2,2);break;
        }
        if(error) {using var warning=new SolidBrush(Color.Firebrick);g.FillEllipse(warning,22,3,5,5);}
        g.Restore(saved);
    }
}
