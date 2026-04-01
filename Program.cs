using System;
using System.Threading;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Console.Title = "Zeta LIVE DEBUG";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== ZETA LIVE DEBUG ===\n");
            Console.ResetColor();

            using var mem = new ZetaMemory();
            if (!mem.Baglan()) { Console.ReadLine(); return; }

            long BA = mem.BaseAddress;

            while (true)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=== ZETA LIVE DEBUG (her 2 sn yenilenir) ===\n");
                Console.ResetColor();

                long worldPtr = mem.Read<long>(BA + 0x25B14B0);
                long LP = mem.Read<long>(worldPtr + 0x8);

                // LP pozisyon
                Vector3 lpPos = new Vector3();
                foreach (long navOff in new long[] { 0x30, 0x1B8, 0x1A8 })
                {
                    long nav = mem.Read<long>(LP + navOff);
                    if (nav < 0x10000) continue;
                    Vector3 p = mem.Read<Vector3>(nav + 0x60);
                    if (!float.IsNaN(p.X) && MathF.Abs(p.X) > 10)
                    { lpPos = p; break; }
                }

                float lpHp = mem.Read<float>(LP + 0x280);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"LP: 0x{LP:X}  HP:{lpHp:F0}  Pos:({lpPos.X:F0},{lpPos.Y:F0},{lpPos.Z:F0})");
                Console.ResetColor();

                // Ped list chain
                long c1 = mem.Read<long>(worldPtr + 0x10);
                long c2 = c1 > 0x10000 ? mem.Read<long>(c1 + 0x58) : 0;
                long c3 = c2 > 0x10000 ? mem.Read<long>(c2 + 0x18) : 0;
                long pedList = c3 > 0x10000 ? mem.Read<long>(c3 + 0x8) : 0;
                int pedMax = c3 > 0x10000 ? mem.Read<int>(c3 + 0x10) : 0;

                Console.WriteLine($"Chain: c1=0x{c1:X} c2=0x{c2:X} c3=0x{c3:X}");
                Console.WriteLine($"List=0x{pedList:X} Max={pedMax}\n");

                if (pedList < 0x10000 || pedMax <= 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[X] PED LIST BOS!");
                    Console.ResetColor();
                    Thread.Sleep(2000);
                    continue;
                }

                // ViewMatrix test
                long vmPtr = mem.Read<long>(BA + 0x201ED50);
                Console.Write("VM: ");
                if (vmPtr > 0x10000)
                {
                    byte[] vmBuf = new byte[64];
                    ZetaMemory.ReadProcessMemory(mem.ProcessHandle, vmPtr + 0x24, vmBuf, 64, out _);
                    float[] vm = new float[16];
                    Buffer.BlockCopy(vmBuf, 0, vm, 0, 64);

                    bool vmOk = true;
                    for (int mi = 0; mi < 16; mi++)
                        if (float.IsNaN(vm[mi]) || float.IsInfinity(vm[mi])) { vmOk = false; break; }

                    if (vmOk)
                    {
                        float w = vm[3] * lpPos.X + vm[7] * lpPos.Y + vm[11] * lpPos.Z + vm[15];
                        float inv = MathF.Abs(w) > 0.01f ? 1f / w : 0;
                        float nx = (vm[0] * lpPos.X + vm[4] * lpPos.Y + vm[8] * lpPos.Z + vm[12]) * inv;
                        float ny = (vm[1] * lpPos.X + vm[5] * lpPos.Y + vm[9] * lpPos.Z + vm[13]) * inv;

                        Console.ForegroundColor = w > 0 ? ConsoleColor.Green : ConsoleColor.Red;
                        Console.WriteLine($"W={w:F2} NDC({nx:F3},{ny:F3}) {(w > 0 ? "ONUNDE" : "ARKASINDA")}");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("NaN/Inf — VM HATALI!");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("PTR=0 — VM BULUNAMADI!");
                }
                Console.ResetColor();

                // Ped scan
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n── PED SCAN ({pedMax} slot) ──\n");
                Console.ResetColor();

                int validPeds = 0;
                int withPos = 0;
                int withSkel = 0;
                int onScreen = 0;

                if (pedMax > 200) pedMax = 200;

                for (int i = 0; i < pedMax; i++)
                {
                    long ped = mem.Read<long>(pedList + (i * 0x10));
                    if (ped < 0x10000 || ped > 0x7FFFFFFFFFFF || ped == LP) continue;

                    // Filter pool metadata
                    if (Math.Abs(ped - pedList) < 0x10000) continue;

                    validPeds++;

                    // Position
                    Vector3 pos = new Vector3();
                    string posSource = "yok";

                    foreach (long navOff in new long[] { 0x30, 0x1B8, 0x1A8 })
                    {
                        long nav = mem.Read<long>(ped + navOff);
                        if (nav < 0x10000) continue;
                        Vector3 p = mem.Read<Vector3>(nav + 0x60);
                        if (!float.IsNaN(p.X) && MathF.Abs(p.X) > 10)
                        { pos = p; posSource = $"Nav+0x{navOff:X}->0x60"; break; }
                    }

                    if (pos.X == 0)
                    {
                        foreach (long dOff in new long[] { 0x90, 0x5C0, 0x640, 0x680 })
                        {
                            float x = mem.Read<float>(ped + dOff);
                            float y = mem.Read<float>(ped + dOff + 4);
                            if (!float.IsNaN(x) && MathF.Abs(x) > 10 && MathF.Abs(x) < 10000
                                && !float.IsNaN(y) && MathF.Abs(y) > 10)
                            {
                                float z = mem.Read<float>(ped + dOff + 8);
                                pos = new Vector3 { X = x, Y = y, Z = z };
                                posSource = $"Direct+0x{dOff:X}";
                                break;
                            }
                        }
                    }

                    if (pos.X != 0) withPos++;

                    float dist = 0;
                    if (pos.X != 0)
                    {
                        float dx = pos.X - lpPos.X;
                        float dy = pos.Y - lpPos.Y;
                        float dz = pos.Z - lpPos.Z;
                        dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                    }

                    // Skeleton
                    long skelPtr = mem.Read<long>(ped + 0x1B0);
                    long boneCache = 0;
                    bool hasSkel = false;
                    if (skelPtr > 0x10000 && skelPtr < 0x7FFFFFFFFFFF)
                    {
                        boneCache = mem.Read<long>(skelPtr + 0x28);
                        if (boneCache > 0x10000 && boneCache < 0x7FFFFFFFFFFF)
                        {
                            Vector3 b0 = mem.Read<Vector3>(boneCache);
                            if (!float.IsNaN(b0.X) && MathF.Abs(b0.X) < 5) hasSkel = true;
                        }
                    }
                    if (hasSkel) withSkel++;

                    // W2S test
                    string w2sResult = "pos_yok";
                    if (pos.X != 0 && vmPtr > 0x10000)
                    {
                        byte[] vmBuf = new byte[64];
                        ZetaMemory.ReadProcessMemory(mem.ProcessHandle, vmPtr + 0x24, vmBuf, 64, out _);
                        float[] vm = new float[16];
                        Buffer.BlockCopy(vmBuf, 0, vm, 0, 64);

                        float w = vm[3] * pos.X + vm[7] * pos.Y + vm[11] * pos.Z + vm[15];
                        if (MathF.Abs(w) > 0.01f)
                        {
                            float inv = 1f / w;
                            float nx = (vm[0] * pos.X + vm[4] * pos.Y + vm[8] * pos.Z + vm[12]) * inv;
                            float ny = (vm[1] * pos.X + vm[5] * pos.Y + vm[9] * pos.Z + vm[13]) * inv;
                            float sx = 960 + nx * 960;
                            float sy = 540 - ny * 540;

                            bool inScreen = sx > 0 && sx < 1920 && sy > 0 && sy < 1080;
                            w2sResult = inScreen ? $"EKRANDA({sx:F0},{sy:F0})" : $"dis({sx:F0},{sy:F0})";
                            if (inScreen) onScreen++;
                        }
                        else w2sResult = "W=0";
                    }

                    // Sadece yakin olan ilk 10 pedi goster
                    if (validPeds <= 10)
                    {
                        Console.ForegroundColor = pos.X != 0 ? ConsoleColor.White : ConsoleColor.DarkGray;
                        Console.Write($"  [{i,3}] 0x{ped:X}");
                        Console.Write($"  {posSource}");
                        if (pos.X != 0)
                            Console.Write($"  ({pos.X:F0},{pos.Y:F0},{pos.Z:F0}) {dist:F0}m");
                        Console.Write($"  Skel:{(hasSkel ? "OK" : "-")}");
                        Console.Write($"  W2S:{w2sResult}");
                        Console.WriteLine();
                        Console.ResetColor();
                    }
                }

                // Ozet
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n  ─── OZET ───");
                Console.WriteLine($"  Toplam slot:   {pedMax}");
                Console.WriteLine($"  Valid pointer: {validPeds}");
                Console.WriteLine($"  Pozisyonu var: {withPos}");
                Console.WriteLine($"  Skeleton var:  {withSkel}");
                Console.WriteLine($"  Ekranda:       {onScreen}");
                Console.ResetColor();

                if (withPos == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n  [!] HICBIR PED'DE POZISYON YOK!");
                    Console.WriteLine("  [!] Ped listesi pool metadata olabilir.");
                    Console.ResetColor();
                }

                if (onScreen == 0 && withPos > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n  [!] POZISYON VAR AMA EKRANDA YOK!");
                    Console.WriteLine("  [!] ViewMatrix yanlis olabilir.");
                    Console.ResetColor();
                }

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("\n  [ENTER = yenile | Ctrl+C = cik]");
                Console.ResetColor();

                // 3 saniye bekle veya ENTER
                var task = Task.Run(() => Console.ReadLine());
                task.Wait(TimeSpan.FromSeconds(3));
            }
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
