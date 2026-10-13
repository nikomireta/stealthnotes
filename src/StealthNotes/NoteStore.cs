using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace StealthNotes;

internal sealed class NoteStore : IDisposable
{
    readonly SqliteConnection connection;
    Snapshot? lastSaved;
    public NoteStore(string path)
    {
        connection = new(new SqliteConnectionStringBuilder { DataSource=path, Mode=SqliteOpenMode.ReadWriteCreate,
            Pooling=false, DefaultTimeout=5 }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000; PRAGMA user_version;";
        command.ExecuteNonQuery();
        command.CommandText = "PRAGMA user_version";
        long version = Convert.ToInt64(command.ExecuteScalar());
        if (version > 1) throw new InvalidOperationException("This database was created by a newer version of StealthNotes. Please update the app.");
        if (version == 0)
        {
            using var transaction = connection.BeginTransaction();
            command.Transaction = transaction;
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS notes (
                    id TEXT PRIMARY KEY, text TEXT NOT NULL, theme TEXT NOT NULL,
                    x INTEGER NOT NULL, y INTEGER NOT NULL, w INTEGER NOT NULL, h INTEGER NOT NULL,
                    ghost INTEGER NOT NULL CHECK(ghost IN (0,1)),
                    opacity REAL NOT NULL CHECK(opacity BETWEEN 0.1 AND 1.0),
                    ghost_opacity REAL NOT NULL CHECK(ghost_opacity BETWEEN 0.1 AND 1.0),
                    created_at TEXT NOT NULL, updated_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                PRAGMA user_version=1;
                """;
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    public Snapshot Load()
    {
        var notes = new List<Note>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,text,theme,x,y,w,h,ghost,opacity,ghost_opacity,created_at,updated_at FROM notes ORDER BY created_at";
        using (var r = command.ExecuteReader()) while (r.Read())
            notes.Add(new(Guid.Parse(r.GetString(0)), r.GetString(1), Themes.Get(r.GetString(2)).Id,
                r.GetInt32(3),r.GetInt32(4),Math.Clamp(r.GetInt32(5),248,10000),Math.Clamp(r.GetInt32(6),180,10000),
                r.GetBoolean(7),Math.Clamp(r.GetDouble(8),.1,1),Math.Clamp(r.GetDouble(9),.1,1),
                DateTimeOffset.Parse(r.GetString(10),CultureInfo.InvariantCulture),DateTimeOffset.Parse(r.GetString(11),CultureInfo.InvariantCulture)));
        command.CommandText = "SELECT key,value FROM settings";
        var settings = new Dictionary<string,string>();
        using (var r = command.ExecuteReader()) while (r.Read()) settings[r.GetString(0)] = r.GetString(1);
        var keys = HotkeySettings.Default;
        if (settings.TryGetValue("hotkeys", out var serialized))
        {
            try { keys = JsonSerializer.Deserialize<HotkeySettings>(serialized) ?? keys; }
            catch (JsonException e) { LocalLog.Error("Invalid hotkey settings; defaults used", e); }
        }
        if (!Hotkeys.Validate(keys, out _)) keys = HotkeySettings.Default;
        lastSaved = new(Snapshot.Freeze(notes), settings.GetValueOrDefault("global_ghost") == "1",
            settings.GetValueOrDefault("all_hidden") == "1", keys);
        return lastSaved;
    }

    // Called by a single serialized writer, never concurrently with Load/Dispose.
    public void Save(Snapshot state)
    {
        using var tx = connection.BeginTransaction();
        foreach (var note in state.Notes.Values)
        {
            if (lastSaved?.Notes.TryGetValue(note.Id, out var old) == true && (old with { SessionHidden=false }) == (note with { SessionHidden=false })) continue;
            using var c = connection.CreateCommand(); c.Transaction=tx;
            c.CommandText = """
                INSERT INTO notes(id,text,theme,x,y,w,h,ghost,opacity,ghost_opacity,created_at,updated_at)
                VALUES($id,$text,$theme,$x,$y,$w,$h,$ghost,$opacity,$ghost_opacity,$created,$updated)
                ON CONFLICT(id) DO UPDATE SET text=excluded.text,theme=excluded.theme,x=excluded.x,y=excluded.y,
                    w=excluded.w,h=excluded.h,ghost=excluded.ghost,opacity=excluded.opacity,
                    ghost_opacity=excluded.ghost_opacity,updated_at=excluded.updated_at;
                """;
            foreach (var (key,value) in new (string,object)[] {
                ("id",note.Id.ToString("N")),("text",note.Text),("theme",note.Theme),("x",note.X),("y",note.Y),
                ("w",note.W),("h",note.H),("ghost",note.Ghost?1:0),("opacity",note.Opacity),("ghost_opacity",note.GhostOpacity),
                ("created",note.CreatedAt.ToString("o")),("updated",note.UpdatedAt.ToString("o")) }) c.Parameters.AddWithValue("$"+key,value);
            c.ExecuteNonQuery();
        }
        if (lastSaved is not null) foreach (var id in lastSaved.Notes.Keys.Except(state.Notes.Keys))
        {
            using var c=connection.CreateCommand();c.Transaction=tx;c.CommandText="DELETE FROM notes WHERE id=$id";
            c.Parameters.AddWithValue("$id",id.ToString("N"));c.ExecuteNonQuery();
        }
        foreach (var (key,value) in new[] {("global_ghost",state.GlobalGhost?"1":"0"),
            ("all_hidden",state.AllHidden?"1":"0"),("hotkeys",JsonSerializer.Serialize(state.Hotkeys))})
        {
            using var c=connection.CreateCommand();c.Transaction=tx;
            c.CommandText="INSERT INTO settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            c.Parameters.AddWithValue("$key",key);c.Parameters.AddWithValue("$value",value);c.ExecuteNonQuery();
        }
        tx.Commit(); lastSaved=state;
    }
    public void Dispose() => connection.Dispose();
}

internal sealed class Persistence : IDisposable
{
    readonly NoteStore store;
    readonly System.Windows.Forms.Timer debounce=new() { Interval=500 };
    readonly SynchronizationContext ui;
    Task writer=Task.CompletedTask;
    Snapshot? pending;
    volatile Exception? failure;
    bool disposed;
    public event Action<Exception?>? StatusChanged;
    public Persistence(NoteStore store, SynchronizationContext ui)
    { this.store=store;this.ui=ui;debounce.Tick+=(_,_)=>{debounce.Stop();EnqueuePending();}; }
    public void Schedule(Snapshot snapshot) { pending=snapshot;debounce.Stop();debounce.Start(); }
    void EnqueuePending()
    {
        if(pending is not { } snapshot) return; pending=null;
        writer=writer.ContinueWith(_=>{
            try { store.Save(snapshot); if(failure is not null){failure=null;Notify(null);} }
            catch(Exception e) { bool first=failure is null;failure=e;LocalLog.Error("Autosave failed",e);if(first)Notify(e); }
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
