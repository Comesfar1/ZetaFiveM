using System;
using System.Collections.Generic;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Console.Title = "Zeta FINAL";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== ZETA FINAL SCANNER ===\n");
            Console.ResetColor();

            using var mem = new ZetaMemory();
            if (!mem.Baglan()) { Console.ReadLine(); return; }

            long BA = mem.BaseAddress;
            long worldPtr = mem.Read<long>(BA + 0x25B14B0);
            long LP = mem.Read<long>(worldPtr + 0x8);
            long lpNav = mem.Read<long>(LP + 0x30);
            Vector3 lpPos = lpNav > 0x10000 ? mem.Read<Vector3>(lpNav + 0x50) : new Vector3();
            float lpHp = mem.Read<float>(LP + 0x280);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"LP: 0x{LP:X}  HP:{lpHp:F1}");
            Console.WriteLine($"Pos: ({lpPos.X:F1}, {lpPos.Y:F1}, {lpPos.Z:F1})\n");
            Console.ResetColor();

            // ════════════════════════════════
            //  A) PED LIST — Entity Deep Dump
            // ════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("══ A: PED ENTITY DEEP DUMP ══\n");
            Console.ResetColor();

            // Follow the chain found in Phase 2
            long c1 = mem.Read<long>(worldPtr + 0x10);
            long c2 = c1 > 0x10000 ? mem.Read<long>(c1 + 0x58) : 0;
            long c3 = c2 > 0x10000 ? mem.Read<long>(c2 + 0x18) : 0;
            long pedList = c3 > 0x10000 ? mem.Read<long>(c3 + 0x8) : 0;
            int pedMax = c3 > 0x10000 ? mem.Read<int>(c3 + 0x10) : 0;

            Console.WriteLine($"  Chain: c1=0x{c1:X} c2=0x{c2:X} c3=0x{c3:X}");
            Console.WriteLine($"  List=0x{pedList:X} Max={pedMax}\n");

            // Also try alternate chains
            if (pedList < 0x10000 || pedMax <= 0)
            {
                Console.WriteLine("  Ana chain calismadi. Alternatif deneniyor...\n");
                // Try different sub-offsets from world
                long[] wOffs = { 0x10, 0x50, 0x60, 0x70, 0x80, 0x90, 0xA0, 0xB0, 0xC0 };
                foreach (long wo in wOffs)
                {
                    long wa = mem.Read<long>(worldPtr + wo);
                    if (wa < 0x10000) continue;
                    byte[] wd = new byte[0x400];
                    if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, wa, wd, wd.Length, out _)) continue;

                    for (int i = 0; i < 0x3F0; i += 8)
                    {
                        long lv = BitConverter.ToInt64(wd, i);
                        if (lv < 0x10000 || lv > 0x7FFFFFFFFFFF) continue;
                        int cnt = BitConverter.ToInt32(wd, i + 8);
                        if (cnt < 5 || cnt > 500) continue;

                        long testPed = mem.Read<long>(lv);
                        if (testPed > 0x10000 && testPed < 0x7FFFFFFFFFFF)
                        {
                            Console.WriteLine($"  W+0x{wo:X}->+0x{i:X}: List=0x{lv:X} Count={cnt}");
                        }
                    }
                }
            }

            if (pedList < 0x10000 || pedMax <= 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  [X] Ped list bulunamadi\n");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

            // LP'yi dump et — referans olarak
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("── LP DUMP (referans) ──\n");
            Console.ResetColor();

            byte[] lpDump = new byte[4096];
            ZetaMemory.ReadProcessMemory(mem.ProcessHandle, LP, lpDump, lpDump.Length, out _);

            // LP'de konum float'larini bul
            Console.Write("  LP'de X koord (-1722 civari) bulunan offset'ler: ");
            for (int off = 0; off < 4000; off += 4)
            {
                float v = BitConverter.ToSingle(lpDump, off);
                if (!float.IsNaN(v) && MathF.Abs(v - lpPos.X) < 2f)
                    Console.Write($"0x{off:X} ");
            }
            Console.WriteLine();

            Console.Write("  LP'de Y koord (-1114 civari) bulunan offset'ler: ");
            for (int off = 0; off < 4000; off += 4)
            {
                float v = BitConverter.ToSingle(lpDump, off);
                if (!float.IsNaN(v) && MathF.Abs(v - lpPos.Y) < 2f)
                    Console.Write($"0x{off:X} ");
            }
            Console.WriteLine();

            Console.Write("  LP'de Z koord (13 civari) bulunan offset'ler: ");
            for (int off = 0; off < 4000; off += 4)
            {
                float v = BitConverter.ToSingle(lpDump, off);
                if (!float.IsNaN(v) && MathF.Abs(v - lpPos.Z) < 1f && MathF.Abs(v) > 5f)
                    Console.Write($"0x{off:X} ");
            }
            Console.WriteLine("\n");

            // Her ped entity'yi deep scan et
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("── PED ENTITY SCANS ──\n");
            Console.ResetColor();

            if (pedMax > 200) pedMax = 200;
            int totalScanned = 0, pedWithSkel = 0;

            for (int i = 0; i < pedMax && totalScanned < 15; i++)
            {
                long ped = mem.Read<long>(pedList + (i * 0x10));
                if (ped < 0x10000 || ped > 0x7FFFFFFFFFFF || ped == LP) continue;
                if (Math.Abs(ped - pedList) < 0x10000) continue; // Pool meta data, skip

                // Read first 4KB
                byte[] pd = new byte[4096];
                IntPtr br;
                if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, ped, pd, pd.Length, out br)) continue;
                if (br.ToInt64() < 2048) continue;

                totalScanned++;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  ═══ [{i}] 0x{ped:X} ═══");
                Console.ResetColor();

                // 1. Position search — find floats that could be world coords
                Console.Write("  World-pos (X:-2000~-500):");
                int posHits = 0;
                var posOffsets = new List<int>();
                for (int off = 0; off < 2048; off += 4)
                {
                    float v = BitConverter.ToSingle(pd, off);
                    if (!float.IsNaN(v) && v > -3000 && v < -100 && posHits < 8)
                    {
                        // Check if next float could be Y
                        float v2 = BitConverter.ToSingle(pd, off + 4);
                        if (!float.IsNaN(v2) && v2 > -3000 && v2 < 0)
                        {
                            Console.Write($" 0x{off:X}=({v:F0},{v2:F0})");
                            posOffsets.Add(off);
                            posHits++;
                        }
                    }
                }
                if (posHits == 0) Console.Write(" -yok-");
                Console.WriteLine();

                // 1b. Position through nav pointers
                Console.Write("  Nav-pos:");
                int navHits = 0;
                for (int off = 0; off < 512; off += 8)
                {
                    long ptr = BitConverter.ToInt64(pd, off);
                    if (ptr < 0x10000 || ptr > 0x7FFFFFFFFFFF) continue;

                    // Try various sub-offsets for position
                    foreach (int po in new[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90, 0xA0, 0xB0, 0xC0, 0xD0 })
                    {
                        Vector3 p = mem.Read<Vector3>(ptr + po);
                        if (!float.IsNaN(p.X) && MathF.Abs(p.X) > 50 && MathF.Abs(p.X) < 5000
                            && !float.IsNaN(p.Y) && MathF.Abs(p.Y) > 50 && MathF.Abs(p.Y) < 5000
                            && !float.IsNaN(p.Z) && MathF.Abs(p.Z) < 1000 && navHits < 5)
                        {
                            float dist = MathF.Sqrt(
                                (p.X - lpPos.X) * (p.X - lpPos.X) +
                                (p.Y - lpPos.Y) * (p.Y - lpPos.Y));
                            string near = dist < 200 ? " <<<YAKIN" : "";
                            Console.Write($" +0x{off:X}->0x{po:X}=({p.X:F0},{p.Y:F0},{p.Z:F0}|{dist:F0}m){near}");
                            navHits++;
                        }
                    }
                }
                if (navHits == 0) Console.Write(" -yok-");
                Console.WriteLine();

                // 2. HP search
                Console.Write("  HP (50-500):");
                int hpHits = 0;
                for (int off = 0x200; off < 0x400; off += 4)
                {
                    float v = BitConverter.ToSingle(pd, off);
                    if (!float.IsNaN(v) && !float.IsInfinity(v) && v >= 50f && v <= 500f && hpHits < 8)
                    { Console.Write($" 0x{off:X}={v:F1}"); hpHits++; }
                }
                if (hpHits == 0) Console.Write(" -yok-");
                Console.Write("  HP(1-50):");
                for (int off = 0x200; off < 0x400; off += 4)
                {
                    float v = BitConverter.ToSingle(pd, off);
                    if (!float.IsNaN(v) && !float.IsInfinity(v) && v >= 1f && v < 50f && hpHits < 12)
                    { Console.Write($" 0x{off:X}={v:F1}"); hpHits++; }
                }
                Console.WriteLine();

                // 3. Skeleton search
                Console.Write("  Skel:");
                bool hasSkel = false;
                foreach (int sOff in new[] { 0x420, 0x430, 0x410, 0x440, 0x450, 0x400, 0x3F0, 0x460 })
                {
                    if (sOff + 8 > pd.Length) continue;
                    long sk = BitConverter.ToInt64(pd, sOff);
                    if (sk < 0x10000 || sk > 0x7FFFFFFFFFFF) continue;

                    foreach (int bcOff in new[] { 0x0, 0x8, 0x10, 0x18, 0x20, 0x28 })
                    {
                        long bc = mem.Read<long>(sk + bcOff);
                        if (bc < 0x10000 || bc > 0x7FFFFFFFFFFF) continue;

                        // Test multiple stride/pos combinations
                        foreach (var (stride, posOff) in new[] { (0x20, 0x10), (0x20, 0x0), (0x40, 0x30), (0x40, 0x10), (0x30, 0x20), (0x30, 0x0), (0x10, 0x0) })
                        {
                            int good = 0;
                            for (int b = 0; b < 10; b++)
                            {
                                Vector3 bp = mem.Read<Vector3>(bc + (b * stride) + posOff);
                                if (!float.IsNaN(bp.X) && !float.IsInfinity(bp.X)
                                    && MathF.Abs(bp.X) > 0.005f && MathF.Abs(bp.X) < 2f
                                    && MathF.Abs(bp.Y) < 2f && MathF.Abs(bp.Z) < 2f)
                                    good++;
                            }
                            if (good >= 4)
                            {
                                Console.Write($" +0x{sOff:X}->+0x{bcOff:X} S=0x{stride:X} P=0x{posOff:X} [{good}/10]");
                                hasSkel = true;

                                // Print first 5 bones
                                Console.WriteLine();
                                for (int b = 0; b < 5; b++)
                                {
                                    Vector3 bp = mem.Read<Vector3>(bc + (b * stride) + posOff);
                                    Console.Write($"    B{b}:({bp.X:F4},{bp.Y:F4},{bp.Z:F4})");
                                }
                                Console.WriteLine();
                                goto skelDone;
                            }
                        }

                        // World-coord bones
                        foreach (var (stride, posOff) in new[] { (0x20, 0x10), (0x20, 0x0), (0x40, 0x30), (0x10, 0x0) })
                        {
                            int good = 0;
                            for (int b = 0; b < 10; b++)
                            {
                                Vector3 bp = mem.Read<Vector3>(bc + (b * stride) + posOff);
                                if (!float.IsNaN(bp.X) && MathF.Abs(bp.X) > 50 && MathF.Abs(bp.X) < 5000
                                    && MathF.Abs(bp.Y) > 50 && MathF.Abs(bp.Y) < 5000)
                                    good++;
                            }
                            if (good >= 4)
                            {
                                Console.Write($" WORLD +0x{sOff:X}->+0x{bcOff:X} S=0x{stride:X} P=0x{posOff:X} [{good}/10]");
                                hasSkel = true;
                                Console.WriteLine();
                                for (int b = 0; b < 3; b++)
                                {
                                    Vector3 bp = mem.Read<Vector3>(bc + (b * stride) + posOff);
                                    Console.Write($"    B{b}:({bp.X:F1},{bp.Y:F1},{bp.Z:F1})");
                                }
                                Console.WriteLine();
                                goto skelDone;
                            }
                        }
                    }
                }
                skelDone:
                if (!hasSkel) Console.WriteLine(" -yok-");

                // 4. PedType
                Console.Write("  Type(val=2):");
                for (int off = 0x1000; off < Math.Min(pd.Length, 0x1200); off += 4)
                {
                    if (off + 4 > pd.Length) break;
                    int v = BitConverter.ToInt32(pd, off);
                    if ((v & 0xFF) == 2 && v < 256)
                        Console.Write($" 0x{off:X}={v}");
                }
                Console.WriteLine("\n");

                if (hasSkel) pedWithSkel++;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  Scanned:{totalScanned} WithSkel:{pedWithSkel}\n");
            Console.ResetColor();

            // ════════════════════════════════
            //  B) LP ENTITY DUMP
            // ════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("══ B: LP ENTITY STRUCTURE ══\n");
            Console.ResetColor();

            // LP skeleton — check ALL pointer combinations more carefully
            Console.Write("  LP Skel:");
            for (int o = 0x100; o < 0xC00; o += 8)
            {
                long sk = BitConverter.ToInt64(lpDump, o);
                if (sk < 0x10000 || sk > 0x7FFFFFFFFFFF) continue;

                // Read skeleton struct
                byte[] skBuf = new byte[0x60];
                if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, sk, skBuf, skBuf.Length, out IntPtr sbr)) continue;
                if (sbr.ToInt64() < 0x40) continue;

                for (int bc = 0; bc < 0x50; bc += 8)
                {
                    long bonePtr = BitConverter.ToInt64(skBuf, bc);
                    if (bonePtr < 0x10000 || bonePtr > 0x7FFFFFFFFFFF) continue;

                    byte[] boneBuf = new byte[2048];
                    if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, bonePtr, boneBuf, boneBuf.Length, out IntPtr bbr)) continue;
                    int bLen = (int)bbr.ToInt64();
                    if (bLen < 200) continue;

                    // Count small floats (bone local coords)
                    int smCount = 0;
                    for (int fi = 0; fi < Math.Min(bLen, 1024); fi += 4)
                    {
                        float f = BitConverter.ToSingle(boneBuf, fi);
                        if (!float.IsNaN(f) && !float.IsInfinity(f) && MathF.Abs(f) > 0.01f && MathF.Abs(f) < 2f)
                            smCount++;
                    }

                    if (smCount >= 15)
                    {
                        Console.Write($"\n    +0x{o:X}->+0x{bc:X} small_floats:{smCount}");

                        // Find stride by looking for repeating pattern
                        for (int stride = 0x10; stride <= 0x60; stride += 0x10)
                        {
                            for (int pOff = 0; pOff + 12 <= stride; pOff += 4)
                            {
                                int gc = 0;
                                for (int b = 0; b < Math.Min(20, (bLen - pOff) / stride); b++)
                                {
                                    int idx = b * stride + pOff;
                                    if (idx + 12 > bLen) break;
                                    float x = BitConverter.ToSingle(boneBuf, idx);
                                    float y = BitConverter.ToSingle(boneBuf, idx + 4);
                                    float z = BitConverter.ToSingle(boneBuf, idx + 8);
                                    if (!float.IsNaN(x) && MathF.Abs(x) < 3 && MathF.Abs(y) < 3 && MathF.Abs(z) < 3
                                        && (MathF.Abs(x) > 0.005f || MathF.Abs(y) > 0.005f || MathF.Abs(z) > 0.005f))
                                        gc++;
                                }
                                if (gc >= 8)
                                {
                                    Console.Write($" CONFIRMED stride=0x{stride:X} pos=0x{pOff:X} good={gc}");
                                    Console.WriteLine();
                                    for (int b = 0; b < 15; b++)
                                    {
                                        int idx = b * stride + pOff;
                                        if (idx + 12 > bLen) break;
                                        float x = BitConverter.ToSingle(boneBuf, idx);
                                        float y = BitConverter.ToSingle(boneBuf, idx + 4);
                                        float z = BitConverter.ToSingle(boneBuf, idx + 8);
                                        Console.WriteLine($"      B[{b,2}]: ({x:F4}, {y:F4}, {z:F4})");
                                    }
                                    goto lpSkelDone;
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine(" -yok-");
            lpSkelDone:
            Console.WriteLine();

            // ════════════════════════════════
            //  C) VIEWMATRIX FINAL
            // ════════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("══ C: VIEWMATRIX FINAL ══\n");
            Console.ResetColor();

            // Scan module for ViewProjectionMatrix pattern
            int modSize = mem.TargetProcess!.MainModule!.ModuleMemorySize;
            byte[] mod = new byte[modSize];
            int chk = 1024 * 1024;
            for (int off = 0; off < modSize; off += chk)
            {
                int sz = Math.Min(chk, modSize - off);
                byte[] buf = new byte[sz];
                if (ZetaMemory.ReadProcessMemory(mem.ProcessHandle, BA + off, buf, sz, out IntPtr mbr2))
                    Array.Copy(buf, 0, mod, off, (int)mbr2.ToInt64());
                Console.Write($"\r  Modul okunuyor {(off + sz) * 100 / modSize}%  ");
            }
            Console.WriteLine();

            // Multiple VM patterns
            var vmPats = new[]
            {
                "48 8B 15 ?? ?? ?? ?? 48 8D 2D",
                "48 8B 15 ?? ?? ?? ?? 45 33 C0",
                "48 8B 3D ?? ?? ?? ?? 48 8B CF E8",
                "48 8B 0D ?? ?? ?? ?? 48 8D 55",
                "48 8B 05 ?? ?? ?? ?? 48 8B 48 ?? 48 8D 54",
                "48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 48",
                "48 8B 05 ?? ?? ?? ?? 48 85 C0 74 ?? F3 0F",
                "48 8B 0D ?? ?? ?? ?? 48 8B 01 FF 50",
            };

            var testedPtrs = new HashSet<long>();
            int vmFound = 0;

            foreach (string patStr in vmPats)
            {
                byte?[] pat = Parse(patStr);
                var hits = Scan(mod, pat);

                foreach (int pos in hits)
                {
                    int disp = BitConverter.ToInt32(mod, pos + 3);
                    long resolved = (long)pos + 7 + disp;
                    if (resolved <= 0 || resolved >= modSize) continue;

                    long ptr = mem.Read<long>(BA + resolved);
                    if (ptr < 0x10000 || ptr > 0x7FFFFFFFFFFF) continue;
                    if (testedPtrs.Contains(ptr)) continue;
                    testedPtrs.Add(ptr);

                    // Read a large chunk from this pointer
                    byte[] vmData = new byte[0x800];
                    if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, ptr, vmData, vmData.Length, out _)) continue;

                    for (int mOff = 0; mOff <= 0x700; mOff += 4)
                    {
                        if (mOff + 64 > vmData.Length) break;

                        float[] m = new float[16];
                        Buffer.BlockCopy(vmData, mOff, m, 0, 64);

                        bool bad = false;
                        int reasonable = 0;
                        for (int mi = 0; mi < 16; mi++)
                        {
                            if (float.IsNaN(m[mi]) || float.IsInfinity(m[mi])) { bad = true; break; }
                            if (MathF.Abs(m[mi]) > 0.0001f && MathF.Abs(m[mi]) < 10f) reasonable++;
                        }
                        if (bad || reasonable < 8) continue;

                        // W2S test with LP position
                        float w = m[3] * lpPos.X + m[7] * lpPos.Y + m[11] * lpPos.Z + m[15];
                        if (MathF.Abs(w) < 0.01f) continue;

                        float inv = 1f / w;
                        float nx = (m[0] * lpPos.X + m[4] * lpPos.Y + m[8] * lpPos.Z + m[12]) * inv;
                        float ny = (m[1] * lpPos.X + m[5] * lpPos.Y + m[9] * lpPos.Z + m[13]) * inv;

                        // NDC should be roughly in -1 to 1 range
                        if (MathF.Abs(nx) > 2f || MathF.Abs(ny) > 2f) continue;

                        float sx = 960 + nx * 960;
                        float sy = 540 - ny * 540;

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  VM ADAY: 0x{resolved:X}->+0x{mOff:X}");
                        Console.WriteLine($"    S({sx:F0},{sy:F0}) NDC({nx:F3},{ny:F3}) W:{w:F2}");
                        Console.WriteLine($"    [{m[0]:F4},{m[1]:F4},{m[2]:F4},{m[3]:F4}]");
                        Console.WriteLine($"    [{m[4]:F4},{m[5]:F4},{m[6]:F4},{m[7]:F4}]");
                        Console.WriteLine($"    [{m[8]:F4},{m[9]:F4},{m[10]:F4},{m[11]:F4}]");
                        Console.WriteLine($"    [{m[12]:F4},{m[13]:F4},{m[14]:F4},{m[15]:F4}]");
                        Console.ResetColor();
                        vmFound++;
                        if (vmFound >= 5) goto vmDone;
                    }
                }
            }

            // Direct base offsets
            Console.WriteLine("\n  Direct base test:");
            for (long dOff = 0x1F0000; dOff <= 0x210000; dOff += 0x10)
            {
                byte[] tb = new byte[64];
                if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, BA + dOff, tb, 64, out IntPtr tbr3)) continue;
                if (tbr3.ToInt64() != 64) continue;

                float[] m = new float[16];
                Buffer.BlockCopy(tb, 0, m, 0, 64);

                bool bad = false;
                int rea = 0;
                for (int mi = 0; mi < 16; mi++)
                {
                    if (float.IsNaN(m[mi]) || float.IsInfinity(m[mi])) { bad = true; break; }
                    if (MathF.Abs(m[mi]) > 0.0001f && MathF.Abs(m[mi]) < 10f) rea++;
                }
                if (bad || rea < 10) continue;

                float w = m[3] * lpPos.X + m[7] * lpPos.Y + m[11] * lpPos.Z + m[15];
                if (MathF.Abs(w) < 0.1f) continue;

                float inv = 1f / w;
                float nx = (m[0] * lpPos.X + m[4] * lpPos.Y + m[8] * lpPos.Z + m[12]) * inv;
                float ny = (m[1] * lpPos.X + m[5] * lpPos.Y + m[9] * lpPos.Z + m[13]) * inv;

                if (MathF.Abs(nx) > 2f || MathF.Abs(ny) > 2f) continue;

                float sx = 960 + nx * 960;
                float sy = 540 - ny * 540;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"    BASE+0x{dOff:X}: S({sx:F0},{sy:F0}) W:{w:F2} NDC({nx:F3},{ny:F3})");
                Console.ResetColor();
                vmFound++;
                if (vmFound >= 8) break;
            }

            vmDone:
            if (vmFound == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  [X] Valid ViewMatrix bulunamadi!");
                Console.ResetColor();
            }

            // ═══════════════════════════════
            //  OZET
            // ═══════════════════════════════
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n═════════════════════════════════");
            Console.WriteLine("  FINAL SCAN TAMAMLANDI!");
            Console.WriteLine("  TUM CIKTIYI KOPYALA!");
            Console.WriteLine("═════════════════════════════════\n");
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
