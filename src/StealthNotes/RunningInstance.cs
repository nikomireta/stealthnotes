using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace StealthNotes;

internal static class RunningInstance
{
    internal sealed record Info(int Pid,DateTime StartedUtc,string Path,string Version);
    static string InfoPath(string directory)=>System.IO.Path.Combine(directory,"instance.json");
    static string VersionOf(string? version)=>System.Version.TryParse(version?.Split('+')[0],out var parsed)?$"{parsed.Major}.{parsed.Minor}.{Math.Max(0,parsed.Build)}":"unknown";
    public static void Write(string directory)
    {
        using var process=Process.GetCurrentProcess();
        var info=new Info(process.Id,process.StartTime.ToUniversalTime(),Application.ExecutablePath,VersionOf(Application.ProductVersion));
        string target=InfoPath(directory),temporary=target+"."+process.Id+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(info),new UTF8Encoding(false));File.Move(temporary,target,true);
    }
    public static void Remove(string directory)
    {
        try {
            string path=InfoPath(directory);
            if(File.Exists(path)&&JsonSerializer.Deserialize<Info>(File.ReadAllText(path))?.Pid==Environment.ProcessId)File.Delete(path);
        }catch(Exception e){LocalLog.Error("Could not remove instance metadata",e);}
    }
    public static bool CanActivate(string directory,out string explanation)
    {
        explanation="";
        try {
            using var current=WindowsIdentity.GetCurrent();
            foreach(var process in Process.GetProcessesByName("StealthNotes")){
                using(process){
                    if(process.Id==Environment.ProcessId)continue;
                    using var handle=OpenProcess(0x1000,false,process.Id);
                    if(handle.IsInvalid)throw new InvalidOperationException("Could not identify another StealthNotes process.");
                    if(!OpenProcessToken(handle,8,out var token))throw new InvalidOperationException("Could not identify the owner of another StealthNotes process.");
                    using(token){using var identity=new WindowsIdentity(token.DangerousGetHandle());if(identity.User!=current.User)continue;}
                    var path=new StringBuilder(32768);int size=path.Capacity;
                    if(!QueryFullProcessImageNameW(handle,0,path,ref size))throw new InvalidOperationException("Could not identify the running application path.");
                    string executable=path.ToString(),version="unknown";
                    string metadata=InfoPath(directory);
                    if(File.Exists(metadata)){
                        var info=JsonSerializer.Deserialize<Info>(File.ReadAllText(metadata));
                        if(info?.Pid==process.Id&&Math.Abs((info.StartedUtc-process.StartTime.ToUniversalTime()).TotalSeconds)<1&&StringComparer.OrdinalIgnoreCase.Equals(info.Path,executable))version=info.Version;
                    }
                    if(version=="unknown"&&File.GetLastWriteTimeUtc(executable)<=process.StartTime.ToUniversalTime())
                        version=VersionOf(FileVersionInfo.GetVersionInfo(executable).ProductVersion);
                    if(StringComparer.OrdinalIgnoreCase.Equals(System.IO.Path.GetFullPath(executable),System.IO.Path.GetFullPath(Application.ExecutablePath))&&version==VersionOf(Application.ProductVersion))return true;
                    explanation="Another copy of StealthNotes is already running. Quit it from the tray, then open this version again.\n\nRunning version: "+version+"\nRunning from: "+executable+"\n\nThis version: "+VersionOf(Application.ProductVersion)+"\nThis copy: "+Application.ExecutablePath;
                    return false;
                }
            }
            explanation="StealthNotes is already starting, but its running copy could not be verified. Wait a moment, or Quit it from the tray and try again.";
        }catch(Exception e){LocalLog.Error("Instance check failed",e);explanation="Could not verify the running copy of StealthNotes. Quit all copies from the tray before opening this version.\n\n"+e.Message;}
        return false;
    }
    [DllImport("kernel32.dll",SetLastError=true)]static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int pid);
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool OpenProcessToken(SafeProcessHandle process,uint access,out SafeAccessTokenHandle token);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageNameW(SafeProcessHandle process,uint flags,StringBuilder path,ref int size);
}
