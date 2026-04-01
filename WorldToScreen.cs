using System;

public class WorldToScreen
{
    private readonly ZetaMemory _mem;
    private readonly float[] _matrix = new float[16];
    private bool _matrixValid = false;

    // Pointer-based VM: Base+0x201ED50 -> struct, matrix at +0x24
    private const long VM_PTR_OFFSET = 0x201ED50;
    private const long VM_MATRIX_OFFSET = 0x24;

    public WorldToScreen(ZetaMemory mem)
    {
        _mem = mem;
    }

    public void Update(long baseAddr)
    {
        _matrixValid = false;

        long vmPtr = _mem.Read<long>(baseAddr + VM_PTR_OFFSET);
        if (vmPtr < 0x10000) return;

        byte[] buffer = new byte[64];
        bool success = ZetaMemory.ReadProcessMemory(
            _mem.ProcessHandle, vmPtr + VM_MATRIX_OFFSET,
            buffer, 64, out IntPtr bytesRead);

        if (!success || bytesRead.ToInt64() != 64) return;

        Buffer.BlockCopy(buffer, 0, _matrix, 0, 64);

        // Validate: no NaN/Inf, at least some non-zero values
        bool hasValue = false;
        for (int i = 0; i < 16; i++)
        {
            if (float.IsNaN(_matrix[i]) || float.IsInfinity(_matrix[i])) return;
            if (MathF.Abs(_matrix[i]) > 0.0001f) hasValue = true;
        }

        _matrixValid = hasValue;
    }

    public bool ToScreen(float x, float y, float z, int width, int height, out float sx, out float sy)
    {
        sx = 0; sy = 0;
        if (!_matrixValid) return false;

        // Matrix layout from scan: row 0 = [m0,m1,m2,m3], row 1 = [m4,m5,m6,m7], etc
        // The scan showed W = m[3]*x + m[7]*y + m[11]*z + m[15] → standard projection
        float w = _matrix[3] * x + _matrix[7] * y + _matrix[11] * z + _matrix[15];
        if (w < 0.01f) return false;

        float invW = 1.0f / w;
        float ndcX = (_matrix[0] * x + _matrix[4] * y + _matrix[8] * z + _matrix[12]) * invW;
        float ndcY = (_matrix[1] * x + _matrix[5] * y + _matrix[9] * z + _matrix[13]) * invW;

        sx = (width / 2f) + (ndcX * width / 2f);
        sy = (height / 2f) - (ndcY * height / 2f);

        return sx >= -100 && sx <= width + 100 && sy >= -100 && sy <= height + 100;
    }
}
