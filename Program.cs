using System;
using System.Collections.Generic;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Console.Title = "Zeta Offset Scanner";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== ZETA OFFSET SCANNER ===\n");
            Console.ResetColor();

            using var mem = new ZetaMemory();
            if (!mem.Baglan())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[X] FiveM bulunamadi!");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

            long baseAddr = mem.BaseAddress;
            int moduleSize = mem.TargetProcess!.MainModule!.ModuleMemorySize;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[OK] PID: {mem.TargetProcess.Id}");
            Console.WriteLine($"[OK] Base: 0x{baseAddr:X}");
            Console.WriteLine($"[OK] Size: {moduleSize / 1024 / 1024} MB\n");
            Console.ResetColor();

            // ── Tum modulu oku ──
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[*] Bellek okunuyor... (30-60 sn surebilir)");
            Console.ResetColor();

            byte[] data = new byte[moduleSize];
            int totalRead = 0;
            int chunk = 1024 * 1024;

            for (int off = 0; off < moduleSize; off += chunk)
            {
                int sz = Math.Min(chunk, moduleSize - off);
                byte[] buf = new byte[sz];
                if (ZetaMemory.ReadProcessMemory(mem.ProcessHandle, baseAddr + off, buf, sz, out IntPtr br))
                {
                    int r = (int)br.ToInt64();
                    Array.Copy(buf, 0, data, off, r);
                    totalRead += r;
                }
                Console.Write($"\r[*] Okunuyor... {(off + sz) * 100 / moduleSize}%   ");
            }
            Console.WriteLine($"\n[OK] {totalRead / 1024 / 1024} MB okundu\n");

            // ═══════════════════════════════════════
            //  1. WORLD POINTER — Pattern Scan
            // ═══════════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("══════ WORLD POINTER ══════\n");
            Console.ResetColor();

            var worldPatterns = new[]
            {
                ("W1", "48 8B 05 ?? ?? ?? ?? 45 ?? ?? ?? ?? 48 8B 48 08 48 85 C9 74"),
                ("W2", "48 8B 05 ?? ?? ?? ?? 48 8B 48 08 48 85 C9 74"),
                ("W3", "48 8B 05 ?? ?? ?? ?? 33 ED 48 8B 48 08"),
                ("W4", "48 8B 1D ?? ?? ?? ?? 48 8B 5B 08"),
                ("W5", "48 8B 05 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B 48 08"),
                ("W6", "48 8B 05 ?? ?? ?? ?? 48 8D ?? ?? 48 8B 48 08"),
            };

            long worldOffset = 0;
            long worldAddr = 0;

            foreach (var (name, patStr) in worldPatterns)
            {
                byte?[] pat = Parse(patStr);
                var matches = Scan(data, pat);

                foreach (int pos in matches)
                {
                    int disp = BitConverter.ToInt32(data, pos + 3);
                    long resolved = (long)pos + 7 + disp;
                    if (resolved <= 0 || resolved >= moduleSize) continue;

                    long addr = baseAddr + resolved;
                    long ptr = mem.Read<long>(addr);
                    if (ptr == 0 || ptr < 0x10000) continue;

                    long lp = mem.Read<long>(ptr + 0x8);
                    if (lp == 0 || lp < 0x10000) continue;

                    // Birden fazla HP offset dene
                    float hp = 0;
                    long hpOff = 0;
                    foreach (long h in new long[] { 0x280, 0x2C0, 0x284, 0x288 })
                    {
                        float test = mem.Read<float>(lp + h);
                        if (test > 0 && test < 10000) { hp = test; hpOff = h; break; }
                    }

                    if (hp <= 0) continue;

                    // Navigation testi
                    Vector3 lpPos = new Vector3();
                    long navOff = 0;
                    foreach (long n in new long[] { 0x90, 0x30, 0x50 })
                    {
                        long nav = mem.Read<long>(lp + n);
                        if (nav == 0 || nav < 0x10000) continue;
                        Vector3 p = mem.Read<Vector3>(nav + 0x50);
                        if (MathF.Abs(p.X) > 0.1f || MathF.Abs(p.Y) > 0.1f)
                        { lpPos = p; navOff = n; break; }
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  {name} BULUNDU!");
                    Console.WriteLine($"    WORLD_OFFSET = 0x{resolved:X}");
                    Console.WriteLine($"    World:  0x{ptr:X}");
                    Console.WriteLine($"    LP:     0x{lp:X}");
                    Console.WriteLine($"    HP:     {hp:F1} (offset: 0x{hpOff:X})");
                    Console.WriteLine($"    NavOff: 0x{navOff:X}");
                    Console.WriteLine($"    Pos:    ({lpPos.X:F1}, {lpPos.Y:F1}, {lpPos.Z:F1})");
                    Console.ResetColor();

                    if (worldOffset == 0) { worldOffset = resolved; worldAddr = addr; }
                }
            }

            // Brute force fallback
            if (worldOffset == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n  Pattern basarisiz. Brute-force taraniyor...\n");
                Console.ResetColor();

                int start = (int)(moduleSize * 0.5);
                start = (start / 8) * 8;

                for (int off = start; off < moduleSize - 8; off += 8)
                {
                    long val = BitConverter.ToInt64(data, off);
                    if (val < 0x10000 || val > 0x7FFFFFFFFFFF) continue;

                    long lp = mem.Read<long>(val + 0x8);
                    if (lp < 0x10000 || lp > 0x7FFFFFFFFFFF) continue;

                    float hp = mem.Read<float>(lp + 0x280);
                    if (hp <= 0 || hp >= 10000)
                    {
                        hp = mem.Read<float>(lp + 0x2C0);
                        if (hp <= 0 || hp >= 10000) continue;
                    }

                    long nav = mem.Read<long>(lp + 0x90);
                    if (nav < 0x10000) { nav = mem.Read<long>(lp + 0x30); }
                    if (nav < 0x10000) continue;

                    Vector3 p = mem.Read<Vector3>(nav + 0x50);
                    if (MathF.Abs(p.X) < 0.1f && MathF.Abs(p.Y) < 0.1f) continue;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  BRUTE BULUNDU!");
                    Console.WriteLine($"    Offset: 0x{off:X}");
                    Console.WriteLine($"    World: 0x{val:X}  LP: 0x{lp:X}");
                    Console.WriteLine($"    HP: {hp:F1}  Pos: ({p.X:F1}, {p.Y:F1}, {p.Z:F1})");
                    Console.ResetColor();

                    if (worldOffset == 0) { worldOffset = off; worldAddr = baseAddr + off; }
                    break;
                }
            }

            if (worldOffset == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n  [X] WORLD BULUNAMADI! Tum ciktiyi bana gonder.\n");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

            // ═══════════════════════════════════════
            //  2. REPLAY + PED LIST — World uzerinden
            // ═══════════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n══════ REPLAY + PED LIST ══════\n");
            Console.ResetColor();

            long worldPtr2 = mem.Read<long>(worldAddr);
            long localPlayer = mem.Read<long>(worldPtr2 + 0x8);

            long foundReplayOff = 0, foundPedIfOff = 0, foundListOff = 0;

            foreach (long rOff in new long[] { 0x10, 0x18, 0x20, 0x28, 0x30, 0x38 })
            {
                long replay = mem.Read<long>(worldPtr2 + rOff);
                if (replay < 0x10000) continue;

                foreach (long pOff in new long[] { 0x10, 0x18, 0x20, 0x28 })
                {
                    long pedIf = mem.Read<long>(replay + pOff);
                    if (pedIf < 0x10000) continue;

                    foreach (long lOff in new long[] { 0x100, 0x108, 0x110, 0x1F0, 0x1F8, 0x200, 0x240, 0x248 })
                    {
                        long list = mem.Read<long>(pedIf + lOff);
                        int max = mem.Read<int>(pedIf + lOff + 0x8);

                        if (list < 0x10000 || max <= 0 || max > 1000) continue;

                        // Bu listedeki ilk ped'i dogrula
                        bool valid = false;
                        for (int s = 0x8; s <= 0x10; s += 0x8)
                        {
                            for (int i = 0; i < Math.Min(max, 30); i++)
                            {
                                long ped = mem.Read<long>(list + (i * s));
                                if (ped < 0x10000 || ped == localPlayer) continue;
                                float h = mem.Read<float>(ped + 0x280);
                                if (h <= 0 || h > 10000)
                                {
                                    h = mem.Read<float>(ped + 0x2C0);
                                }
                                if (h > 0 && h < 10000)
                                {
                                    valid = true;
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"  BULUNDU!");
                                    Console.WriteLine($"    Replay:  World+0x{rOff:X} => 0x{replay:X}");
                                    Console.WriteLine($"    PedIf:   Replay+0x{pOff:X} => 0x{pedIf:X}");
                                    Console.WriteLine($"    PedList: PedIf+0x{lOff:X} => 0x{list:X}");
                                    Console.WriteLine($"    PedMax:  PedIf+0x{lOff + 0x8:X} => {max}");
                                    Console.WriteLine($"    Stride:  0x{s:X}");
                                    Console.WriteLine($"    Test Ped: 0x{ped:X} HP: {h:F1}");
                                    Console.ResetColor();

                                    if (foundReplayOff == 0)
                                    {
                                        foundReplayOff = rOff;
                                        foundPedIfOff = pOff;
                                        foundListOff = lOff;
                                    }
                                    break;
                                }
                            }
                            if (valid) break;
                        }
                        if (valid) break;
                    }
                    if (foundReplayOff != 0) break;
                }
                if (foundReplayOff != 0) break;
            }

            if (foundReplayOff == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  [X] Ped listesi bulunamadi!\n");
                Console.ResetColor();
            }

            // ═══════════════════════════════════════
            //  3. VIEWMATRIX — Pattern Scan
            // ═══════════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n══════ VIEWMATRIX ══════\n");
            Console.ResetColor();

            var vmPatterns = new[]
            {
                ("VM1", "48 8B 15 ?? ?? ?? ?? 48 8D 2D ?? ?? ?? ?? 48 8B CD"),
                ("VM2", "48 8B 15 ?? ?? ?? ?? 45 33 C0 48 8B CD"),
                ("VM3", "48 8B 3D ?? ?? ?? ?? 48 8B CF E8"),
                ("VM4", "48 8B 0D ?? ?? ?? ?? 48 8D 55 ?? E8"),
                ("VM5", "48 8B 05 ?? ?? ?? ?? 48 8B 48 ?? 48 8D 54 24 ?? E8"),
            };

            long vmPtrOffset = 0;
            long vmStructOffset = 0;

            foreach (var (name, patStr) in vmPatterns)
            {
                byte?[] pat = Parse(patStr);
                var matches = Scan(data, pat);

                foreach (int pos in matches)
                {
                    int disp = BitConverter.ToInt32(data, pos + 3);
                    long resolved = (long)pos + 7 + disp;
                    if (resolved <= 0 || resolved >= moduleSize) continue;

                    long addr = baseAddr + resolved;
                    long ptr = mem.Read<long>(addr);
                    if (ptr < 0x10000) continue;

                    foreach (long mOff in new long[] {
                        0x0, 0x20, 0x60, 0xA0, 0x100, 0x180,
                        0x1E0, 0x200, 0x24C, 0x260, 0x280, 0x2A0,
                        0x300, 0x340, 0x380, 0x3D0, 0x400, 0x450,
                        0x4A0, 0x500, 0x580, 0x600 })
                    {
                        byte[] matBuf = new byte[64];
                        if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, ptr + mOff, matBuf, 64, out IntPtr br2)) continue;
                        if (br2.ToInt64() != 64) continue;

                        float[] mat = new float[16];
                        Buffer.BlockCopy(matBuf, 0, mat, 0, 64);

                        bool hasNaN = false;
                        bool allZero = true;
                        int reasonable = 0;

                        for (int mi = 0; mi < 16; mi++)
                        {
                            if (float.IsNaN(mat[mi]) || float.IsInfinity(mat[mi])) { hasNaN = true; break; }
                            if (MathF.Abs(mat[mi]) > 0.0001f) allZero = false;
                            if (MathF.Abs(mat[mi]) > 0.0001f && MathF.Abs(mat[mi]) < 100f) reasonable++;
                        }

                        if (hasNaN || allZero || reasonable < 8) continue;

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  {name} BULUNDU!");
                        Console.WriteLine($"    Ptr Offset:    0x{resolved:X}");
                        Console.WriteLine($"    Struct Offset: 0x{mOff:X}");
                        Console.WriteLine($"    Ptr: 0x{ptr:X}");
                        Console.WriteLine($"    [{mat[0]:F4}, {mat[1]:F4}, {mat[2]:F4}, {mat[3]:F4}]");
                        Console.WriteLine($"    [{mat[4]:F4}, {mat[5]:F4}, {mat[6]:F4}, {mat[7]:F4}]");
                        Console.WriteLine($"    [{mat[8]:F4}, {mat[9]:F4}, {mat[10]:F4}, {mat[11]:F4}]");
                        Console.WriteLine($"    [{mat[12]:F4}, {mat[13]:F4}, {mat[14]:F4}, {mat[15]:F4}]");
                        Console.ResetColor();

                        if (vmPtrOffset == 0)
                        { vmPtrOffset = resolved; vmStructOffset = mOff; }
                    }
                }
            }

            if (vmPtrOffset == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  [X] ViewMatrix bulunamadi!\n");
                Console.ResetColor();
            }

            // ═══════════════════════════════════════
            //  SONUC
            // ═══════════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("        BULUNAN OFFSET'LER");
            Console.WriteLine("═══════════════════════════════════════\n");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  WORLD_OFFSET           = 0x{worldOffset:X}");
            Console.WriteLine($"  REPLAY (World+)        = 0x{foundReplayOff:X}");
            Console.WriteLine($"  PED_INTERFACE (Replay+) = 0x{foundPedIfOff:X}");
            Console.WriteLine($"  PED_LIST (PedIf+)      = 0x{foundListOff:X}");
            Console.WriteLine($"  VIEWMATRIX_PTR         = 0x{vmPtrOffset:X}");
            Console.WriteLine($"  VIEWMATRIX_STRUCT      = 0x{vmStructOffset:X}");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  >>> BU CIKTIYI KOPYALA VE BANA GONDER! <<<");
            Console.ResetColor();
            Console.WriteLine("\nENTER'a bas...");
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

    static byte?[] Parse(string p)
    {
        string[] s = p.Split(' ');
        byte?[] r = new byte?[s.Length];
        for (int i = 0; i < s.Length; i++)
            r[i] = s[i] == "??" ? null : Convert.ToByte(s[i], 16);
        return r;
    }

    static List<int> Scan(byte[] data, byte?[] pat)
    {
        var r = new List<int>();
        int lim = data.Length - pat.Length;
        for (int i = 0; i <= lim; i++)
        {
            bool ok = true;
            for (int j = 0; j < pat.Length; j++)
            {
                if (pat[j].HasValue && data[i + j] != pat[j].Value)
                { ok = false; break; }
            }
            if (ok) r.Add(i);
        }
        return r;
    }
}
