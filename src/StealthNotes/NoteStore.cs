using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace StealthNotes;

internal sealed class NoteStore : IDisposable
{
    readonly SqliteConnection connection;
    readonly string databasePath;
    public string DatabasePath=>databasePath;
    Snapshot? lastSaved;
    public NoteStore(string path)
    {
        databasePath=Path.GetFullPath(path);
        connection=new(new SqliteConnectionStringBuilder {DataSource=databasePath,Mode=SqliteOpenMode.ReadWriteCreate,Pooling=false,DefaultTimeout=5}.ToString());
        try {
            connection.Open();
            using var c=connection.CreateCommand();
            c.CommandText="PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
            c.ExecuteNonQuery();c.CommandText="PRAGMA user_version";
            long version=Convert.ToInt64(c.ExecuteScalar());
            if(version>2)throw new InvalidOperationException("This database requires a newer version of StealthNotes.");
            if(version==0) {
                c.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                if(Convert.ToInt64(c.ExecuteScalar())!=0)throw new InvalidOperationException("Unrecognized database schema. Your database has not been changed.");
                using var tx=connection.BeginTransaction();c.Transaction=tx;
                c.CommandText="""
                    CREATE TABLE notes (
                        id TEXT PRIMARY KEY,text TEXT NOT NULL,theme TEXT NOT NULL,
                        x INTEGER NOT NULL,y INTEGER NOT NULL,w INTEGER NOT NULL,h INTEGER NOT NULL,
                        ghost INTEGER NOT NULL CHECK(ghost IN (0,1)),
                        opacity REAL NOT NULL CHECK(opacity BETWEEN 0.1 AND 1.0),
                        ghost_opacity REAL NOT NULL CHECK(ghost_opacity BETWEEN 0.1 AND 1.0),
                        created_at TEXT NOT NULL,updated_at TEXT NOT NULL,
                        content_rtf TEXT,is_open INTEGER NOT NULL DEFAULT 0 CHECK(is_open IN (0,1)));
                    CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT NOT NULL);
                    """+ImageSchema+"PRAGMA user_version=2;";
                c.ExecuteNonQuery();tx.Commit();
            } else if(version==1) {
                BackupBeforeMigration();
                // Match .NET whitespace semantics; SQL trim() would miss Unicode whitespace.
                var drafts=new List<string>();c.CommandText="SELECT id,text FROM notes";
                using(var reader=c.ExecuteReader())while(reader.Read())if(string.IsNullOrWhiteSpace(reader.GetString(1)))drafts.Add(reader.GetString(0));
                using var tx=connection.BeginTransaction();c.Transaction=tx;
                c.CommandText="""
                    ALTER TABLE notes ADD COLUMN content_rtf TEXT;
                    ALTER TABLE notes ADD COLUMN is_open INTEGER NOT NULL DEFAULT 0 CHECK(is_open IN (0,1));
                    """+ImageSchema;
                c.ExecuteNonQuery();
                foreach(string id in drafts){c.CommandText="DELETE FROM notes WHERE id=$id";c.Parameters.Clear();c.Parameters.AddWithValue("$id",id);c.ExecuteNonQuery();}
                c.Parameters.Clear();c.CommandText="DELETE FROM settings WHERE key='all_hidden'; PRAGMA user_version=2;";
                c.ExecuteNonQuery();tx.Commit();
            }
        } catch {connection.Dispose();throw;}
    }
    const string ImageSchema="""
        CREATE TABLE note_images (
            id TEXT PRIMARY KEY,note_id TEXT NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
            sort_order INTEGER NOT NULL,mime TEXT NOT NULL,width INTEGER NOT NULL,height INTEGER NOT NULL,
            data BLOB NOT NULL,thumbnail BLOB NOT NULL);
        CREATE INDEX ix_note_images_note ON note_images(note_id,sort_order);
        """;
    void BackupBeforeMigration()
    {
        string folder=Path.Combine(Path.GetDirectoryName(databasePath)!,"backups");
        string backup=Path.Combine(folder,"before-1.2.0.sqlite3");
        if(File.Exists(backup))return;
        string temporary=backup+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            Directory.CreateDirectory(folder);
            using(var target=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=temporary,Mode=SqliteOpenMode.ReadWriteCreate,Pooling=false}.ToString())){
                target.Open();connection.BackupDatabase(target);
            }
            File.Move(temporary,backup);
        } catch(Exception e) {throw new InvalidOperationException("Could not create the required upgrade backup. Migration was stopped without deleting notes. Check disk space and permissions.\n\nBackup: "+backup,e);}
        finally {try{if(File.Exists(temporary))File.Delete(temporary);}catch{/* Keep the original error. */}}
    }
    public Snapshot Load()
    {
        var images=new Dictionary<Guid,List<NoteImage>>();
        using var c=connection.CreateCommand();
        c.CommandText="SELECT id,note_id,mime,width,height FROM note_images ORDER BY sort_order";
        using(var r=c.ExecuteReader())while(r.Read()){
            var id=Guid.Parse(r.GetString(1));if(!images.TryGetValue(id,out var list))images[id]=list=[];
            list.Add(new(Guid.Parse(r.GetString(0)),r.GetString(2),r.GetInt32(3),r.GetInt32(4)));
        }
        var notes=new List<Note>();
        c.CommandText="SELECT id,text,theme,x,y,w,h,ghost,opacity,ghost_opacity,created_at,updated_at,content_rtf,is_open FROM notes ORDER BY created_at";
        using(var r=c.ExecuteReader())while(r.Read()){
            var id=Guid.Parse(r.GetString(0));
            notes.Add(new(id,r.GetString(1),Themes.Get(r.GetString(2)).Id,r.GetInt32(3),r.GetInt32(4),
                Math.Clamp(r.GetInt32(5),248,10000),Math.Clamp(r.GetInt32(6),180,10000),r.GetBoolean(7),
                Math.Clamp(r.GetDouble(8),.1,1),Math.Clamp(r.GetDouble(9),.1,1),
                DateTimeOffset.Parse(r.GetString(10),CultureInfo.InvariantCulture),DateTimeOffset.Parse(r.GetString(11),CultureInfo.InvariantCulture),
                false,r.IsDBNull(12)?null:r.GetString(12),r.GetBoolean(13),
                images.TryGetValue(id,out var list)?Array.AsReadOnly(list.ToArray()):null));
        }
        var settings=new Dictionary<string,string>();c.CommandText="SELECT key,value FROM settings";
        using(var r=c.ExecuteReader())while(r.Read())settings[r.GetString(0)]=r.GetString(1);
        var keys=HotkeySettings.Default;
        if(settings.TryGetValue("hotkeys",out string? data)){
            try{keys=JsonSerializer.Deserialize<HotkeySettings>(data)??keys;}catch(JsonException e){LocalLog.Error("Invalid hotkeys",e);}
        }
        if(!Hotkeys.Validate(keys,out _))keys=HotkeySettings.Default;
        lastSaved=new(Snapshot.Freeze(notes),settings.GetValueOrDefault("global_ghost")=="1",false,keys);
        return lastSaved;
    }
    public void Save(Snapshot snapshot,ImageStore images)
    {
        var persisted=snapshot with {Notes=Snapshot.Freeze(snapshot.Notes.Values.Where(n=>n.HasContent)),AllHidden=false};
        var writtenImages=new List<Guid>();
        using var tx=connection.BeginTransaction();
        foreach(var n in persisted.Notes.Values){
            Note? old=lastSaved is not null&&lastSaved.Notes.TryGetValue(n.Id,out var existing)?existing:null;
            if(old is null||(old with {SessionHidden=false})!=(n with {SessionHidden=false})){
                using var c=connection.CreateCommand();c.Transaction=tx;
                c.CommandText="""
                    INSERT INTO notes(id,text,theme,x,y,w,h,ghost,opacity,ghost_opacity,created_at,updated_at,content_rtf,is_open)
                    VALUES($id,$text,$theme,$x,$y,$w,$h,$ghost,$opacity,$ghost_opacity,$created,$updated,$rtf,$open)
                    ON CONFLICT(id) DO UPDATE SET text=excluded.text,theme=excluded.theme,x=excluded.x,y=excluded.y,w=excluded.w,h=excluded.h,
                    ghost=excluded.ghost,opacity=excluded.opacity,ghost_opacity=excluded.ghost_opacity,updated_at=excluded.updated_at,
                    content_rtf=excluded.content_rtf,is_open=excluded.is_open;
                    """;
                foreach(var (key,value) in new (string,object)[]{("id",n.Id.ToString("N")),("text",n.Text),("theme",n.Theme),("x",n.X),("y",n.Y),
                    ("w",n.W),("h",n.H),("ghost",n.Ghost?1:0),("opacity",n.Opacity),("ghost_opacity",n.GhostOpacity),
                    ("created",n.CreatedAt.ToString("o")),("updated",n.UpdatedAt.ToString("o")),("rtf",(object?)n.Rtf??DBNull.Value),("open",n.IsOpen?1:0)})
                    c.Parameters.AddWithValue("$"+key,value);
                c.ExecuteNonQuery();
            }
            var oldIds=(old?.Images??Array.Empty<NoteImage>()).Select(i=>i.Id).ToHashSet();
            bool orderChanged=old is null||!old.Images.Select(i=>i.Id).SequenceEqual(n.Images.Select(i=>i.Id));
            for(int index=0;index<n.Images.Count;index++){
                var image=n.Images[index];
                if(oldIds.Contains(image.Id)){
                    if(orderChanged){
                        using var order=connection.CreateCommand();order.Transaction=tx;
                        order.CommandText="UPDATE note_images SET sort_order=$order WHERE id=$id";
                        order.Parameters.AddWithValue("$order",index);order.Parameters.AddWithValue("$id",image.Id.ToString("N"));order.ExecuteNonQuery();
                    }
                    continue;
                }
                var payload=images.GetPayload(image.Id);
                using var c=connection.CreateCommand();c.Transaction=tx;
                c.CommandText="INSERT INTO note_images(id,note_id,sort_order,mime,width,height,data,thumbnail) VALUES($id,$note,$order,$mime,$w,$h,$data,$thumb)";
                c.Parameters.AddWithValue("$id",image.Id.ToString("N"));c.Parameters.AddWithValue("$note",n.Id.ToString("N"));
                c.Parameters.AddWithValue("$order",index);c.Parameters.AddWithValue("$mime",image.Mime);
                c.Parameters.AddWithValue("$w",image.Width);c.Parameters.AddWithValue("$h",image.Height);
                c.Parameters.AddWithValue("$data",payload.Data);c.Parameters.AddWithValue("$thumb",payload.Thumbnail);
                c.ExecuteNonQuery();writtenImages.Add(image.Id);
            }
            foreach(var id in oldIds.Except(n.Images.Select(i=>i.Id))){
                using var c=connection.CreateCommand();c.Transaction=tx;c.CommandText="DELETE FROM note_images WHERE id=$id";
                c.Parameters.AddWithValue("$id",id.ToString("N"));c.ExecuteNonQuery();
            }
        }
        if(lastSaved is not null)foreach(var id in lastSaved.Notes.Keys.Except(persisted.Notes.Keys)){
            using var c=connection.CreateCommand();c.Transaction=tx;c.CommandText="DELETE FROM notes WHERE id=$id";
            c.Parameters.AddWithValue("$id",id.ToString("N"));c.ExecuteNonQuery();
        }
        foreach(var (key,value) in new[]{("global_ghost",snapshot.GlobalGhost?"1":"0"),("hotkeys",JsonSerializer.Serialize(snapshot.Hotkeys))}){
            using var c=connection.CreateCommand();c.Transaction=tx;
            c.CommandText="INSERT INTO settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            c.Parameters.AddWithValue("$key",key);c.Parameters.AddWithValue("$value",value);c.ExecuteNonQuery();
        }
        tx.Commit();lastSaved=persisted;images.Committed(writtenImages);
    }
    public void Dispose()=>connection.Dispose();
}

internal sealed class Persistence : IDisposable
{
    readonly NoteStore store;
    readonly ImageStore images;
    readonly System.Windows.Forms.Timer debounce=new() { Interval=500 };
    readonly SynchronizationContext ui;
    Task writer=Task.CompletedTask;
    Snapshot? pending;
    volatile Exception? failure;
    bool disposed;
    public event Action<Exception?>? StatusChanged;
    public Persistence(NoteStore store, ImageStore images, SynchronizationContext ui)
    { this.store=store;this.images=images;this.ui=ui;debounce.Tick+=(_,_)=>{debounce.Stop();EnqueuePending();}; }
    public void Schedule(Snapshot snapshot) { pending=snapshot;debounce.Stop();debounce.Start(); }
    void EnqueuePending()
    {
        if(pending is not { } snapshot) return; pending=null;
        var lease=images.Lease(snapshot);
        writer=writer.ContinueWith(_=>{
            try { store.Save(snapshot,images); if(failure is not null){failure=null;Notify(null);} }
            catch(Exception e) { bool first=failure is null;failure=e;LocalLog.Error("Autosave failed",e);if(first)Notify(e); }
            finally {lease.Dispose();}
        },CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.Default);
    }
    void Notify(Exception? e)=>ui.Post(_=>{if(!disposed)StatusChanged?.Invoke(e);},null);
    public bool Flush(Snapshot snapshot)
    {
        debounce.Stop();pending=snapshot;EnqueuePending();writer.GetAwaiter().GetResult();return failure is null;
    }
    public void Dispose(){disposed=true;debounce.Dispose();writer.GetAwaiter().GetResult();}
}

internal static class LocalLog
{
    public static string DirectoryPath { get; set; } = "";
    public static void Error(string context, Exception e)
    {
        try {
            string file=Path.Combine(DirectoryPath,"errors.log");
            lock(typeof(LocalLog)) {
                if(File.Exists(file)&&new FileInfo(file).Length>256*1024) File.Move(file,file+".old",true);
                File.AppendAllText(file,$"{DateTimeOffset.UtcNow:o} {context}: {e.GetType().Name}: {e.Message}{Environment.NewLine}");
            }
        } catch { /* Logging must not replace the original error. */ }
    }
}
