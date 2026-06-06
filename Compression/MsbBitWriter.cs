namespace KaguyaArcTool.Compression;

internal sealed class MsbBitWriter
{
    private readonly MemoryStream _output = new();
    private int _mask = 0x80;
    private byte _current;

    public void WriteBit(int bit)
    {
        if ((bit & 1) != 0)
            _current |= (byte)_mask;

        _mask >>= 1;
        if (_mask == 0)
            FlushByte();
    }

    public void WriteBits(int value, int count)
    {
        for (int bit = count - 1; bit >= 0; bit--)
            WriteBit((value >> bit) & 1);
    }

    public byte[] ToArray()
    {
        if (_mask != 0x80)
            FlushByte();
        return _output.ToArray();
    }

    private void FlushByte()
    {
        _output.WriteByte(_current);
        _current = 0;
        _mask = 0x80;
    }
}
