using System;
using System.Threading;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Console.Title = "Zeta DEBUG";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== ZETA DEBUG MOD ===\n");
            Console.ResetColor();

            using var mem = new ZetaMemory();

            if (!mem.Baglan())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[X] FiveM bulunamadi!");
                Console.WriteLine("    FiveM acik mi? Sunucuya girdin mi?");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[OK] Process: {mem.TargetProcess!.ProcessName}");
            Console.WriteLine($"[OK] PID: {mem.TargetProcess.Id}");
            Console.WriteLine($"[OK] Base: 0x{mem.BaseAddress:X}\n");
            Console.ResetColor();

            // ── Offset'leri tek tek test et ──
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine("  OFFSET TARAMASI BASLIYOR...");
            Console.WriteLine("════════════════════════════════════════\n");

            // 1. World Pointer
            long worldPtr = mem.Read<long>(mem.BaseAddress + 0x252D738);
            PrintResult("World (0x252D738)", worldPtr);

            if (worldPtr == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[!] World offset yanlis. Alternatifler deneniyor...\n");
                Console.ResetColor();

                long[] worldOffsets = {
                    0x252D738, 0x24E6CD0, 0x24B30D8,
                    0x24ACF28, 0x24C7CD0, 0x24E4CD0,
                    0x2520738, 0x2540738, 0x2560738,
                    0x24D6CD0, 0x24F6CD0, 0x2506CD0
                };

                foreach (long offset in worldOffsets)
                {
                    long test = mem.Read<long>(mem.BaseAddress + offset);
                    string status = test != 0 ? "BULUNDU <<<" : "bos";
                    Console.WriteLine($"  0x{offset:X} => 0x{test:X} [{status}]");
                    if (test != 0 && worldPtr == 0) worldPtr = test;
                }
                Console.WriteLine();
            }

            if (worldPtr == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[X] WORLD POINTER BULUNAMADI");
                Console.WriteLine("[X] Tum offset'ler hatali. Build uyumsuz olabilir.");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

            // 2. Local Player
            long localPlayer = mem.Read<long>(worldPtr + 0x8);
            PrintResult("LocalPlayer (World+0x8)", localPlayer);

            // 3. Replay Interface
            long replayInterface = mem.Read<long>(worldPtr + 0x18);
            PrintResult("ReplayInterface (World+0x18)", replayInterface);

            if (replayInterface == 0)
            {
                // Alternatif offset dene
                long[] replayOffsets = { 0x18, 0x10, 0x20, 0x28 };
                Console.WriteLine("  Alternatif replay offset'ler:");
                foreach (long off in replayOffsets)
                {
                    long test = mem.Read<long>(worldPtr + off);
                    Console.WriteLine($"    World+0x{off:X} => 0x{test:X}");
                    if (test != 0 && replayInterface == 0) replayInterface = test;
                }
            }

            // 4. Ped Interface
            long pedInterface = 0;
            if (replayInterface != 0)
            {
                pedInterface = mem.Read<long>(replayInterface + 0x18);
                PrintResult("PedInterface (Replay+0x18)", pedInterface);

                if (pedInterface == 0)
                {
                    long[] pedOffsets = { 0x10, 0x18, 0x20, 0x28, 0x100 };
                    Console.WriteLine("  Alternatif ped interface offset'ler:");
                    foreach (long off in pedOffsets)
                    {
                        long test = mem.Read<long>(replayInterface + off);
                        Console.WriteLine($"    Replay+0x{off:X} => 0x{test:X}");
                    }
                }
            }

            // 5. Ped List
            if (pedInterface != 0)
            {
                long pedList = mem.Read<long>(pedInterface + 0x100);
                int pedMax = mem.Read<int>(pedInterface + 0x108);
                PrintResult("PedList (PedInt+0x100)", pedList);
                Console.WriteLine($"  PedMax (PedInt+0x108) => {pedMax}");

                if (pedList == 0 || pedMax == 0)
                {
                    Console.WriteLine("  Alternatif ped list offset'ler:");
                    long[] listOffsets = { 0x100, 0x108, 0x110, 0x118, 0x1F0, 0x1F8, 0x200 };
                    foreach (long off in listOffsets)
                    {
                        long testList = mem.Read<long>(pedInterface + off);
                        int testMax = mem.Read<int>(pedInterface + off + 0x8);
                        Console.WriteLine($"    PedInt+0x{off:X} => List:0x{testList:X} Max:{testMax}");
                    }
                }

                // 6. İlk 10 Ped'i Tara
                if (pedList != 0 && pedMax > 0)
                {
                    if (pedMax > 256) pedMax = 256;

                    Console.WriteLine($"\n  === PED TARAMASI (Max: {pedMax}) ===");
                    int found = 0;

                    for (int i = 0; i < pedMax && found < 10; i++)
                    {
                        // Her iki stride'ı da dene
                        long ped8 = mem.Read<long>(pedList + (i * 0x8));
                        long ped10 = mem.Read<long>(pedList + (i * 0x10));

                        long pedPtr = 0;
                        string stride = "";

                        if (ped10 != 0 && ped10 > 0x10000 && ped10 != localPlayer)
                        {
                            pedPtr = ped10;
                            stride = "0x10";
                        }
                        else if (ped8 != 0 && ped8 > 0x10000 && ped8 != localPlayer)
                        {
                            pedPtr = ped8;
                            stride = "0x8";
                        }

                        if (pedPtr == 0) continue;

                        float hp = mem.Read<float>(pedPtr + 0x280);
                        int pedType = mem.Read<int>(pedPtr + 0x10B8);
                        int pedTypeByte = pedType & 0xFF;

                        // Navigation
                        long navPtr = mem.Read<long>(pedPtr + 0x90);
                        Vector3 pos = new Vector3();
                        if (navPtr != 0)
                            pos = mem.Read<Vector3>(navPtr + 0x50);

                        // Skeleton
                        long skelPtr = mem.Read<long>(pedPtr + 0x430);
                        long boneCache = 0;
                        if (skelPtr != 0)
                            boneCache = mem.Read<long>(skelPtr + 0x18);

                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"\n  [{i}] Stride:{stride} Ptr:0x{pedPtr:X}");
                        Console.WriteLine($"      HP: {hp:F1}");
                        Console.WriteLine($"      PedType: {pedType} (byte: {pedTypeByte})");
                        Console.WriteLine($"      Pos: X={pos.X:F1} Y={pos.Y:F1} Z={pos.Z:F1}");
                        Console.WriteLine($"      Nav: 0x{navPtr:X}");
                        Console.WriteLine($"      Skeleton: 0x{skelPtr:X}");
                        Console.WriteLine($"      BoneCache: 0x{boneCache:X}");
                        Console.ResetColor();

                        found++;
                    }

                    if (found == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n  [X] HICBIR PED BULUNAMADI!");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\n  [OK] {found} ped bulundu");
                        Console.ResetColor();
                    }
                }
            }

            // 7. ViewMatrix Testi
            Console.WriteLine("\n════════════════════════════════════════");
            Console.WriteLine("  VIEWMATRIX TESTI");
            Console.WriteLine("════════════════════════════════════════\n");

            long[] vmOffsets = { 0x1FCBD0, 0x2087A40, 0x1F4BD0, 0x1FEBD0, 0x200BD0, 0x1F8BD0 };
            foreach (long vmOff in vmOffsets)
            {
                byte[] buf = new byte[64];
                bool ok = ZetaMemory.ReadProcessMemory(
                    mem.ProcessHandle, mem.BaseAddress + vmOff,
                    buf, 64, out IntPtr br);

                if (ok && br.ToInt64() == 64)
                {
                    float[] mat = new float[16];
                    Buffer.BlockCopy(buf, 0, mat, 0, 64);

                    bool hasValue = false;
                    for (int i = 0; i < 16; i++)
                        if (Math.Abs(mat[i]) > 0.0001f) { hasValue = true; break; }

                    string status = hasValue ? "GECERLI <<<" : "bos/sifir";
                    Console.WriteLine($"  VM 0x{vmOff:X} => [{status}]");
                    if (hasValue)
                    {
                        Console.WriteLine($"    [{mat[0]:F4}, {mat[1]:F4}, {mat[2]:F4}, {mat[3]:F4}]");
                        Console.WriteLine($"    [{mat[4]:F4}, {mat[5]:F4}, {mat[6]:F4}, {mat[7]:F4}]");
                    }
                }
                else
                {
                    Console.WriteLine($"  VM 0x{vmOff:X} => OKUNAMADI");
                }
            }

            // 8. Sonuç
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n════════════════════════════════════════");
            Console.WriteLine("  TARAMA TAMAMLANDI");
            Console.WriteLine("  Bu ciktiyi kopyala ve bana gonder!");
            Console.WriteLine("════════════════════════════════════════");
            Console.ResetColor();

            Console.WriteLine("\nCikmak icin ENTER...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[CRASH] {ex}");
            Console.ResetColor();
            Console.ReadLine();
        }
    }

    static void PrintResult(string name, long value)
    {
        if (value != 0 && value > 0x10000)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [OK] {name} => 0x{value:X}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [X]  {name} => 0x{value:X} (HATALI!)");
        }
        Console.ResetColor();
    }
}
