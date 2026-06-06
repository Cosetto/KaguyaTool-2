namespace KaguyaArcTool.Compression;

internal sealed class MsbBitReader
{
    private readonly byte[] _input;
    private int _position;
    private int _mask;
    private int _current;

    public MsbBitReader(byte[] input)
    {
        _input = input;
    }

    public int GetBit()
    {
        if (_mask == 0)
        {
            if (_position >= _input.Length)
                throw new EndOfStreamException("compressed stream ended early");

            _current = _input[_position++];
            _mask = 0x80;
        }

        int bit = (_current & _mask) != 0 ? 1 : 0;
        _mask >>= 1;
        return bit;
    }

    public int GetBits(int count)
    {
        int value = 0;
        for (int i = 0; i < count; i++)
            value = (value << 1) | GetBit();
        return value;
    }
}
