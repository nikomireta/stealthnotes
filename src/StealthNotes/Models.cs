using System.Collections.ObjectModel;

namespace StealthNotes;

internal sealed record Note(Guid Id, string Text, string Theme, int X, int Y, int W, int H,
    bool Ghost, double Opacity, double GhostOpacity, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    bool SessionHidden = false)
{
    public Rectangle Bounds => new(X, Y, W, H);
    public double EffectiveOpacity(bool globalGhost) => globalGhost || Ghost ? GhostOpacity : Opacity;
}

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
        new("yellow", "Yellow", Color.FromArgb(255,244,181), Color.FromArgb(250,231,141), Color.FromArgb(48,42,28)),
        new("pink", "Pink", Color.FromArgb(255,223,230), Color.FromArgb(245,198,210), Color.FromArgb(62,36,43)),
        new("mint", "Mint", Color.FromArgb(219,244,224), Color.FromArgb(189,226,200), Color.FromArgb(30,54,38)),
        new("blue", "Blue", Color.FromArgb(219,235,255), Color.FromArgb(189,213,246), Color.FromArgb(31,47,70)),
        new("violet", "Purple", Color.FromArgb(237,225,255), Color.FromArgb(218,199,244), Color.FromArgb(48,35,67)),
        new("dark", "Dark", Color.FromArgb(37,38,42), Color.FromArgb(47,48,53), Color.FromArgb(242,242,244))
    ];
    public static Palette Get(string id) => All.FirstOrDefault(p => p.Id == id, All[0]);
}

internal interface IAction { }
internal sealed record AddNote(Note Note) : IAction;
internal sealed record EditText(Guid Id, string Text) : IAction;
internal sealed record MoveNote(Guid Id, Rectangle Bounds) : IAction;
internal sealed record ChangeTheme(Guid Id, string Theme) : IAction;
internal sealed record ToggleNoteGhost(Guid Id) : IAction;
internal sealed record ChangeOpacity(Guid Id, double Value) : IAction;
internal sealed record ToggleGlobalGhost : IAction;
internal sealed record SetAllHidden(bool Hidden) : IAction;
internal sealed record HideNote(Guid Id) : IAction;
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
        void Update(Guid id, Func<Note, Note> change)
        {
            if (!notes.TryGetValue(id, out var old)) return;
            var next = change(old);
            if (next != old) notes[id] = next with { UpdatedAt = DateTimeOffset.UtcNow };
        }
        switch (action)
        {
            case AddNote a:
                if(hidden)foreach(var id in notes.Keys.ToArray())notes[id]=notes[id] with {SessionHidden=true};
                notes[a.Note.Id] = a.Note; hidden = false; break;
            case EditText a: Update(a.Id, n => n with { Text = a.Text }); break;
            case MoveNote a: Update(a.Id, n => n with { X=a.Bounds.X, Y=a.Bounds.Y, W=a.Bounds.Width, H=a.Bounds.Height }); break;
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
            case HideNote a: if (notes.TryGetValue(a.Id, out var n)) notes[a.Id] = n with { SessionHidden = true }; break;
            case DeleteNote a: notes.Remove(a.Id); break;
            case ChangeHotkeys a: keys = a.Settings.Copy(); break;
        }
        if (global == before.GlobalGhost && hidden == before.AllHidden && ReferenceEquals(keys, before.Hotkeys)
            && notes.Count == before.Notes.Count && notes.All(p => before.Notes.TryGetValue(p.Key, out var n) && n == p.Value)) return;
        Current = new(Snapshot.Freeze(notes.Values), global, hidden, keys);
        Changed?.Invoke(Current);
    }
}
