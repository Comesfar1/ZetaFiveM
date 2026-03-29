using System;
using System.Threading;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Console.Title = "Zeta | b3095";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
 ███████╗███████╗████████╗ █████╗  
 ╚══███╔╝██╔════╝╚══██╔══╝██╔══██╗ 
   ███╔╝ █████╗     ██║   ███████║ 
  ███╔╝  ██╔══╝     ██║   ██╔══██║ 
 ███████╗███████╗   ██║   ██║  ██║ 
 ╚══════╝╚══════╝   ╚═╝   ╚═╝  ╚═╝");
            Console.ResetColor();
            Console.WriteLine("     [ FiveM b3095 | External ESP ]\n");

            int selectedKey = 0x74;
            Console.WriteLine("[?] ESP Ac/Kapat tusu:");
            Console.WriteLine("  1. F5  (Varsayilan)");
            Console.WriteLine("  2. INSERT");
            Console.WriteLine("  3. NUMPAD 0");
            Console.Write("\nSeciminiz (1/2/3): ");

            string? secim = Console.ReadLine()?.Trim();
            selectedKey = secim switch { "2" => 0x2D, "3" => 0x60, _ => 0x74 };
            string keyName = selectedKey switch { 0x2D => "INSERT", 0x60 => "NUMPAD 0", _ => "F5" };
            Console.WriteLine($"[+] Tus: {keyName}\n");

            Console.WriteLine("[*] FiveM araniyor...");
            using var mem = new ZetaMemory();

            if (!mem.Baglan())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[X] FiveM bulunamadi!");
                Console.WriteLine("  1. FiveM acik mi?");
                Console.WriteLine("  2. Sunucuya girdiniz mi?");
                Console.WriteLine("  3. Yonetici olarak calistirin!");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

            if (mem.TargetProcess!.MainWindowHandle == IntPtr.Zero)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[!] Pencere hazir degil. Oyun acilinca tekrar deneyin.");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[+] PID: {mem.TargetProcess.Id}");
            Console.WriteLine($"[+] Base: 0x{mem.BaseAddress:X}");
            Console.ResetColor();

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

            using (var overlay = new ZetaSkeletonOverlay(mem))
            {
                overlay.ToggleKey = selectedKey;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n[+] ZETA ESP AKTIF | Tus: {keyName} | Ctrl+C = Kapat\n");
                Console.ResetColor();

                var overlayThread = new Thread(() => overlay.Run())
                {
                    IsBackground = true,
                    Name = "ZetaOverlay"
                };
                overlayThread.SetApartmentState(ApartmentState.STA);
                overlayThread.Start();

                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        Thread.Sleep(500);
                        bool exited = false;
                        try { exited = mem.TargetProcess.HasExited; }
                        catch { exited = true; }
                        if (exited)
                        {
                            Console.WriteLine("[!] FiveM kapandi.");
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) { }

                if (overlayThread.IsAlive)
                    overlayThread.Join(3000);
            }

            Console.WriteLine("[+] Zeta kapatildi.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[CRASH] {ex}");
            Console.ResetColor();
            Console.ReadLine();
        }
    }
}
