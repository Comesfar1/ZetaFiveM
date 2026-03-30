using System;
using System.Collections.Generic;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Console.Title = "Zeta Phase 4";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== ZETA PHASE 4 ===\n");
            Console.ResetColor();

            using var mem = new ZetaMemory();
            if (!mem.Baglan()) { Console.ReadLine(); return; }

            long BA = mem.BaseAddress;
            long worldPtr = mem.Read<long>(BA + 0x25B14B0);
            long LP = mem.Read<long>(worldPtr + 0x8);
            float lpHp = mem.Read<float>(LP + 0x280);
            long lpNav = mem.Read<long>(LP + 0x30);
            Vector3 lpPos = lpNav > 0x10000 ? mem.Read<Vector3>(lpNav + 0x50) : new Vector3();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"LP:0x{LP:X} HP:{lpHp:F1} Pos:({lpPos.X:F1},{lpPos.Y:F1},{lpPos.Z:F1})\n");
            Console.ResetColor();

            // ═══════════════════════════════
            // A) LP SKELETON — BRUTE FORCE
            // ═══════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("══ A: LP SKELETON SCAN (0x000-0x2000) ══\n");
            Console.ResetColor();

            byte[] lpData = new byte[0x2000];
            ZetaMemory.ReadProcessMemory(mem.ProcessHandle, LP, lpData, lpData.Length, out _);

            bool skelF = false;

            for (int i = 0; i < lpData.Length - 8 && !skelF; i += 8)
            {
                long p1 = BitConverter.ToInt64(lpData, i);
                if (p1 < 0x10000 || p1 > 0x7FFFFFFFFFFF) continue;

                // Level 1: p1 direct
                if (TestBones(mem, p1, out int s1, out int po1))
                { Report(mem, $"LP+0x{i:X} (direct)", p1, s1, po1); skelF = true; break; }

                // Level 2: p1 sub-pointers
                for (int j = 0; j <= 0x48 && !skelF; j += 8)
                {
                    long p2 = mem.Read<long>(p1 + j);
                    if (p2 < 0x10000 || p2 > 0x7FFFFFFFFFFF) continue;

                    if (TestBones(mem, p2, out int s2, out int po2))
                    { Report(mem, $"LP+0x{i:X}->+0x{j:X}", p2, s2, po2); skelF = true; break; }

                    // Level 3
                    for (int k = 0; k <= 0x28 && !skelF; k += 8)
                    {
                        long p3 = mem.Read<long>(p2 + k);
                        if (p3 < 0x10000 || p3 > 0x7FFFFFFFFFFF) continue;
                        if (TestBones(mem, p3, out int s3, out int po3))
                        { Report(mem, $"LP+0x{i:X}->+0x{j:X}->+0x{k:X}", p3, s3, po3); skelF = true; break; }
                    }
                }
            }

            if (!skelF)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Local coords bulunamadi. World-coords deneniyor...\n");
                Console.ResetColor();

                for (int i = 0; i < lpData.Length - 8 && !skelF; i += 8)
                {
                    long p1 = BitConverter.ToInt64(lpData, i);
                    if (p1 < 0x10000 || p1 > 0x7FFFFFFFFFFF) continue;

                    if (TestBonesWorld(mem, p1, lpPos, out int sw, out int pow))
                    { ReportW(mem, $"LP+0x{i:X} (direct world)", p1, sw, pow); skelF = true; break; }

                    for (int j = 0; j <= 0x48 && !skelF; j += 8)
                    {
                        long p2 = mem.Read<long>(p1 + j);
                        if (p2 < 0x10000 || p2 > 0x7FFFFFFFFFFF) continue;

                        if (TestBonesWorld(mem, p2, lpPos, out int sw2, out int pow2))
                        { ReportW(mem, $"LP+0x{i:X}->+0x{j:X} (world)", p2, sw2, pow2); skelF = true; break; }
                    }
                }
            }

            if (!skelF)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  [X] SKELETON BULUNAMADI\n");
                Console.ResetColor();

                Console.WriteLine("  LP pointer haritasi (0x000-0x800):\n");
                for (int xx = 0; xx < 0x800; xx += 8)
                {
                    long vv = BitConverter.ToInt64(lpData, xx);
                    if (vv > 0x10000 && vv < 0x7FFFFFFFFFFF)
                    {
                        // Her pointer icin kisa bone testi yap
                        string tag = "";
                        byte[] test = new byte[256];
                        if (ZetaMemory.ReadProcessMemory(mem.ProcessHandle, vv, test, 256, out IntPtr tbr) && tbr.ToInt64() >= 48)
                        {
                            // Check for small float sequences
                            int sf2 = 0;
                            for (int fi = 0; fi < 48; fi += 4)
                            {
                                float fv = BitConverter.ToSingle(test, fi);
                                if (!float.IsNaN(fv) && MathF.Abs(fv) > 0.001f && MathF.Abs(fv) < 3f) sf2++;
                            }
                            if (sf2 >= 4) tag = $" [small_floats:{sf2}]";

                            // Check for LP-pos-like floats
                            int wf = 0;
                            for (int fi = 0; fi < 48; fi += 4)
                            {
                                float fv = BitConverter.ToSingle(test, fi);
                                if (!float.IsNaN(fv) && MathF.Abs(fv - lpPos.X) < 5) wf++;
                                if (!float.IsNaN(fv) && MathF.Abs(fv - lpPos.Y) < 5) wf++;
                            }
                            if (wf >= 2) tag += $" [world_near:{wf}]";
                        }
                        Console.WriteLine($"    +0x{xx:X3} => 0x{vv:X}{tag}");
                    }
                }
            }

            // ═══════════════════════════════
            // B) REAL PED LIST — LP REVERSE LOOKUP
            // ═══════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n══ B: LP REVERSE LOOKUP ══\n");
            Console.ResetColor();

            byte[] worldData = new byte[0x800];
            ZetaMemory.ReadProcessMemory(mem.ProcessHandle, worldPtr, worldData, worldData.Length, out _);

            bool lpFound = false;

            for (int wo = 0; wo < 0x800 - 8 && !lpFound; wo += 8)
            {
                long ws = BitConverter.ToInt64(worldData, wo);
                if (ws < 0x10000 || ws > 0x7FFFFFFFFFFF) continue;

                // Level 1: search this structure for LP
                if (SearchForLP(mem, ws, LP, $"W+0x{wo:X}", ref lpFound)) continue;

                // Level 2: sub-pointers
                byte[] d1 = new byte[0x400];
                if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, ws, d1, d1.Length, out IntPtr br1)) continue;
                int sc1 = 0;
                for (int s1 = 0; s1 < Math.Min((int)br1.ToInt64(), 0x200) - 8 && !lpFound && sc1 < 30; s1 += 8)
                {
                    long sv1 = BitConverter.ToInt64(d1, s1);
                    if (sv1 < 0x10000 || sv1 > 0x7FFFFFFFFFFF || sv1 == ws) continue;
                    sc1++;

                    if (SearchForLP(mem, sv1, LP, $"W+0x{wo:X}->+0x{s1:X}", ref lpFound)) break;

                    // Level 3
                    byte[] d2 = new byte[0x400];
                    if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, sv1, d2, d2.Length, out IntPtr br2)) continue;
                    int sc2 = 0;
                    for (int s2 = 0; s2 < Math.Min((int)br2.ToInt64(), 0x200) - 8 && !lpFound && sc2 < 15; s2 += 8)
                    {
                        long sv2 = BitConverter.ToInt64(d2, s2);
                        if (sv2 < 0x10000 || sv2 > 0x7FFFFFFFFFFF || sv2 == sv1) continue;
                        sc2++;
                        SearchForLP(mem, sv2, LP, $"W+0x{wo:X}->+0x{s1:X}->+0x{s2:X}", ref lpFound);
                    }
                }
            }

            if (!lpFound)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  LP World sub-ptr'lerde bulunamadi.\n");
                Console.ResetColor();

                // Fallback: pattern scan for entity pool
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Pattern scan deneniyor...\n");
                Console.ResetColor();

                int modSize = mem.TargetProcess!.MainModule!.ModuleMemorySize;
                byte[] modData = new byte[modSize];
                int chunk = 1024 * 1024;
                for (int off = 0; off < modSize; off += chunk)
                {
                    int sz = Math.Min(chunk, modSize - off);
                    byte[] buf = new byte[sz];
                    if (ZetaMemory.ReadProcessMemory(mem.ProcessHandle, BA + off, buf, sz, out IntPtr mbr))
                        Array.Copy(buf, 0, modData, off, (int)mbr.ToInt64());
                    Console.Write($"\r  Okunuyor {(off + sz) * 100 / modSize}%  ");
                }
                Console.WriteLine();

                var patterns = new[]
                {
                    ("Replay1", "48 8B 0D ?? ?? ?? ?? 48 8D 59 20"),
                    ("Replay2", "48 8B 0D ?? ?? ?? ?? 48 8B D9 48 85 C9"),
                    ("Replay3", "48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 48"),
                    ("PedPool", "48 8B 05 ?? ?? ?? ?? 48 85 C0 74 ?? 8B 48"),
                    ("EntPool", "4C 8B 0D ?? ?? ?? ?? 4D 85 C9 74"),
                    ("EntityP", "48 8B 0D ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B 41"),
                    ("PedFac",  "48 8B 05 ?? ?? ?? ?? 48 8B 48 ?? E8 ?? ?? ?? ?? 48 8B"),
                };

                var seen = new HashSet<long>();
                foreach (var (name, patStr) in patterns)
                {
                    byte?[] pat = Parse(patStr);
                    var matches = Scan(modData, pat);

                    foreach (int pos in matches)
                    {
                        int disp = BitConverter.ToInt32(modData, pos + 3);
                        long resolved = (long)pos + 7 + disp;
                        if (resolved <= 0 || resolved >= modSize) continue;

                        long addr = BA + resolved;
                        long ptr = mem.Read<long>(addr);
                        if (ptr < 0x10000 || ptr > 0x7FFFFFFFFFFF) continue;
                        if (seen.Contains(ptr)) continue;
                        seen.Add(ptr);

                        // Search this pointer's structure for ped-like arrays
                        byte[] pd = new byte[0x400];
                        if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, ptr, pd, pd.Length, out _)) continue;

                        for (int off = 0; off < 0x3F0; off += 8)
                        {
                            long lp = BitConverter.ToInt64(pd, off);
                            if (lp < 0x10000 || lp > 0x7FFFFFFFFFFF) continue;
                            int cnt = BitConverter.ToInt32(pd, off + 8);
                            if (cnt < 2 || cnt > 500) continue;

                            int valid = 0;
                            for (int k = 0; k < Math.Min(cnt, 20); k++)
                            {
                                for (int stride = 0x8; stride <= 0x10; stride += 0x8)
                                {
                                    long ped = mem.Read<long>(lp + (k * stride));
                                    if (ped < 0x10000) continue;
                                    if (ped == LP) { valid += 10; continue; }
                                    float hp = mem.Read<float>(ped + 0x280);
                                    if (hp > 0 && hp < 1000 && !float.IsNaN(hp))
                                    {
                                        long nav = mem.Read<long>(ped + 0x30);
                                        if (nav > 0x10000)
                                        {
                                            Vector3 p = mem.Read<Vector3>(nav + 0x50);
                                            if (MathF.Abs(p.X) > 1) valid++;
                                        }
                                    }
                                }
                            }

                            if (valid >= 2)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"\n  {name} PED LIST! 0x{resolved:X}->+0x{off:X}");
                                Console.WriteLine($"    List:0x{lp:X} Count:{cnt}");

                                int sh = 0;
                                for (int k = 0; k < Math.Min(cnt, 30) && sh < 8; k++)
                                {
                                    long ped = mem.Read<long>(lp + (k * 0x10));
                                    if (ped < 0x10000) { ped = mem.Read<long>(lp + (k * 0x8)); }
                                    if (ped < 0x10000) continue;
                                    float hp = mem.Read<float>(ped + 0x280);
                                    string tag = ped == LP ? " <-LP" : "";
                                    long nav = mem.Read<long>(ped + 0x30);
                                    Vector3 p = nav > 0x10000 ? mem.Read<Vector3>(nav + 0x50) : new Vector3();
                                    Console.WriteLine($"    [{k}] 0x{ped:X} HP:{hp:F0} ({p.X:F0},{p.Y:F0},{p.Z:F0}){tag}");
                                    sh++;
                                }
                                Console.ResetColor();
                                lpFound = true;
                            }
                        }
                    }
                }
            }

            // ═══════════════════════════════
            // C) VIEWMATRIX TEST
            // ═══════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n══ C: VIEWMATRIX ══\n");
            Console.ResetColor();

            long vmPtr = mem.Read<long>(BA + 0x2591ED0);
            if (vmPtr > 0x10000)
            {
                // Test +0x300 and nearby
                foreach (long vmOff in new long[] { 0x2A0, 0x300, 0x340, 0x380 })
                {
                    byte[] vmBuf = new byte[64];
                    ZetaMemory.ReadProcessMemory(mem.ProcessHandle, vmPtr + vmOff, vmBuf, 64, out _);
                    float[] vm = new float[16];
                    Buffer.BlockCopy(vmBuf, 0, vm, 0, 64);

                    bool ok = true;
                    for (int mi = 0; mi < 16; mi++)
                        if (float.IsNaN(vm[mi]) || float.IsInfinity(vm[mi])) { ok = false; break; }
                    if (!ok) { Console.WriteLine($"  +0x{vmOff:X}: NaN/Inf"); continue; }

                    float w = vm[3] * lpPos.X + vm[7] * lpPos.Y + vm[11] * lpPos.Z + vm[15];
                    if (MathF.Abs(w) < 0.01f) { Console.WriteLine($"  +0x{vmOff:X}: W=0"); continue; }

                    float inv = 1f / w;
                    float nx = (vm[0] * lpPos.X + vm[4] * lpPos.Y + vm[8] * lpPos.Z + vm[12]) * inv;
                    float ny = (vm[1] * lpPos.X + vm[5] * lpPos.Y + vm[9] * lpPos.Z + vm[13]) * inv;
                    float sx = 960 + nx * 960;
                    float sy = 540 - ny * 540;

                    string status = (sx > 0 && sx < 1920 && sy > 0 && sy < 1080) ? "EKRANDA" : "dis";
                    Console.WriteLine($"  +0x{vmOff:X}: S({sx:F0},{sy:F0}) W:{w:F2} [{status}]");
                    Console.WriteLine($"    [{vm[0]:F4},{vm[1]:F4},{vm[2]:F4},{vm[3]:F4}]");
                    Console.WriteLine($"    [{vm[4]:F4},{vm[5]:F4},{vm[6]:F4},{vm[7]:F4}]");
                    Console.WriteLine($"    [{vm[8]:F4},{vm[9]:F4},{vm[10]:F4},{vm[11]:F4}]");
                    Console.WriteLine($"    [{vm[12]:F4},{vm[13]:F4},{vm[14]:F4},{vm[15]:F4}]");
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═════════════════════════");
            Console.WriteLine("  PHASE 4 BITTI!");
            Console.WriteLine("  CIKTIYI KOPYALA!");
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

    static bool TestBones(ZetaMemory mem, long addr, out int fStride, out int fPosOff)
    {
        fStride = 0; fPosOff = 0;
        byte[] raw = new byte[2048];
        if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, addr, raw, 2048, out IntPtr br)) return false;
        if (br.ToInt64() < 320) return false;
        int len = (int)br.ToInt64();

        foreach (int stride in new[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60 })
        {
            for (int po = 0; po + 12 <= stride; po += 4)
            {
                int good = 0, total = Math.Min(20, (len - po - 12) / stride);
                if (total < 5) continue;
                bool bad = false;

                for (int b = 0; b < total && !bad; b++)
                {
                    int off = b * stride + po;
                    float x = BitConverter.ToSingle(raw, off);
                    float y = BitConverter.ToSingle(raw, off + 4);
                    float z = BitConverter.ToSingle(raw, off + 8);

                    if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) { bad = true; break; }
                    if (float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z)) { bad = true; break; }

                    bool inR = MathF.Abs(x) < 3 && MathF.Abs(y) < 3 && MathF.Abs(z) < 3;
                    bool nz = MathF.Abs(x) > 0.005f || MathF.Abs(y) > 0.005f || MathF.Abs(z) > 0.005f;
                    if (inR && nz) good++;
                }

                if (!bad && good >= 5) { fStride = stride; fPosOff = po; return true; }
            }
        }
        return false;
    }

    static bool TestBonesWorld(ZetaMemory mem, long addr, Vector3 lp, out int fStride, out int fPosOff)
    {
        fStride = 0; fPosOff = 0;
        byte[] raw = new byte[2048];
        if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, addr, raw, 2048, out IntPtr br)) return false;
        if (br.ToInt64() < 320) return false;
        int len = (int)br.ToInt64();

        foreach (int stride in new[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60 })
        {
            for (int po = 0; po + 12 <= stride; po += 4)
            {
                int good = 0, total = Math.Min(15, (len - po - 12) / stride);
                if (total < 5) continue;

                for (int b = 0; b < total; b++)
                {
                    int off = b * stride + po;
                    float x = BitConverter.ToSingle(raw, off);
                    float y = BitConverter.ToSingle(raw, off + 4);
                    float z = BitConverter.ToSingle(raw, off + 8);

                    if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) break;
                    if (MathF.Abs(x - lp.X) < 5 && MathF.Abs(y - lp.Y) < 5 && MathF.Abs(z - lp.Z) < 5)
                        good++;
                }

                if (good >= 5) { fStride = stride; fPosOff = po; return true; }
            }
        }
        return false;
    }

    static bool SearchForLP(ZetaMemory mem, long structAddr, long LP, string path, ref bool found)
    {
        byte[] d = new byte[0x2000];
        if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, structAddr, d, d.Length, out IntPtr br)) return false;
        int len = (int)br.ToInt64();

        for (int i = 0; i < len - 8; i += 8)
        {
            if (BitConverter.ToInt64(d, i) == LP)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  LP BULUNDU! {path} offset +0x{i:X}");
                Console.ResetColor();

                for (int stride = 0x8; stride <= 0x10; stride += 0x8)
                {
                    Console.Write($"    Stride 0x{stride:X}:");
                    for (int di = -3; di <= 5; di++)
                    {
                        int nOff = i + (di * stride);
                        if (nOff < 0 || nOff + 8 > len) continue;
                        long nv = BitConverter.ToInt64(d, nOff);
                        if (nv < 0x1000) continue;
                        string tag = nv == LP ? "(LP)" : "";
                        float hp = 0;
                        if (nv > 0x10000 && nv < 0x7FFFFFFFFFFF && nv != LP)
                        {
                            hp = mem.Read<float>(nv + 0x280);
                            long nav = mem.Read<long>(nv + 0x30);
                            Vector3 p = nav > 0x10000 ? mem.Read<Vector3>(nav + 0x50) : new Vector3();
                            Console.Write($" [{di}]HP:{hp:F0}({p.X:F0},{p.Y:F0})");
                        }
                        else Console.Write($" [{di}]{tag}");
                    }
                    Console.WriteLine();
                }
                found = true;
                return true;
            }
        }
        return false;
    }

    static void Report(ZetaMemory mem, string path, long addr, int stride, int posOff)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  SKELETON BULUNDU! {path}");
        Console.WriteLine($"  Stride:0x{stride:X} PosOff:0x{posOff:X}\n");
        for (int b = 0; b < 20; b++)
        {
            Vector3 bp = mem.Read<Vector3>(addr + (b * stride) + posOff);
            Console.WriteLine($"    B[{b,2}]: ({bp.X:F4}, {bp.Y:F4}, {bp.Z:F4})");
        }
        Console.ResetColor();
    }

    static void ReportW(ZetaMemory mem, string path, long addr, int stride, int posOff)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  WORLD-BONES BULUNDU! {path}");
        Console.WriteLine($"  Stride:0x{stride:X} PosOff:0x{posOff:X}\n");
        for (int b = 0; b < 15; b++)
        {
            Vector3 bp = mem.Read<Vector3>(addr + (b * stride) + posOff);
            Console.WriteLine($"    B[{b,2}]: ({bp.X:F1}, {bp.Y:F1}, {bp.Z:F1})");
        }
        Console.ResetColor();
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
        for (int i = 0; i <= data.Length - pat.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < pat.Length; j++)
                if (pat[j].HasValue && data[i + j] != pat[j].Value) { ok = false; break; }
            if (ok) r.Add(i);
        }
        return r;
    }
}
