using System;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Console.Title = "Zeta Phase 3";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== ZETA PHASE 3 ===\n");
            Console.ResetColor();

            using var mem = new ZetaMemory();
            if (!mem.Baglan()) { Console.WriteLine("[X] FiveM yok"); Console.ReadLine(); return; }

            long BA = mem.BaseAddress;
            long worldPtr = mem.Read<long>(BA + 0x25B14B0);
            long LP = mem.Read<long>(worldPtr + 0x8);
            float lpHp = mem.Read<float>(LP + 0x280);
            long lpNav = mem.Read<long>(LP + 0x30);
            Vector3 lpPos = lpNav > 0x10000 ? mem.Read<Vector3>(lpNav + 0x50) : new Vector3();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"LP:0x{LP:X} HP:{lpHp} Pos:({lpPos.X:F1},{lpPos.Y:F1},{lpPos.Z:F1})");
            Console.ResetColor();

            // ═══════════════════════════════
            //  1. LP SKELETON FULL SCAN
            // ═══════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n══ LP SKELETON SCAN ══\n");
            Console.ResetColor();

            bool lpSkelFound = false;

            for (long o = 0x100; o <= 0xB00 && !lpSkelFound; o += 8)
            {
                long sk = mem.Read<long>(LP + o);
                if (sk < 0x10000 || sk > 0x7FFFFFFFFFFF) continue;

                for (long bc = 0; bc <= 0x40 && !lpSkelFound; bc += 8)
                {
                    long b = mem.Read<long>(sk + bc);
                    if (b < 0x10000 || b > 0x7FFFFFFFFFFF) continue;

                    // Stride 0x20 pos +0x10
                    int good = 0;
                    for (int bi = 0; bi < 5; bi++)
                    {
                        Vector3 bp = mem.Read<Vector3>(b + (bi * 0x20) + 0x10);
                        if (!float.IsNaN(bp.X) && MathF.Abs(bp.X) < 5 && MathF.Abs(bp.Y) < 5 && MathF.Abs(bp.Z) < 5
                            && (MathF.Abs(bp.X) > 0.01f || MathF.Abs(bp.Y) > 0.01f || MathF.Abs(bp.Z) > 0.01f))
                            good++;
                    }
                    if (good >= 3)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  BULUNDU! LP+0x{o:X} -> +0x{bc:X} (stride 0x20, pos +0x10)");
                        for (int bi = 0; bi < 15; bi++)
                        {
                            Vector3 bp = mem.Read<Vector3>(b + (bi * 0x20) + 0x10);
                            Console.WriteLine($"    B[{bi,2}]: ({bp.X:F4}, {bp.Y:F4}, {bp.Z:F4})");
                        }
                        Console.ResetColor();
                        lpSkelFound = true; break;
                    }

                    // Stride 0x40 pos +0x30
                    good = 0;
                    for (int bi = 0; bi < 5; bi++)
                    {
                        Vector3 bp = mem.Read<Vector3>(b + (bi * 0x40) + 0x30);
                        if (!float.IsNaN(bp.X) && MathF.Abs(bp.X) < 5 && MathF.Abs(bp.Y) < 5 && MathF.Abs(bp.Z) < 5
                            && (MathF.Abs(bp.X) > 0.01f || MathF.Abs(bp.Y) > 0.01f || MathF.Abs(bp.Z) > 0.01f))
                            good++;
                    }
                    if (good >= 3)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  BULUNDU! LP+0x{o:X} -> +0x{bc:X} (stride 0x40, pos +0x30)");
                        for (int bi = 0; bi < 15; bi++)
                        {
                            Vector3 bp = mem.Read<Vector3>(b + (bi * 0x40) + 0x30);
                            Console.WriteLine($"    B[{bi,2}]: ({bp.X:F4}, {bp.Y:F4}, {bp.Z:F4})");
                        }
                        Console.ResetColor();
                        lpSkelFound = true; break;
                    }

                    // Stride 0x20 pos +0x00
                    good = 0;
                    for (int bi = 0; bi < 5; bi++)
                    {
                        Vector3 bp = mem.Read<Vector3>(b + (bi * 0x20));
                        if (!float.IsNaN(bp.X) && MathF.Abs(bp.X) < 5 && MathF.Abs(bp.Y) < 5 && MathF.Abs(bp.Z) < 5
                            && (MathF.Abs(bp.X) > 0.01f || MathF.Abs(bp.Y) > 0.01f || MathF.Abs(bp.Z) > 0.01f))
                            good++;
                    }
                    if (good >= 3)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  BULUNDU! LP+0x{o:X} -> +0x{bc:X} (stride 0x20, pos +0x00)");
                        for (int bi = 0; bi < 15; bi++)
                        {
                            Vector3 bp = mem.Read<Vector3>(b + (bi * 0x20));
                            Console.WriteLine($"    B[{bi,2}]: ({bp.X:F4}, {bp.Y:F4}, {bp.Z:F4})");
                        }
                        Console.ResetColor();
                        lpSkelFound = true; break;
                    }
                }
            }

            if (!lpSkelFound)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  LP Skeleton bulunamadi (0x100-0xB00)");
                Console.ResetColor();

                // LP+0x420 raw dump
                long sk420 = mem.Read<long>(LP + 0x420);
                Console.WriteLine($"\n  LP+0x420 = 0x{sk420:X}");
                if (sk420 > 0x10000 && sk420 < 0x7FFFFFFFFFFF)
                {
                    for (long bc = 0; bc <= 0x40; bc += 8)
                    {
                        long b = mem.Read<long>(sk420 + bc);
                        Console.Write($"    +0x{bc:X}=0x{b:X}");
                        if (b > 0x10000 && b < 0x7FFFFFFFFFFF)
                        {
                            Console.Write(" floats:");
                            for (int ri = 0; ri < 128; ri += 4)
                            {
                                byte[] raw = new byte[4];
                                ZetaMemory.ReadProcessMemory(mem.ProcessHandle, b + ri, raw, 4, out _);
                                float fv = BitConverter.ToSingle(raw, 0);
                                if (MathF.Abs(fv) > 0.005f && MathF.Abs(fv) < 10f && !float.IsNaN(fv))
                                    Console.Write($" [0x{ri:X}={fv:F3}]");
                            }
                        }
                        Console.WriteLine();
                    }
                }
            }

            // ═══════════════════════════════
            //  2. PED LIST - DEEP SCAN
            // ═══════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n══ PED LIST DEEP ══\n");
            Console.ResetColor();

            long c1 = mem.Read<long>(worldPtr + 0x10);
            long c2 = c1 > 0x10000 ? mem.Read<long>(c1 + 0x58) : 0;
            long c3 = c2 > 0x10000 ? mem.Read<long>(c2 + 0x18) : 0;
            long pedList = c3 > 0x10000 ? mem.Read<long>(c3 + 0x8) : 0;
            int pedMax = c3 > 0x10000 ? mem.Read<int>(c3 + 0x10) : 0;

            Console.WriteLine($"  List:0x{pedList:X} Max:{pedMax}");

            if (pedList > 0x10000 && pedMax > 0)
            {
                if (pedMax > 200) pedMax = 200;
                int shown = 0;

                for (int i = 0; i < pedMax && shown < 6; i++)
                {
                    long ped = mem.Read<long>(pedList + (i * 0x10));
                    if (ped < 0x10000 || ped == LP || ped > 0x7FFFFFFFFFFF) continue;
                    if (Math.Abs(ped - pedList) < 0x10000) continue;

                    byte[] pd = new byte[4096];
                    if (!ZetaMemory.ReadProcessMemory(mem.ProcessHandle, ped, pd, pd.Length, out IntPtr rb)) continue;
                    if (rb.ToInt64() < 2048) continue;

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"\n  ── [{i}] 0x{ped:X} ──");
                    Console.ResetColor();

                    // HP scan (float 50-500)
                    Console.Write("  HP:");
                    int hc = 0;
                    for (int off = 0; off < 2048 && hc < 10; off += 4)
                    {
                        float v = BitConverter.ToSingle(pd, off);
                        if (!float.IsNaN(v) && !float.IsInfinity(v) && v >= 50f && v <= 500f)
                        { Console.Write($" 0x{off:X}={v:F0}"); hc++; }
                    }
                    if (hc == 0)
                    {
                        // Also check for smaller HP values (1-50)
                        Console.Write(" (1-50):");
                        for (int off = 0; off < 2048 && hc < 5; off += 4)
                        {
                            float v = BitConverter.ToSingle(pd, off);
                            if (!float.IsNaN(v) && !float.IsInfinity(v) && v >= 1f && v < 50f)
                            { Console.Write($" 0x{off:X}={v:F1}"); hc++; }
                        }
                    }
                    if (hc == 0) Console.Write(" -yok-");
                    Console.WriteLine();

                    // Raw at 0x280
                    Console.WriteLine($"  +0x280=0x{BitConverter.ToUInt32(pd, 0x280):X8}");

                    // Nav/Pos scan
                    Console.Write("  Pos:");
                    int pc = 0;
                    for (int off = 0; off < 256 && pc < 5; off += 8)
                    {
                        long ptr = BitConverter.ToInt64(pd, off);
                        if (ptr < 0x10000 || ptr > 0x7FFFFFFFFFFF) continue;

                        foreach (int po in new[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90 })
                        {
                            Vector3 p = mem.Read<Vector3>(ptr + po);
                            if (!float.IsNaN(p.X) && MathF.Abs(p.X) > 10 && MathF.Abs(p.X) < 10000
                                && MathF.Abs(p.Y) > 10 && MathF.Abs(p.Y) < 10000)
                            {
                                Console.Write($" +0x{off:X}->0x{po:X}=({p.X:F0},{p.Y:F0},{p.Z:F0})");
                                pc++; break;
                            }
                        }
                    }
                    if (pc == 0) Console.Write(" -yok-");
                    Console.WriteLine();

                    // Skeleton
                    long psk = BitConverter.ToInt64(pd, 0x420);
                    if (psk > 0x10000 && psk < 0x7FFFFFFFFFFF)
                    {
                        long pbc = mem.Read<long>(psk + 0x18);
                        if (pbc > 0x10000)
                        {
                            Console.Write("  Bones20:");
                            for (int bi = 0; bi < 5; bi++)
                            {
                                Vector3 bp = mem.Read<Vector3>(pbc + (bi * 0x20) + 0x10);
                                Console.Write($" ({bp.X:F3},{bp.Y:F3},{bp.Z:F3})");
                            }
                            Console.WriteLine();
                            Console.Write("  Bones40:");
                            for (int bi = 0; bi < 5; bi++)
                            {
                                Vector3 bp = mem.Read<Vector3>(pbc + (bi * 0x40) + 0x30);
                                Console.Write($" ({bp.X:F3},{bp.Y:F3},{bp.Z:F3})");
                            }
                            Console.WriteLine();
                        }
                        else Console.WriteLine("  Skel: bc=null");
                    }
                    else Console.WriteLine("  Skel: null");

                    // PedType
                    Console.Write("  Type:");
                    foreach (int to in new[] { 0x1088, 0x10B8, 0x10A8, 0x10C8 })
                    {
                        if (to + 4 <= pd.Length)
                        {
                            int pt = BitConverter.ToInt32(pd, to);
                            Console.Write($" 0x{to:X}={pt}({pt & 0xFF})");
                        }
                    }
                    Console.WriteLine();

                    shown++;
                }

                if (shown == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n  Listede valid ped yok! Birinin YANINDA ol.");
                    Console.ResetColor();
                }
            }

            // ═══════════════════════════════
            //  3. VIEWMATRIX W2S TEST
            // ═══════════════════════════════
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n══ VIEWMATRIX W2S TEST ══\n");
            Console.ResetColor();

            long vmPtr = mem.Read<long>(BA + 0x2591ED0);
            if (vmPtr > 0x10000)
            {
                Console.WriteLine($"  VM Ptr: 0x{vmPtr:X}");

                foreach (long vmOff in new long[] { 0x1E0, 0x24C, 0x260, 0x280, 0x2A0, 0x300, 0x340, 0x380 })
                {
                    byte[] tb = new byte[64];
                    ZetaMemory.ReadProcessMemory(mem.ProcessHandle, vmPtr + vmOff, tb, 64, out _);
                    float[] m = new float[16];
                    Buffer.BlockCopy(tb, 0, m, 0, 64);

                    bool hasNaN = false;
                    for (int mi = 0; mi < 16; mi++)
                        if (float.IsNaN(m[mi]) || float.IsInfinity(m[mi])) { hasNaN = true; break; }
                    if (hasNaN) continue;

                    float w = m[3] * lpPos.X + m[7] * lpPos.Y + m[11] * lpPos.Z + m[15];
                    if (MathF.Abs(w) < 0.001f) continue;

                    float inv = 1f / w;
                    float nx = (m[0] * lpPos.X + m[4] * lpPos.Y + m[8] * lpPos.Z + m[12]) * inv;
                    float ny = (m[1] * lpPos.X + m[5] * lpPos.Y + m[9] * lpPos.Z + m[13]) * inv;
                    float sx = 960 + nx * 960;
                    float sy = 540 - ny * 540;

                    string ok = (sx > 0 && sx < 1920 && sy > 0 && sy < 1080) ? "EKRANDA" : "dis";
                    Console.WriteLine($"  +0x{vmOff:X}: S({sx:F0},{sy:F0}) NDC({nx:F3},{ny:F3}) W:{w:F2} [{ok}]");
                }

                // Also try reading VM as direct matrix from base (not through pointer)
                Console.WriteLine("\n  Direct VM (base+ offsets):");
                foreach (long dOff in new long[] { 0x1FCBD0, 0x1F4BD0, 0x1FEBD0, 0x200BD0 })
                {
                    byte[] tb = new byte[64];
                    ZetaMemory.ReadProcessMemory(mem.ProcessHandle, BA + dOff, tb, 64, out _);
                    float[] m = new float[16];
                    Buffer.BlockCopy(tb, 0, m, 0, 64);

                    bool hasNaN = false;
                    for (int mi = 0; mi < 16; mi++)
                        if (float.IsNaN(m[mi]) || float.IsInfinity(m[mi])) { hasNaN = true; break; }
                    if (hasNaN) { Console.WriteLine($"    0x{dOff:X}: NaN"); continue; }

                    float w = m[3] * lpPos.X + m[7] * lpPos.Y + m[11] * lpPos.Z + m[15];
                    if (MathF.Abs(w) < 0.001f) { Console.WriteLine($"    0x{dOff:X}: W=0"); continue; }

                    float inv = 1f / w;
                    float nx = (m[0] * lpPos.X + m[4] * lpPos.Y + m[8] * lpPos.Z + m[12]) * inv;
                    float ny = (m[1] * lpPos.X + m[5] * lpPos.Y + m[9] * lpPos.Z + m[13]) * inv;
                    float sx = 960 + nx * 960;
                    float sy = 540 - ny * 540;

                    string ok = (sx > 0 && sx < 1920 && sy > 0 && sy < 1080) ? "EKRANDA" : "dis";
                    Console.WriteLine($"    0x{dOff:X}: S({sx:F0},{sy:F0}) W:{w:F2} [{ok}]");
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n══════════════════════════");
            Console.WriteLine("  PHASE 3 BITTI!");
            Console.WriteLine("  TUM CIKTIYI KOPYALA!");
            Console.WriteLine("══════════════════════════\n");
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
}
