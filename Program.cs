using System;
using System.Collections.Generic;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Console.Title = "Zeta Phase 2";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== ZETA PHASE 2 SCANNER ===\n");
            Console.ResetColor();

            using var mem = new ZetaMemory();
            if (!mem.Baglan()) { Console.WriteLine("[X] FiveM bulunamadi!"); Console.ReadLine(); return; }

            long BA = mem.BaseAddress;

            // Phase 1 sonuclari
            long worldPtr = mem.Read<long>(BA + 0x25B14B0);
            long LP = mem.Read<long>(worldPtr + 0x8);
            float lpHp = mem.Read<float>(LP + 0x280);
            long lpNav = mem.Read<long>(LP + 0x30);
            Vector3 lpPos = mem.Read<Vector3>(lpNav + 0x50);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[OK] World: 0x{worldPtr:X}");
            Console.WriteLine($"[OK] LP: 0x{LP:X}  HP: {lpHp:F1}");
            Console.WriteLine($"[OK] Pos: ({lpPos.X:F1}, {lpPos.Y:F1}, {lpPos.Z:F1})\n");
            Console.ResetColor();

            // ═══════════════════════════════════
            //  1. WORLD STRUCTURE DUMP
            // ═══════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("══════ WORLD STRUCTURE ══════\n");
            Console.ResetColor();

            byte[] wd = BulkRead(mem, worldPtr, 0x800);
            var wp = GetPointers(wd);
            foreach (var (o, v) in wp)
            {
                string l = o == 8 ? " <- LP" : "";
                Console.WriteLine($"  +0x{o:X3} => 0x{v:X}{l}");
            }

            // ═══════════════════════════════════
            //  2. PED LIST DEEP SEARCH
            // ═══════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n══════ PED LIST SEARCH (1-3 dk) ══════\n");
            Console.ResetColor();

            long fList = 0; int fMax = 0, fStride = 0; string fPath = "";
            int prog = 0;

            foreach (var (wOff, wVal) in wp)
            {
                if (wOff == 8 || fList != 0) continue;
                prog++;
                Console.Write($"\r  [{prog}/{wp.Count}] World+0x{wOff:X3}...          ");

                byte[] d1 = BulkRead(mem, wVal, 0x500);

                // Level 1: wVal icerisinde list ara
                TryFindList(mem, d1, wVal, LP,
                    $"W+0x{wOff:X}", ref fList, ref fMax, ref fStride, ref fPath);
                if (fList != 0) break;

                // Level 2: wVal'in alt pointer'lari
                var p1 = GetPointers(d1);
                int sc = 0;
                foreach (var (s1Off, s1Val) in p1)
                {
                    if (fList != 0 || sc++ > 50) break;

                    byte[] d2 = BulkRead(mem, s1Val, 0x500);
                    TryFindList(mem, d2, s1Val, LP,
                        $"W+0x{wOff:X}->+0x{s1Off:X}", ref fList, ref fMax, ref fStride, ref fPath);
                    if (fList != 0) break;

                    // Level 3: bir kademe daha
                    var p2 = GetPointers(d2);
                    int sc2 = 0;
                    foreach (var (s2Off, s2Val) in p2)
                    {
                        if (fList != 0 || sc2++ > 20) break;
                        byte[] d3 = BulkRead(mem, s2Val, 0x500);
                        TryFindList(mem, d3, s2Val, LP,
                            $"W+0x{wOff:X}->+0x{s1Off:X}->+0x{s2Off:X}", ref fList, ref fMax, ref fStride, ref fPath);
                    }
                }
            }

            Console.WriteLine();

            if (fList != 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  PED LIST BULUNDU!");
                Console.WriteLine($"    Path:   {fPath}");
                Console.WriteLine($"    List:   0x{fList:X}");
                Console.WriteLine($"    Max:    {fMax}");
                Console.WriteLine($"    Stride: 0x{fStride:X}");
                Console.ResetColor();

                // Ped detaylari
                Console.WriteLine("\n  -- Bulunan Pedler --\n");
                int shown = 0;
                for (int i = 0; i < Math.Min(fMax, 200) && shown < 10; i++)
                {
                    long ped = mem.Read<long>(fList + (i * fStride));
                    if (ped < 0x10000 || ped == LP) continue;
                    float hp = mem.Read<float>(ped + 0x280);
                    if (hp <= 0 || hp > 10000) continue;

                    long nav = mem.Read<long>(ped + 0x30);
                    Vector3 pp = nav > 0x10000 ? mem.Read<Vector3>(nav + 0x50) : new Vector3();

                    // PedType aday offset'leri tara
                    string ptInfo = "";
                    foreach (long ptOff in new long[] { 0x10B8, 0x10A8, 0x10C8, 0x10D8, 0x1088, 0x1098 })
                    {
                        int pt = mem.Read<int>(ped + ptOff);
                        ptInfo += $" 0x{ptOff:X}={pt}({pt & 0xFF})";
                    }

                    // Skeleton tara
                    string skelInfo = "yok";
                    foreach (long sOff in new long[] { 0x430, 0x410, 0x420, 0x440, 0x450,
                        0x460, 0x380, 0x390, 0x3A0, 0x3B0, 0x3C0, 0x3D0, 0x3E0, 0x3F0, 0x400, 0x470, 0x480 })
                    {
                        long sk = mem.Read<long>(ped + sOff);
                        if (sk < 0x10000 || sk > 0x7FFFFFFFFFFF) continue;
                        for (long bcOff = 0; bcOff <= 0x30; bcOff += 8)
                        {
                            long bc = mem.Read<long>(sk + bcOff);
                            if (bc < 0x10000 || bc > 0x7FFFFFFFFFFF) continue;
                            Vector3 b0 = mem.Read<Vector3>(bc + 0x10);
                            if (MathF.Abs(b0.X) < 3 && MathF.Abs(b0.Y) < 3 && MathF.Abs(b0.Z) < 3
                                && (MathF.Abs(b0.X) > 0.001f || MathF.Abs(b0.Y) > 0.001f || MathF.Abs(b0.Z) > 0.001f))
                            {
                                skelInfo = $"Ped+0x{sOff:X}->+0x{bcOff:X} B0:({b0.X:F3},{b0.Y:F3},{b0.Z:F3})";
                                goto skelDone;
                            }
                        }
                    }
                    skelDone:

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"  [{i}] 0x{ped:X}");
                    Console.WriteLine($"      HP: {hp:F1}  Pos: ({pp.X:F1},{pp.Y:F1},{pp.Z:F1})");
                    Console.WriteLine($"      PedType:{ptInfo}");
                    Console.WriteLine($"      Skeleton: {skelInfo}");
                    Console.ResetColor();
                    Console.WriteLine();
                    shown++;
                }

                if (shown == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  Yakininda HP'si olan ped yok! Birinin yanina git.");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n  [X] PED LIST BULUNAMADI (World uzerinden)");
                Console.ResetColor();

                // Fallback: LP uzerinden ped type ve skeleton tara
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n  LP uzerinden skeleton taraniyor...\n");
                Console.ResetColor();
            }

            // ═══════════════════════════════════
            //  3. LP SKELETON SCAN
            // ═══════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n══════ LP SKELETON SCAN ══════\n");
            Console.ResetColor();

            bool skelFound = false;
            for (long o = 0x100; o <= 0x800 && !skelFound; o += 8)
            {
                long sk = mem.Read<long>(LP + o);
                if (sk < 0x10000 || sk > 0x7FFFFFFFFFFF) continue;

                for (long bc = 0; bc <= 0x30 && !skelFound; bc += 8)
                {
                    long b = mem.Read<long>(sk + bc);
                    if (b < 0x10000 || b > 0x7FFFFFFFFFFF) continue;

                    for (int st = 0x10; st <= 0x40 && !skelFound; st += 0x10)
                    {
                        Vector3 b0 = mem.Read<Vector3>(b + 0x10);
                        Vector3 b5 = mem.Read<Vector3>(b + (5 * st) + 0x10);
                        Vector3 b8 = mem.Read<Vector3>(b + (8 * st) + 0x10);

                        bool v0 = MathF.Abs(b0.X) < 3 && MathF.Abs(b0.Y) < 3 && MathF.Abs(b0.Z) < 3;
                        bool v5 = MathF.Abs(b5.X) < 3 && MathF.Abs(b5.Y) < 3 && MathF.Abs(b5.Z) < 3;
                        bool v8 = MathF.Abs(b8.X) < 3 && MathF.Abs(b8.Y) < 3 && MathF.Abs(b8.Z) < 3;
                        bool nz = MathF.Abs(b0.X) > 0.001f || MathF.Abs(b0.Y) > 0.001f || MathF.Abs(b0.Z) > 0.001f;

                        if (v0 && v5 && v8 && nz)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"  SKELETON BULUNDU!");
                            Console.WriteLine($"    Offset: LP+0x{o:X}");
                            Console.WriteLine($"    BoneCache: Skel+0x{bc:X}");
                            Console.WriteLine($"    Stride: 0x{st:X}");
                            Console.WriteLine($"    B0: ({b0.X:F3}, {b0.Y:F3}, {b0.Z:F3})");
                            Console.WriteLine($"    B5: ({b5.X:F3}, {b5.Y:F3}, {b5.Z:F3})");
                            Console.WriteLine($"    B8: ({b8.X:F3}, {b8.Y:F3}, {b8.Z:F3})");
                            Console.ResetColor();
                            skelFound = true;
                        }
                    }
                }
            }

            if (!skelFound)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  [X] Skeleton bulunamadi");
                Console.ResetColor();
            }

            // ═══════════════════════════════════
            //  4. LP PEDTYPE SCAN
            // ═══════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n══════ LP PEDTYPE SCAN ══════\n");
            Console.ResetColor();

            Console.WriteLine("  LP PedType adaylari (deger=2 olan player):");
            for (long o = 0x1000; o <= 0x1200; o += 4)
            {
                int val = mem.Read<int>(LP + o);
                if ((val & 0xFF) == 2 && val < 256)
                {
                    Console.WriteLine($"    LP+0x{o:X} = {val} (byte: {val & 0xFF})");
                }
            }

            // ═══════════════════════════════════
            //  OZET
            // ═══════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═════════════════════════════════════");
            Console.WriteLine("  PHASE 2 TAMAMLANDI!");
            Console.WriteLine("  TUM CIKTIYI KOPYALA → BANA GONDER!");
            Console.WriteLine("═════════════════════════════════════\n");
            Console.ResetColor();
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

    static byte[] BulkRead(ZetaMemory mem, long addr, int size)
    {
        byte[] buf = new byte[size];
        ZetaMemory.ReadProcessMemory(mem.ProcessHandle, addr, buf, size, out _);
        return buf;
    }

    static List<(int off, long val)> GetPointers(byte[] data)
    {
        var r = new List<(int, long)>();
        for (int i = 0; i <= data.Length - 8; i += 8)
        {
            long v = BitConverter.ToInt64(data, i);
            if (v > 0x10000 && v < 0x7FFFFFFFFFFF)
                r.Add((i, v));
        }
        return r;
    }

    static void TryFindList(ZetaMemory mem, byte[] data, long structAddr, long LP, string path,
        ref long fList, ref int fMax, ref int fStride, ref string fPath)
    {
        if (fList != 0) return;

        for (int j = 0; j <= data.Length - 16; j += 8)
        {
            if (fList != 0) return;

            long ptr = BitConverter.ToInt64(data, j);
            if (ptr < 0x10000 || ptr > 0x7FFFFFFFFFFF) continue;

            int cnt = BitConverter.ToInt32(data, j + 8);
            if (cnt <= 0 || cnt > 1000) continue;

            // Stride 0x10 ve 0x8 dene
            for (int stride = 0x10; stride >= 0x8; stride -= 0x8)
            {
                int valid = 0;
                for (int k = 0; k < Math.Min(cnt, 10); k++)
                {
                    long ped = mem.Read<long>(ptr + (k * stride));
                    if (ped < 0x10000 || ped == LP || ped > 0x7FFFFFFFFFFF) continue;

                    float hp = mem.Read<float>(ped + 0x280);
                    if (hp <= 0 || hp > 10000) continue;

                    long nav = mem.Read<long>(ped + 0x30);
                    if (nav < 0x10000) continue;

                    Vector3 p = mem.Read<Vector3>(nav + 0x50);
                    if (MathF.Abs(p.X) > 0.1f || MathF.Abs(p.Y) > 0.1f)
                        valid++;
                }

                if (valid >= 1)
                {
                    fPath = $"{path}->+0x{j:X}";
                    fList = ptr;
                    fMax = cnt;
                    fStride = stride;
                    return;
                }
            }
        }
    }
}
