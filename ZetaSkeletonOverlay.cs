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

    private SolidBrush? _greenBrush;
    private SolidBrush? _yellowBrush;
    private SolidBrush? _redBrush;
    private SolidBrush? _whiteBrush;
    private SolidBrush? _bgBrush;
    private Font? _font;
    private Font? _fontSmall;

    public bool IsEspActive = true;
    public int ToggleKey = 0x74;
    private bool _isKeyPressed = false;
    private int _entityFoundCount = 0;

    private const long WORLD_OFFSET = 0x252D738;
    private const long LOCAL_PLAYER_OFFSET = 0x8;
    private const long REPLAY_INTERFACE_OFFSET = 0x18;
    private const long PED_INTERFACE_OFFSET = 0x18;
    private const long PED_LIST_OFFSET = 0x100;
    private const long PED_MAX_OFFSET = 0x108;
    private const long HEALTH_OFFSET = 0x280;
    private const long PED_TYPE_OFFSET = 0x10B8;
    private const long SKELETON_OFFSET = 0x430;
    private const long BONE_CACHE_OFFSET = 0x18;
    private const long NAVIGATION_OFFSET = 0x90;
    private const long NAV_POSITION_OFFSET = 0x50;

    private const int BONE_STRIDE = 0x20;
    private const int BONE_POS_OFFSET = 0x10;

    private static readonly int[] BoneIndices = {
        0, 5, 6, 7, 8, 21, 22, 23, 24,
        40, 41, 42, 43, 58, 59, 60, 63, 64, 65
    };

    private static readonly (int Start, int End)[] BonePairs = {
        (0, 1), (1, 2), (2, 3), (3, 4),
        (2, 5), (5, 6), (6, 7), (7, 8),
        (2, 9), (9, 10), (10, 11), (11, 12),
        (0, 13), (13, 14), (14, 15),
        (0, 16), (16, 17), (17, 18)
    };

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
        _bgBrush = gfx.CreateSolidBrush(0, 0, 0, 150);
        _font = gfx.CreateFont("Consolas", 16, true);
        _fontSmall = gfx.CreateFont("Consolas", 12);
    }

    private void DestroyGraphics(object? sender, DestroyGraphicsEventArgs e)
    {
        _greenBrush?.Dispose();
        _yellowBrush?.Dispose();
        _redBrush?.Dispose();
        _whiteBrush?.Dispose();
        _bgBrush?.Dispose();
        _font?.Dispose();
        _fontSmall?.Dispose();
    }

    private void DrawGraphics(object? sender, DrawGraphicsEventArgs e)
    {
        var gfx = e.Graphics;
        gfx.ClearScene();
        HandleToggleKey();

        if (_bgBrush == null || _font == null || _fontSmall == null || _whiteBrush == null) return;

        gfx.FillRectangle(_bgBrush, 10, 10, 320, 75);
        var statusBrush = IsEspActive ? _greenBrush : _redBrush;
        gfx.DrawText(_font, statusBrush, 20, 15, IsEspActive ? "ZETA ESP: AKTIF" : "ZETA ESP: KAPALI");
        gfx.DrawText(_fontSmall, _whiteBrush, 20, 38, $"FPS: {gfx.FPS} | Oyuncu: {_entityFoundCount} | b3095");

        if (!IsEspActive) return;

        _w2s.Update(_mem.BaseAddress);

        long worldPtr = _mem.Read<long>(_mem.BaseAddress + WORLD_OFFSET);
        if (worldPtr == 0) return;

        long localPlayer = _mem.Read<long>(worldPtr + LOCAL_PLAYER_OFFSET);
        long replayInterface = _mem.Read<long>(worldPtr + REPLAY_INTERFACE_OFFSET);
        if (replayInterface == 0) return;

        long pedInterface = _mem.Read<long>(replayInterface + PED_INTERFACE_OFFSET);
        if (pedInterface == 0) return;

        long pedList = _mem.Read<long>(pedInterface + PED_LIST_OFFSET);
        int pedMaxCount = _mem.Read<int>(pedInterface + PED_MAX_OFFSET);
        if (pedList == 0 || pedMaxCount <= 0) return;
        if (pedMaxCount > 256) pedMaxCount = 256;

        int foundCount = 0;

        long localNavPtr = _mem.Read<long>(localPlayer + NAVIGATION_OFFSET);
        Vector3 localPos = _mem.Read<Vector3>(localNavPtr + NAV_POSITION_OFFSET);

        for (int i = 0; i < pedMaxCount; i++)
        {
            long pedPtr = _mem.Read<long>(pedList + (i * 0x10));
            if (pedPtr == 0 || pedPtr == localPlayer) continue;

            int pedType = _mem.Read<int>(pedPtr + PED_TYPE_OFFSET);
            if ((pedType & 0xFF) != 2) continue;

            float health = _mem.Read<float>(pedPtr + HEALTH_OFFSET);
            if (health <= 0 || health > 10000) continue;

            long navPtr = _mem.Read<long>(pedPtr + NAVIGATION_OFFSET);
            if (navPtr == 0) continue;
            Vector3 entityPos = _mem.Read<Vector3>(navPtr + NAV_POSITION_OFFSET);
            if (entityPos.X == 0 && entityPos.Y == 0 && entityPos.Z == 0) continue;

            long skeletonPtr = _mem.Read<long>(pedPtr + SKELETON_OFFSET);
            if (skeletonPtr == 0) continue;

            long boneCachePtr = _mem.Read<long>(skeletonPtr + BONE_CACHE_OFFSET);
            if (boneCachePtr == 0) continue;

            var screenBones = new Dictionary<int, (float X, float Y)>();
            bool allValid = true;

            for (int b = 0; b < BoneIndices.Length; b++)
            {
                int boneId = BoneIndices[b];
                long boneAddr = boneCachePtr + (boneId * BONE_STRIDE);
                Vector3 localBone = _mem.Read<Vector3>(boneAddr + BONE_POS_OFFSET);

                float worldX = entityPos.X + localBone.X;
                float worldY = entityPos.Y + localBone.Y;
                float worldZ = entityPos.Z + localBone.Z;

                if (_w2s.ToScreen(worldX, worldY, worldZ, gfx.Width, gfx.Height, out float sx, out float sy))
                {
                    screenBones[b] = (sx, sy);
                }
                else
                {
                    allValid = false;
                    break;
                }
            }

            if (!allValid || screenBones.Count != BoneIndices.Length) continue;

            foreach (var (start, end) in BonePairs)
            {
                if (screenBones.TryGetValue(start, out var s) && screenBones.TryGetValue(end, out var en))
                    gfx.DrawLine(_greenBrush, s.X, s.Y, en.X, en.Y, 2);
            }

            if (screenBones.TryGetValue(4, out var head))
                gfx.DrawCircle(_yellowBrush, head.X, head.Y, 10, 2);

            if (screenBones.TryGetValue(0, out var pelvis))
            {
                float barW = 40, barH = 4;
                float barX = pelvis.X - barW / 2;
                float barY = pelvis.Y + 15;
                float hpRatio = Math.Clamp(health / 200f, 0, 1);

                gfx.FillRectangle(_bgBrush, barX, barY, barX + barW, barY + barH);
                var hpBrush = hpRatio > 0.5f ? _greenBrush : (hpRatio > 0.25f ? _yellowBrush : _redBrush);
                gfx.FillRectangle(hpBrush, barX, barY, barX + (barW * hpRatio), barY + barH);

                float dx = entityPos.X - localPos.X;
                float dy = entityPos.Y - localPos.Y;
                float dz = entityPos.Z - localPos.Z;
                float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

                gfx.DrawText(_fontSmall, _whiteBrush, barX - 10, barY + 8, $"{(int)health} HP | {dist:F0}m");
            }

            foundCount++;
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
