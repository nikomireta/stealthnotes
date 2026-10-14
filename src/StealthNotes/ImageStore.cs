using System.Buffers.Binary;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Data.Sqlite;

namespace StealthNotes;

internal sealed record ImagePayload(NoteImage Image,byte[] Data,byte[] Thumbnail);

// Owns pending import bytes, serialized decoding, read-only image queries, and a UI-thread LRU.
// AppState contains metadata only. Writer leases keep pending bytes alive until queued saves finish.
internal sealed class ImageStore : IDisposable
{
    const int MaxBytes=10*1024*1024;
    readonly string databasePath;
    readonly SemaphoreSlim decoder=new(1,1);
    readonly CancellationTokenSource shutdown=new();
    readonly object stagingLock=new();
    readonly Dictionary<Guid,Pending> staged=[];
    public PreviewCache Previews {get;}
    volatile bool disposed;
    sealed class Pending(ImagePayload payload){public ImagePayload Payload=payload;public bool Live=true;public int Leases;}
    public ImageStore(string databasePath,SynchronizationContext ui)
    {
        this.databasePath=databasePath;
        Previews=new((key,token)=>Decode(()=>DecodePreview(key,token),token),ui);
    }

    async Task<T> Decode<T>(Func<T> work,CancellationToken cancellation=default)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token,cancellation);
        await decoder.WaitAsync(linked.Token).ConfigureAwait(false);
        try{return await Task.Run(work,linked.Token).ConfigureAwait(false);}
        finally{decoder.Release();}
    }
    public async Task<NoteImage> ImportFile(string path)
    {
        var payload=await Decode(()=>{
            using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(file.Length>MaxBytes)throw new InvalidOperationException("Images must be 10 MiB or smaller.");
            using var memory=new MemoryStream();file.CopyTo(memory);
            byte[] bytes=memory.ToArray();var (width,height,mime)=Dimensions(bytes);CheckSize(width,height);
            using var input=new MemoryStream(bytes,false);using var image=Image.FromStream(input,true,true);
            CheckSize(image.Width,image.Height);
            if(mime is "image/gif" or "image/bmp") {bytes=Png(image);mime="image/png";}
            if(bytes.Length>MaxBytes)throw new InvalidOperationException("The converted image exceeds 10 MiB.");
            using var thumb=Resize(image,256);
            return new ImagePayload(new(Guid.NewGuid(),mime,image.Width,image.Height),bytes,Png(thumb));
        }).ConfigureAwait(false);
        return Stage(payload);
    }
    // Takes ownership of the clipboard image, including when import fails or shutdown is requested.
    public async Task<NoteImage> ImportClipboard(Image image)
    {
        try {
            var payload=await Decode(()=>{
                CheckSize(image.Width,image.Height);byte[] bytes=Png(image);
                if(bytes.Length>MaxBytes)throw new InvalidOperationException("Clipboard images must be 10 MiB or smaller when saved as PNG.");
                using var thumb=Resize(image,256);
                return new ImagePayload(new(Guid.NewGuid(),"image/png",image.Width,image.Height),bytes,Png(thumb));
            }).ConfigureAwait(false);
            return Stage(payload);
        } finally {image.Dispose();}
    }
    NoteImage Stage(ImagePayload payload)
    {
        lock(stagingLock){if(disposed)throw new OperationCanceledException();staged.Add(payload.Image.Id,new(payload));}
        return payload.Image;
    }
    public ImagePayload GetPayload(Guid id)
    {
        lock(stagingLock)return staged.TryGetValue(id,out var item)?item.Payload:throw new InvalidOperationException("An image is not available for saving. Keep the app open and retry.");
    }
    public void SetLiveReferences(Snapshot state)
    {
        var ids=state.Notes.Values.SelectMany(n=>n.Images).Select(i=>i.Id).ToHashSet();
        Previews.Retain(ids);
        lock(stagingLock)foreach(var (id,item) in staged.ToArray()){
            item.Live=ids.Contains(id);if(!item.Live&&item.Leases==0)staged.Remove(id);
        }
    }
    public void Discard(Guid id){lock(stagingLock){if(staged.TryGetValue(id,out var item)){item.Live=false;if(item.Leases==0)staged.Remove(id);}}}
    public IDisposable Lease(Snapshot snapshot)
    {
        Guid[] ids=snapshot.Notes.Values.SelectMany(n=>n.Images).Select(i=>i.Id).Distinct().ToArray();
        lock(stagingLock)foreach(var id in ids)if(staged.TryGetValue(id,out var item))item.Leases++;
        return new Release(()=>{lock(stagingLock)foreach(var id in ids)if(staged.TryGetValue(id,out var item)){
            item.Leases--;if(!item.Live&&item.Leases==0)staged.Remove(id);
        }});
    }
    public void Committed(IEnumerable<Guid> ids){lock(stagingLock)foreach(var id in ids)staged.Remove(id);}
    sealed class Release(Action action) : IDisposable {Action? release=action;public void Dispose()=>Interlocked.Exchange(ref release,null)?.Invoke();}

    byte[] Read(Guid id,bool thumbnail)
    {
        lock(stagingLock)if(staged.TryGetValue(id,out var item))return thumbnail?item.Payload.Thumbnail:item.Payload.Data;
        using var reader=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=databasePath,Mode=SqliteOpenMode.ReadOnly,Pooling=false,DefaultTimeout=5}.ToString());
        reader.Open();using var c=reader.CreateCommand();
        c.CommandText=thumbnail?"SELECT thumbnail FROM note_images WHERE id=$id":"SELECT data FROM note_images WHERE id=$id";
        c.Parameters.AddWithValue("$id",id.ToString("N"));
        return c.ExecuteScalar() as byte[]??throw new InvalidOperationException("The image is no longer available.");
    }
    Bitmap DecodePreview(PreviewKey key,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var input=new MemoryStream(Read(key.Id,false),false);
        using var image=Image.FromStream(input,true,true);CheckSize(image.Width,image.Height);
        double scale=Math.Min(1,Math.Min((double)key.Width/image.Width,Math.Sqrt(2_000_000d/((long)image.Width*image.Height))));
        var result=new Bitmap(Math.Max(1,(int)(image.Width*scale)),Math.Max(1,(int)(image.Height*scale)),PixelFormat.Format32bppPArgb);
        try {
            cancellation.ThrowIfCancellationRequested();
            using(var graphics=Graphics.FromImage(result)){
                graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                graphics.Clear(Color.Transparent);graphics.DrawImage(image,new Rectangle(0,0,result.Width,result.Height));
            }
            cancellation.ThrowIfCancellationRequested();return result;
        }catch {result.Dispose();throw;}
    }
    public static Rectangle Fit(Size image,Rectangle bounds)
    {
        double scale=Math.Min((double)bounds.Width/image.Width,(double)bounds.Height/image.Height);
        int width=Math.Max(1,(int)(image.Width*scale)),height=Math.Max(1,(int)(image.Height*scale));
        return new(bounds.X+(bounds.Width-width)/2,bounds.Y+(bounds.Height-height)/2,width,height);
    }
    static Bitmap Resize(Image image,int edge)
    {
        double scale=Math.Min(1,(double)edge/Math.Max(image.Width,image.Height));
        var result=new Bitmap(Math.Max(1,(int)(image.Width*scale)),Math.Max(1,(int)(image.Height*scale)),PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(result);g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);g.DrawImage(image,new Rectangle(0,0,result.Width,result.Height));return result;
    }
    static byte[] Png(Image image){using var output=new MemoryStream();image.Save(output,ImageFormat.Png);return output.ToArray();}
    static void CheckSize(int width,int height)
    {
        if(width<=0||height<=0||(long)width*height>20_000_000)throw new InvalidOperationException("Images must contain at most 20 megapixels.");
    }
    static (int Width,int Height,string Mime) Dimensions(byte[] bytes)
    {
        var b=bytes.AsSpan();
        if(b.Length>=24&&b[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))
            return(BinaryPrimitives.ReadInt32BigEndian(b[16..]),BinaryPrimitives.ReadInt32BigEndian(b[20..]),"image/png");
        if(b.Length>=10&&(b[..6].SequenceEqual("GIF87a"u8)||b[..6].SequenceEqual("GIF89a"u8)))
            return(BinaryPrimitives.ReadUInt16LittleEndian(b[6..]),BinaryPrimitives.ReadUInt16LittleEndian(b[8..]),"image/gif");
        if(b.Length>=26&&b[0]=='B'&&b[1]=='M'){
            if(BinaryPrimitives.ReadInt32LittleEndian(b[14..])==12)
                return(BinaryPrimitives.ReadUInt16LittleEndian(b[18..]),BinaryPrimitives.ReadUInt16LittleEndian(b[20..]),"image/bmp");
            return(BinaryPrimitives.ReadInt32LittleEndian(b[18..]),Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(b[22..])),"image/bmp");
        }
        if(b.Length>4&&b[0]==255&&b[1]==216){
            int p=2;
            while(p+3<b.Length){
                if(b[p++]!=255)throw new InvalidOperationException("Invalid JPEG image.");
                while(p<b.Length&&b[p]==255)p++;
                if(p>=b.Length)break;int marker=b[p++];
                if(marker is 1 or >=208 and <=217)continue;
                if(p+2>b.Length)break;int length=BinaryPrimitives.ReadUInt16BigEndian(b[p..]);
                if(length<2||p+length>b.Length)break;
                if(marker is >=192 and <=207&&marker is not (196 or 200 or 204)&&length>=7)
                    return(BinaryPrimitives.ReadUInt16BigEndian(b[(p+5)..]),BinaryPrimitives.ReadUInt16BigEndian(b[(p+3)..]),"image/jpeg");
                p+=length;
            }
        }
        throw new InvalidOperationException("Choose a valid PNG, JPEG, BMP, or GIF image.");
    }
    public void Dispose()
    {
        disposed=true;shutdown.Cancel();
        Previews.Dispose();
        lock(stagingLock)staged.Clear();
    }
}
