using System.Security.Cryptography;
using System.Text;

namespace StealthNotes;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo("en-US");
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>{
            LocalLog.Error("UI error",e.Exception);
            MessageBox.Show("Something went wrong. Saved notes are safe. Check errors.log and restart the app if needed.",
                "StealthNotes",MessageBoxButtons.OK,MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException+=(_,e)=>{if(e.ExceptionObject is Exception error)LocalLog.Error("Fatal error",error);};
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StealthNotes");
        LocalLog.DirectoryPath=directory;
        string identity=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(directory.ToUpperInvariant())))[..16];
        // Inno Setup checks existence before replacing files or uninstalling.
        using var installerGuard=new Mutex(false,@"Local\StealthNotes.InstallerGuard");
        using var mutex=new Mutex(true,@"Local\StealthNotes."+identity,out bool first);
        if(!first){
            try {using var signal=EventWaitHandle.OpenExisting(@"Local\StealthNotes.Open."+identity);signal.Set();}
            catch(WaitHandleCannotBeOpenedException){MessageBox.Show("StealthNotes is starting. If it is already running, use the tray icon.","StealthNotes");}
            return;
        }
        try {
            Directory.CreateDirectory(directory);
            using var signal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\StealthNotes.Open."+identity);
            using var store=new NoteStore(Path.Combine(directory,"notes.sqlite3"));
            using var context=new NotesContext(store,signal);Application.Run(context);
        }catch(Exception e){
            LocalLog.Error("Startup/shutdown error",e);
            MessageBox.Show("StealthNotes could not open or save your notes. Your database has not been deleted.\n\n"+e.Message+"\n\nFolder: "+directory,
                "StealthNotes",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }finally{mutex.ReleaseMutex();}
    }
}
