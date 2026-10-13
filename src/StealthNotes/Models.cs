using System.Collections.ObjectModel;

namespace StealthNotes;

internal sealed record Note(Guid Id, string Text, string Theme, int X, int Y, int W, int H,
    bool Ghost, double Opacity, double GhostOpacity, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    bool SessionHidden = false, string? Rtf = null, bool IsOpen = true, IReadOnlyList<NoteImage>? Attachments = null)
{
    public IReadOnlyList<NoteImage> Images => Attachments ?? Array.Empty<NoteImage>();
    public bool HasContent => !string.IsNullOrWhiteSpace(Text) || Images.Count>0;
    public Rectangle Bounds => new(X, Y, W, H);
    public double EffectiveOpacity(bool globalGhost) => globalGhost || Ghost ? GhostOpacity : Opacity;
}

internal sealed record NoteImage(Guid Id,string Mime,int Width,int Height);

internal enum HotkeyAction { NewNote = 1, ToggleNoteGhost, ToggleGlobalGhost, ToggleVisibility, OpacityUp, OpacityDown }

internal sealed record KeyBinding(HotkeyAction Action, uint Modifiers, Keys Key)
{
    public override string ToString() => string.Join(" + ", new[] {
        (Modifiers & 2) != 0 ? "Ctrl" : null, (Modifiers & 1) != 0 ? "Alt" : null,
        (Modifiers & 4) != 0 ? "Shift" : null, Key.ToString()
    }.Where(s => s is not null));
}

internal sealed record HotkeySettings(KeyBinding[] Bindings)
{
    public static HotkeySettings Default => new([
        new(HotkeyAction.NewNote, 3, Keys.N),
        new(HotkeyAction.ToggleNoteGhost, 3, Keys.G),
        new(HotkeyAction.ToggleGlobalGhost, 7, Keys.G),
        new(HotkeyAction.ToggleVisibility, 3, Keys.H),
        new(HotkeyAction.OpacityUp, 3, Keys.Up),
        new(HotkeyAction.OpacityDown, 3, Keys.Down)
    ]);
    public HotkeySettings Copy() => new(Bindings.ToArray());
}

internal sealed record Snapshot(IReadOnlyDictionary<Guid, Note> Notes, bool GlobalGhost, bool AllHidden, HotkeySettings Hotkeys)
{
    public static IReadOnlyDictionary<Guid, Note> Freeze(IEnumerable<Note> notes) =>
        new ReadOnlyDictionary<Guid, Note>(notes.ToDictionary(n => n.Id));
}

internal readonly record struct Palette(string Id, string Name, Color Paper, Color Header, Color Ink);

internal static class Themes
{
    public static readonly Palette[] All = [
        new("yellow", "Yellow", Color.FromArgb(255,248,210), Color.FromArgb(253,241,182), Color.FromArgb(58,51,34)),
        new("pink", "Pink", Color.FromArgb(255,235,240), Color.FromArgb(251,220,230), Color.FromArgb(64,42,49)),
        new("mint", "Mint", Color.FromArgb(233,247,236), Color.FromArgb(214,237,220), Color.FromArgb(37,57,43)),
        new("blue", "Blue", Color.FromArgb(233,242,255), Color.FromArgb(213,229,250), Color.FromArgb(39,53,73)),
        new("violet", "Purple", Color.FromArgb(244,236,255), Color.FromArgb(230,217,249), Color.FromArgb(56,43,72)),
        new("dark", "Dark", Color.FromArgb(39,40,45), Color.FromArgb(48,49,55), Color.FromArgb(239,239,243))
    ];
    public static Palette Get(string id) => All.FirstOrDefault(p => p.Id == id, All[0]);
}

internal interface IAction { }
internal sealed record AddNote(Note Note) : IAction;
internal sealed record UpdateDocument(Guid Id, string Text, string Rtf) : IAction;
internal sealed record OpenNote(Guid Id) : IAction;
internal sealed record OpenAllNotes : IAction;
internal sealed record AddImage(Guid NoteId, NoteImage Image) : IAction;
internal sealed record RemoveImage(Guid NoteId, Guid ImageId) : IAction;
internal sealed record MoveNote(Guid Id, Rectangle Bounds) : IAction;
internal sealed record ChangeTheme(Guid Id, string Theme) : IAction;
internal sealed record ToggleNoteGhost(Guid Id) : IAction;
internal sealed record ChangeOpacity(Guid Id, double Value) : IAction;
internal sealed record ToggleGlobalGhost : IAction;
internal sealed record SetAllHidden(bool Hidden) : IAction;
internal sealed record CloseNote(Guid Id) : IAction;
internal sealed record DeleteNote(Guid Id) : IAction;
internal sealed record ChangeHotkeys(HotkeySettings Settings) : IAction;

// The only owner of note/preferences state. Windows render snapshots and send actions.
internal sealed class AppState
{
    public Snapshot Current { get; private set; }
    public event Action<Snapshot>? Changed;
    public AppState(Snapshot initial) => Current = initial;

    public void Dispatch(IAction action)
    {
        var before = Current;
        var notes = before.Notes.ToDictionary();
        bool global = before.GlobalGhost, hidden = before.AllHidden;
        var keys = before.Hotkeys;
        void Update(Guid id, Func<Note, Note> change, bool edited=true)
        {
            if (!notes.TryGetValue(id, out var old)) return;
            var next = change(old);
            if (next != old) notes[id] = edited?next with { UpdatedAt = DateTimeOffset.UtcNow }:next;
        }
        switch (action)
        {
            case AddNote a:
                if(hidden)foreach(var id in notes.Keys.ToArray())notes[id]=notes[id] with {SessionHidden=true};
                notes[a.Note.Id] = a.Note; hidden = false; break;
            case UpdateDocument a: Update(a.Id, n => n with { Text = a.Text,Rtf=a.Rtf }); break;
            case MoveNote a: Update(a.Id, n => n with { X=a.Bounds.X, Y=a.Bounds.Y, W=a.Bounds.Width, H=a.Bounds.Height },false); break;
            case OpenNote a:
                if(hidden)foreach(var id in notes.Keys.ToArray())notes[id]=notes[id] with {SessionHidden=true};
                Update(a.Id,n=>n with {IsOpen=true,SessionHidden=false},false);hidden=false;break;
            case OpenAllNotes:
                foreach(var id in notes.Keys.ToArray())notes[id]=notes[id] with {IsOpen=true,SessionHidden=false};hidden=false;break;
            case AddImage a: Update(a.NoteId,n=>n with {Attachments=Array.AsReadOnly(n.Images.Append(a.Image).ToArray())});break;
            case RemoveImage a: Update(a.NoteId,n=>n with {Attachments=Array.AsReadOnly(n.Images.Where(i=>i.Id!=a.ImageId).ToArray())});break;
            case ChangeTheme a: Update(a.Id, n => n with { Theme = Themes.Get(a.Theme).Id }); break;
            case ToggleNoteGhost a: Update(a.Id, n => n with { Ghost = !n.Ghost }); break;
            case ChangeOpacity a:
                Update(a.Id, n => before.GlobalGhost || n.Ghost
                    ? n with { GhostOpacity = Math.Clamp(a.Value, .1, 1) }
                    : n with { Opacity = Math.Clamp(a.Value, .1, 1) }); break;
            case ToggleGlobalGhost: global = !global; break;
            case SetAllHidden a:
                hidden = a.Hidden;
                if (!hidden) foreach (var id in notes.Keys.ToArray()) notes[id] = notes[id] with { SessionHidden = false };
                break;
            case CloseNote a:
                if (notes.TryGetValue(a.Id, out var n)) {
                    if (n.HasContent) notes[a.Id] = n with { IsOpen=false,SessionHidden=false };
                    else notes.Remove(a.Id);
                }
                break;
            case DeleteNote a: notes.Remove(a.Id); break;
            case ChangeHotkeys a: keys = a.Settings.Copy(); break;
        }
        if (global == before.GlobalGhost && hidden == before.AllHidden && ReferenceEquals(keys, before.Hotkeys)
            && notes.Count == before.Notes.Count && notes.All(p => before.Notes.TryGetValue(p.Key, out var n) && n == p.Value)) return;
        Current = new(Snapshot.Freeze(notes.Values), global, hidden, keys);
        Changed?.Invoke(Current);
    }
}
