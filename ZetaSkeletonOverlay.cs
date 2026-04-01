using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GameOverlay.Drawing;
using GameOverlay.Windows;

public class ZetaSkeletonOverlay : IDisposable
{
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    private readonly GraphicsWindow _window;
    private readonly ZetaMemory _mem;
    private readonly WorldToScreen _w2s;

    private SolidBrush? _greenBrush, _yellowBrush, _redBrush, _whiteBrush, _bgBrush, _orangeBrush;
    private Font? _font, _fontSmall;

    public bool IsEspActive = true;
    public int ToggleKey = 0x74;
    private bool _isKeyPressed = false;
    private int _entityFoundCount = 0;

    // ── Scan'den bulunan OFFSET'LER ──
    private const long WORLD_OFFSET = 0x25B14B0;
    private const long LOCAL_PLAYER_OFFSET = 0x8;

    // Ped list chain: World+0x10 → +0x58 → +0x18 → list at +0x8, max at +0x10
    private const long CHAIN1 = 0x10;
    private const long CHAIN2 = 0x58;
    private const long CHAIN3 = 0x18;
    private const long LIST_OFFSET = 0x8;
    private const long MAX_OFFSET = 0x10;
    private const int PED_STRIDE = 0x10;

    // Entity offsets
    private const long HEALTH_OFFSET = 0x280;

    // Navigation: +0x30 for LP, +0x1B8 and +0x1A8 for other peds
    private static readonly long[] NAV_OFFSETS = { 0x30, 0x1B8, 0x1A8 };
    private const long NAV_POS_OFFSET = 0x60;

    // Direct position fallback offsets (found in scan)
    private static readonly long[] DIRECT_POS_OFFSETS = { 0x90, 0x5C0, 0x640, 0x680 };

    // Skeleton: Ped+0x1B0 → +0x28, stride 0x10
    private const long SKEL_OFFSET = 0x1B0;
    private const long BONE_CACHE_OFFSET = 0x28;
    private const int BONE_STRIDE = 0x10;
    private const int BONE_POS_OFFSET = 0x0; // XYZ starts at beginning of each entry

    public ZetaSkeletonOverlay(ZetaMemory mem)
    {
        _mem = mem;
        _w2s = new WorldToScreen(mem);

        var gfx = new Graphics()
        {
            MeasureFPS = true,
            PerPrimitiveAntiAliasing = true,
            TextAntiAliasing = true
        };

        _window = new StickyWindow(_mem.TargetProcess!.MainWindowHandle, gfx)
        {
            FPS = 60,
            IsTopmost = true,
            IsVisible = true
        };

        _window.SetupGraphics += SetupGraphics;
        _window.DrawGraphics += DrawGraphics;
        _window.DestroyGraphics += DestroyGraphics;
    }

    private void SetupGraphics(object? sender, SetupGraphicsEventArgs e)
    {
        var gfx = e.Graphics;
        _greenBrush = gfx.CreateSolidBrush(0, 255, 70, 255);
        _yellowBrush = gfx.CreateSolidBrush(255, 255, 0, 255);
        _redBrush = gfx.CreateSolidBrush(255, 50, 50, 255);
        _whiteBrush = gfx.CreateSolidBrush(255, 255, 255, 200);
        _orangeBrush = gfx.CreateSolidBrush(255, 165, 0, 255);
        _bgBrush = gfx.CreateSolidBrush(0, 0, 0, 150);
        _font = gfx.CreateFont("Consolas", 16, true);
        _fontSmall = gfx.CreateFont("Consolas", 12);
    }

    private void DestroyGraphics(object? sender, DestroyGraphicsEventArgs e)
    {
        _greenBrush?.Dispose(); _yellowBrush?.Dispose(); _redBrush?.Dispose();
        _whiteBrush?.Dispose(); _orangeBrush?.Dispose(); _bgBrush?.Dispose();
        _font?.Dispose(); _fontSmall?.Dispose();
    }

    private Vector3 GetEntityPosition(long ped)
    {
        // Try navigation offsets first
        foreach (long navOff in NAV_OFFSETS)
        {
            long nav = _mem.Read<long>(ped + navOff);
            if (nav < 0x10000) continue;

            Vector3 pos = _mem.Read<Vector3>(nav + NAV_POS_OFFSET);
            if (!float.IsNaN(pos.X) && MathF.Abs(pos.X) > 10 && MathF.Abs(pos.X) < 10000
                && !float.IsNaN(pos.Y) && MathF.Abs(pos.Y) > 10)
                return pos;
        }

        // Fallback: direct position offsets
        foreach (long pOff in DIRECT_POS_OFFSETS)
        {
            float x = _mem.Read<float>(ped + pOff);
            float y = _mem.Read<float>(ped + pOff + 4);
            float z = _mem.Read<float>(ped + pOff + 8);

            if (!float.IsNaN(x) && MathF.Abs(x) > 10 && MathF.Abs(x) < 10000
                && !float.IsNaN(y) && MathF.Abs(y) > 10)
                return new Vector3 { X = x, Y = y, Z = z };
        }

        return default;
    }

    private void DrawGraphics(object? sender, DrawGraphicsEventArgs e)
    {
        var gfx = e.Graphics;
        gfx.ClearScene();
        HandleToggleKey();

        if (_bgBrush == null || _font == null || _fontSmall == null || _whiteBrush == null) return;

        // HUD Panel
        gfx.FillRectangle(_bgBrush, 10, 10, 340, 80);
        var statusBrush = IsEspActive ? _greenBrush : _redBrush;
        gfx.DrawText(_font, statusBrush, 20, 15, IsEspActive ? "ZETA ESP: AKTIF" : "ZETA ESP: KAPALI");
        gfx.DrawText(_fontSmall, _whiteBrush, 20, 38, $"FPS: {gfx.FPS} | Oyuncu: {_entityFoundCount} | Build: b3095");

        if (!IsEspActive) return;

        _w2s.Update(_mem.BaseAddress);

        long worldPtr = _mem.Read<long>(_mem.BaseAddress + WORLD_OFFSET);
        if (worldPtr == 0) return;

        long localPlayer = _mem.Read<long>(worldPtr + LOCAL_PLAYER_OFFSET);
        if (localPlayer == 0) return;

        // Get local player position
        Vector3 localPos = GetEntityPosition(localPlayer);

        // Show local player info
        float myHp = _mem.Read<float>(localPlayer + HEALTH_OFFSET);
        gfx.DrawText(_fontSmall, _whiteBrush, 20, 55,
            $"HP: {myHp:F0} | Pos: ({localPos.X:F0},{localPos.Y:F0},{localPos.Z:F0})");

        // Follow ped list chain
        long c1 = _mem.Read<long>(worldPtr + CHAIN1);
        if (c1 == 0) return;
        long c2 = _mem.Read<long>(c1 + CHAIN2);
        if (c2 == 0) return;
        long c3 = _mem.Read<long>(c2 + CHAIN3);
        if (c3 == 0) return;

        long pedList = _mem.Read<long>(c3 + LIST_OFFSET);
        int pedMax = _mem.Read<int>(c3 + MAX_OFFSET);
        if (pedList == 0 || pedMax <= 0) return;
        if (pedMax > 256) pedMax = 256;

        int foundCount = 0;

        for (int i = 0; i < pedMax; i++)
        {
            long ped = _mem.Read<long>(pedList + (i * PED_STRIDE));
            if (ped < 0x10000 || ped == localPlayer) continue;

            // Get position
            Vector3 entityPos = GetEntityPosition(ped);
            if (entityPos.X == 0 && entityPos.Y == 0) continue;

            // Distance check
            float dx = entityPos.X - localPos.X;
            float dy = entityPos.Y - localPos.Y;
            float dz = entityPos.Z - localPos.Z;
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

            // Only show entities within 500m
            if (dist > 500 || dist < 1) continue;

            // Try to get skeleton bones
            long skelPtr = _mem.Read<long>(ped + SKEL_OFFSET);
            bool hasSkeleton = false;
            long boneCache = 0;

            if (skelPtr > 0x10000 && skelPtr < 0x7FFFFFFFFFFF)
            {
                boneCache = _mem.Read<long>(skelPtr + BONE_CACHE_OFFSET);
                if (boneCache > 0x10000 && boneCache < 0x7FFFFFFFFFFF)
                {
                    // Quick validate: check if first few bones are reasonable local coords
                    Vector3 testBone = _mem.Read<Vector3>(boneCache);
                    if (!float.IsNaN(testBone.X) && MathF.Abs(testBone.X) < 5)
                        hasSkeleton = true;
                }
            }

            if (hasSkeleton && boneCache != 0)
            {
                // ── SKELETON ESP ──
                // Read key bone positions (local space) and convert to world
                // Bone indices for a basic skeleton
                int[] keyBones = { 0, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14 };
                var screenPoints = new Dictionary<int, (float X, float Y)>();

                foreach (int boneIdx in keyBones)
                {
                    Vector3 localBone = _mem.Read<Vector3>(boneCache + (boneIdx * BONE_STRIDE) + BONE_POS_OFFSET);

                    if (float.IsNaN(localBone.X) || MathF.Abs(localBone.X) > 5) continue;

                    float wx = entityPos.X + localBone.X;
                    float wy = entityPos.Y + localBone.Y;
                    float wz = entityPos.Z + localBone.Z;

                    if (_w2s.ToScreen(wx, wy, wz, gfx.Width, gfx.Height, out float sx, out float sy))
                    {
                        screenPoints[boneIdx] = (sx, sy);
                    }
                }

                // Draw skeleton lines
                var bonePairs = new (int, int)[] {
                    (0, 3), (3, 4), (4, 5),   // spine
                    (5, 6), (6, 7),             // left arm
                    (5, 8), (8, 10),            // right arm
                    (0, 11), (11, 12),          // left leg
                    (0, 13), (13, 14)           // right leg
                };

                int drawnLines = 0;
                foreach (var (a, b) in bonePairs)
                {
                    if (screenPoints.TryGetValue(a, out var pa) && screenPoints.TryGetValue(b, out var pb))
                    {
                        gfx.DrawLine(_greenBrush, pa.X, pa.Y, pb.X, pb.Y, 2);
                        drawnLines++;
                    }
                }

                // Draw bone points
                foreach (var kv in screenPoints)
                {
                    gfx.FillCircle(_yellowBrush, kv.Value.X, kv.Value.Y, 3);
                }

                // Head circle
                if (screenPoints.TryGetValue(5, out var head))
                {
                    gfx.DrawCircle(_yellowBrush, head.X, head.Y, 12, 2);
                }

                // Info text near skeleton
                if (screenPoints.TryGetValue(0, out var root))
                {
                    gfx.DrawText(_fontSmall, _whiteBrush, root.X - 20, root.Y + 10, $"{dist:F0}m");
                }

                foundCount++;
            }
            else
            {
                // ── BOX ESP (fallback, skeleton yoksa) ──
                if (_w2s.ToScreen(entityPos.X, entityPos.Y, entityPos.Z, gfx.Width, gfx.Height, out float sx, out float sy))
                {
                    // Scale box size by distance
                    float boxH = Math.Clamp(2000 / dist, 20, 200);
                    float boxW = boxH * 0.4f;

                    float left = sx - boxW / 2;
                    float top = sy - boxH;
                    float right = sx + boxW / 2;
                    float bottom = sy;

                    // Box
                    SolidBrush? boxColor = dist < 50 ? _redBrush : (dist < 150 ? _orangeBrush : _greenBrush);
                    gfx.DrawRectangle(boxColor, left, top, right, bottom, 2);

                    // Snap line (from bottom center to entity)
                    gfx.DrawLine(_greenBrush, gfx.Width / 2f, (float)gfx.Height, sx, sy, 1);

                    // Distance text
                    gfx.DrawText(_fontSmall, _whiteBrush, left, bottom + 5, $"{dist:F0}m");

                    foundCount++;
                }
            }
        }

        _entityFoundCount = foundCount;
    }

    private void HandleToggleKey()
    {
        short keyState = GetAsyncKeyState(ToggleKey);
        if ((keyState & 0x8000) > 0)
        {
            if (!_isKeyPressed) { IsEspActive = !IsEspActive; _isKeyPressed = true; }
        }
        else _isKeyPressed = false;
    }

    public void Run() => _window.Create();
    public void Dispose() => _window?.Dispose();
}
