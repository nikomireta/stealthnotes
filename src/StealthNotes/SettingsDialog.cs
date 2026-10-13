namespace StealthNotes;

internal sealed class SettingsDialog : Form
{
    readonly Dictionary<HotkeyAction,BindingBox> fields=[];
    readonly Label status;
    readonly Func<HotkeySettings,string?> apply;
    public static string ActionName(HotkeyAction action)=>action switch {
        HotkeyAction.NewNote=>"New note",HotkeyAction.ToggleNoteGhost=>"Toggle ghost for the note under the pointer",
        HotkeyAction.ToggleGlobalGhost=>"Toggle ghost for all notes",HotkeyAction.ToggleVisibility=>"Show / hide all notes",
        HotkeyAction.OpacityUp=>"Increase opacity",HotkeyAction.OpacityDown=>"Decrease opacity",_=>action.ToString()
    };
    public SettingsDialog(HotkeySettings settings,Func<HotkeySettings,string?> apply)
    {
        this.apply=apply;Text="StealthNotes settings";FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;TopMost=true;
        AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new(550,490);BackColor=Color.FromArgb(248,248,250);
        var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new(22),ColumnCount=2,RowCount=10 };
        layout.ColumnStyles.Add(new(SizeType.Percent,57));layout.ColumnStyles.Add(new(SizeType.Percent,43));
        var intro=new Label { Text="Click a field, then press your new shortcut.",AutoSize=true,Margin=new(0,0,0,16) };
        layout.Controls.Add(intro,0,0);layout.SetColumnSpan(intro,2);
        int row=1;
        foreach(var binding in settings.Bindings.OrderBy(b=>b.Action)){
            layout.Controls.Add(new Label {Text=ActionName(binding.Action),AutoSize=true,Anchor=AnchorStyles.Left,Margin=new(0,8,8,8)},0,row);
            var field=new BindingBox(binding){Dock=DockStyle.Fill,Margin=new(0,4,0,4)};fields[binding.Action]=field;layout.Controls.Add(field,1,row++);
        }
        var note=new Label { Text="Global ghost keeps each note's individual preference.\nHotkeys target the note under the pointer, then the last active note.",AutoSize=true,ForeColor=Color.DimGray,Margin=new(0,12,0,8) };
        layout.Controls.Add(note,0,row);layout.SetColumnSpan(note,2);row++;
        status=new Label {AutoSize=true,ForeColor=Color.Firebrick,MaximumSize=new(480,70)};
        layout.Controls.Add(status,0,row);layout.SetColumnSpan(status,2);row++;
        var buttons=new FlowLayoutPanel {AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Dock=DockStyle.Fill};
        var save=new Button {Text="Save",AutoSize=true};save.Click+=(_,_)=>{
            var proposed=new HotkeySettings(fields.Values.Select(f=>f.Binding).ToArray());
            var error=apply(proposed);if(error is not null){status.Text=error;return;}DialogResult=DialogResult.OK;Close();
        };
        var cancel=new Button {Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel};
        var reset=new Button {Text="Default",AutoSize=true};reset.Click+=(_,_)=>{foreach(var b in HotkeySettings.Default.Bindings)fields[b.Action].SetBinding(b);status.Text="";};
        buttons.Controls.AddRange([save,cancel,reset]);layout.Controls.Add(buttons,0,row);layout.SetColumnSpan(buttons,2);
        Controls.Add(layout);AcceptButton=save;CancelButton=cancel;
    }
    sealed class BindingBox : TextBox
    {
        public KeyBinding Binding {get;private set;}
        public BindingBox(KeyBinding binding){Binding=binding;ReadOnly=true;ShortcutsEnabled=false;SetBinding(binding);}
        public void SetBinding(KeyBinding binding){Binding=binding;Text=binding.ToString();}
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(keyData==Keys.Tab || keyData==(Keys.Shift|Keys.Tab))return base.ProcessCmdKey(ref msg,keyData);
            Keys key=keyData&Keys.KeyCode;
            if(key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)return true;
            uint mods=0;if((keyData&Keys.Control)!=0)mods|=2;if((keyData&Keys.Alt)!=0)mods|=1;if((keyData&Keys.Shift)!=0)mods|=4;
            if(mods!=0)SetBinding(Binding with {Key=key,Modifiers=mods});return true;
        }
    }
}
