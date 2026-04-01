using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

public struct Vector3
{
    public float X, Y, Z;
}

public class ZetaMemory : IDisposable
{
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess, long lpBaseAddress,
        [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    public IntPtr ProcessHandle;
    public long BaseAddress;
    public Process? TargetProcess;

    private const int PROCESS_VM_READ = 0x0010;

    public bool Baglan()
    {
        TargetProcess = Process.GetProcesses().FirstOrDefault(p =>
        {
            try { return p.ProcessName.Contains("GTAProcess", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        });

        if (TargetProcess == null) return false;

        try
        {
            ProcessHandle = OpenProcess(PROCESS_VM_READ, false, TargetProcess.Id);
            if (ProcessHandle == IntPtr.Zero) return false;

            var mainModule = TargetProcess.MainModule;
            if (mainModule == null) return false;

            BaseAddress = mainModule.BaseAddress.ToInt64();
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[!] Erisim hatasi: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    public T Read<T>(long address) where T : struct
    {
        if (address == 0 || address < 0x10000) return default;
        int size = Marshal.SizeOf(typeof(T));
        byte[] buffer = new byte[size];
        bool success = ReadProcessMemory(ProcessHandle, address, buffer, size, out IntPtr bytesRead);
        if (!success || bytesRead.ToInt64() != size) return default;

        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try { return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!; }
        finally { handle.Free(); }
    }

    public void Dispose()
    {
        if (ProcessHandle != IntPtr.Zero)
        {
            CloseHandle(ProcessHandle);
            ProcessHandle = IntPtr.Zero;
        }
    }
}
