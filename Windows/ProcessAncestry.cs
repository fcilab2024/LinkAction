using System.Diagnostics;
using System.Runtime.InteropServices;
using LinkAction.Core;

namespace LinkAction.Windows;

internal sealed class ProcessAncestry(Action<string> log)
{
    public IReadOnlyList<string> ReadNames()
    {
        var names = new List<string>();
        try
        {
            int pid = Environment.ProcessId;
            for (int depth = 0; depth < LinkPolicy.MaxAncestors; depth++)
            {
                int parentId = GetParentProcessId(pid);
                if (parentId <= 0)
                    break;

                using var parent = Process.GetProcessById(parentId);
                var name = parent.ProcessName;
                log($"Ancestor[{depth}]: {name} (PID={parentId})");
                names.Add(name);
                pid = parentId;
            }
        }
        catch (Exception ex)
        {
            log("Process ancestry error: " + ex.Message);
        }

        return names;
    }

    private static int GetParentProcessId(int pid)
    {
        const uint TH32CS_SNAPPROCESS = 0x00000002;

        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero || snapshot.ToInt64() == -1)
            return 0;

        try
        {
            PROCESSENTRY32 procEntry = new PROCESSENTRY32();
            procEntry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

            if (!Process32First(snapshot, ref procEntry))
                return 0;

            do
            {
                if (procEntry.th32ProcessID == pid)
                    return (int)procEntry.th32ParentProcessID;
            }
            while (Process32Next(snapshot, ref procEntry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll")]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
