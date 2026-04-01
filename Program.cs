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
            Console.WriteLine("     [ FiveM | External ESP ]\n");

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
                Console.WriteLine("[X] FiveM bulunamadi!");
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

            // Verify offsets
            long worldPtr = mem.Read<long>(mem.BaseAddress + 0x25B14B0);
            long lp = worldPtr > 0 ? mem.Read<long>(worldPtr + 0x8) : 0;
            float hp = lp > 0 ? mem.Read<float>(lp + 0x280) : 0;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[+] PID: {mem.TargetProcess.Id}");
            Console.WriteLine($"[+] Base: 0x{mem.BaseAddress:X}");
            Console.WriteLine($"[+] World: 0x{worldPtr:X}");
            Console.WriteLine($"[+] LP: 0x{lp:X}  HP: {hp:F1}");
            Console.ResetColor();

            if (worldPtr == 0 || lp == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[X] Offset dogrulanamadi. Sunucuya baglandiginizdan emin olun.");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

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
