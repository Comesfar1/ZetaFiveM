using System;

public class WorldToScreen
{
    private readonly ZetaMemory _mem;
    private readonly float[] _matrix = new float[16];
    private bool _matrixValid = false;

    private const long VIEWMATRIX_OFFSET = 0x1FCBD0;

    public WorldToScreen(ZetaMemory mem)
    {
        _mem = mem;
    }

    public void Update(long baseAddr)
    {
        byte[] buffer = new byte[64];
        bool success = ZetaMemory.ReadProcessMemory(
            _mem.ProcessHandle, baseAddr + VIEWMATRIX_OFFSET,
            buffer, 64, out IntPtr bytesRead);

        if (success && bytesRead.ToInt64() == 64)
        {
            Buffer.BlockCopy(buffer, 0, _matrix, 0, 64);
            _matrixValid = false;
            for (int i = 0; i < 16; i++)
            {
                if (Math.Abs(_matrix[i]) > 0.0001f)
                {
                    _matrixValid = true;
                    break;
                }
            }
        }
        else
        {
            _matrixValid = false;
        }
    }

    public bool ToScreen(float x, float y, float z, int width, int height, out float sx, out float sy)
    {
        sx = 0; sy = 0;
        if (!_matrixValid) return false;

        float w = _matrix[3] * x + _matrix[7] * y + _matrix[11] * z + _matrix[15];
        if (w < 0.01f) return false;

        float invW = 1.0f / w;
        float ndcX = (_matrix[0] * x + _matrix[4] * y + _matrix[8] * z + _matrix[12]) * invW;
        float ndcY = (_matrix[1] * x + _matrix[5] * y + _matrix[9] * z + _matrix[13]) * invW;

        sx = (width / 2f) + (ndcX * width / 2f);
        sy = (height / 2f) - (ndcY * height / 2f);

        return sx >= 0 && sx <= width && sy >= 0 && sy <= height;
    }
}
