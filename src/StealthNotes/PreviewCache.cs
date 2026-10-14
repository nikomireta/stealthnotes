namespace StealthNotes;

internal readonly record struct PreviewKey(Guid Id,int Width)
{
    public static PreviewKey For(Guid id,int width)=>new(id,Math.Clamp((width+127)/128*128,128,8192));
}

// UI-thread ownership: clients borrow bitmaps only for the duration of Paint.
// Every client declares its visible demand; obsolete queued/running requests are cancelled.
internal sealed class PreviewCache(Func<PreviewKey,CancellationToken,Task<Bitmap>> decode,SynchronizationContext ui) : IDisposable
{
    const long Limit=32*1024*1024;
    readonly Dictionary<PreviewKey,LinkedListNode<Entry>> cached=[];
    readonly LinkedList<Entry> recent=[];
    readonly Dictionary<PreviewKey,Request> pending=[];
    readonly HashSet<PreviewKey> failed=[];
    readonly HashSet<Session> sessions=[];
    long bytes;
    bool disposed;
    sealed record Entry(PreviewKey Key,Bitmap Bitmap) {public long Bytes=>(long)Bitmap.Width*Bitmap.Height*4;}
    sealed class Request : IDisposable
    {
        readonly CancellationTokenSource source=new();
        readonly object gate=new();
        bool disposed;
        public readonly CancellationToken Token;
        public Request(){Token=source.Token;}
        public void Cancel(){lock(gate){if(!disposed)source.Cancel();}}
        public void Dispose(){lock(gate){if(disposed)return;disposed=true;source.Dispose();}}
    }

    public Session CreateSession(Action ready)
    {
        var session=new Session(this,ready);sessions.Add(session);return session;
    }
    bool Wanted(PreviewKey key)=>sessions.Any(s=>s.Keys.Contains(key));
    void Update()
    {
        if(disposed)return;
        foreach(var (key,cancellation) in pending.ToArray())if(!Wanted(key)){
            pending.Remove(key);cancellation.Cancel();
        }
        // Only visible images enter the queue, in viewport order. The store serializes decoding.
        foreach(var key in sessions.SelectMany(s=>s.Keys).Distinct()){
            if(cached.ContainsKey(key)||pending.ContainsKey(key)||failed.Contains(key))continue;
            var cancellation=new Request();pending.Add(key,cancellation);
            _=Fetch(key,cancellation);
        }
    }
    async Task Fetch(PreviewKey key,Request cancellation)
    {
        Bitmap? result=null;Exception? error=null;
        try{result=await decode(key,cancellation.Token).ConfigureAwait(false);}
        catch(Exception e){error=e;}
        if(cancellation.Token.IsCancellationRequested){result?.Dispose();cancellation.Dispose();return;}
        Bitmap? delivered=result;
        var registration=cancellation.Token.Register(()=>Interlocked.Exchange(ref delivered,null)?.Dispose());
        try {ui.Post(_=>{
            try {
                var completed=Interlocked.Exchange(ref delivered,null);
                if(disposed||!pending.TryGetValue(key,out var current)||current!=cancellation||!Wanted(key)){
                    completed?.Dispose();return;
                }
                pending.Remove(key);
                if(completed is not null){
                    var entry=new Entry(key,completed);cached.Add(key,recent.AddFirst(entry));bytes+=entry.Bytes;
                    while(bytes>Limit&&recent.Last is { } oldest){
                        recent.RemoveLast();cached.Remove(oldest.Value.Key);bytes-=oldest.Value.Bytes;oldest.Value.Bitmap.Dispose();
                    }
                }else if(error is not OperationCanceledException){
                    failed.Add(key);if(error is not null)LocalLog.Error("Image preview read failed",error);
                }
                foreach(var session in sessions.ToArray())if(session.Keys.Contains(key))session.Ready();
            } finally {registration.Dispose();cancellation.Dispose();}
        },null);}catch(InvalidOperationException){
            // The dispatcher can disappear during shutdown, before a queued result is delivered.
            registration.Dispose();Interlocked.Exchange(ref delivered,null)?.Dispose();cancellation.Dispose();
        }
    }
    Bitmap? Get(PreviewKey key)
    {
        if(!cached.TryGetValue(key,out var node))return null;
        recent.Remove(node);recent.AddFirst(node);return node.Value.Bitmap;
    }
    public void Retain(HashSet<Guid> ids)
    {
        if(disposed)return;
        foreach(var session in sessions)session.Keys=session.Keys.Where(k=>ids.Contains(k.Id)).ToArray();
        foreach(var (key,node) in cached.ToArray())if(!ids.Contains(key.Id)){
            recent.Remove(node);cached.Remove(key);bytes-=node.Value.Bytes;node.Value.Bitmap.Dispose();
        }
        failed.RemoveWhere(k=>!ids.Contains(k.Id));Update();
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        foreach(var cancellation in pending.Values)cancellation.Cancel();pending.Clear();
        foreach(var entry in recent)entry.Bitmap.Dispose();recent.Clear();cached.Clear();bytes=0;
        sessions.Clear();
    }
    internal sealed class Session(PreviewCache owner,Action ready) : IDisposable
    {
        internal PreviewKey[] Keys=[];
        bool disposed;
        internal void Ready(){if(!disposed)ready();}
        public void Request(IEnumerable<PreviewKey> keys){if(disposed)return;Keys=keys.Distinct().ToArray();owner.Update();}
        public Bitmap? Get(PreviewKey key)=>disposed?null:owner.Get(key);
        public bool Failed(PreviewKey key)=>owner.failed.Contains(key);
        public void Dispose(){if(disposed)return;disposed=true;Keys=[];owner.sessions.Remove(this);owner.Update();}
    }
}
