namespace KaguyaArcTool.Compression;

internal static class KaguyaLz
{
    private const int FrameSize = 0x1000;
    private const int FrameMask = FrameSize - 1;
    private const int MaxMatch = 17;
    private const int MinMatch = 2;
    private const int MaxCandidates = 256;

    public static byte[] Unpack(byte[] input, int unpackedSize)
    {
        byte[] output = new byte[unpackedSize];
        byte[] frame = new byte[FrameSize];
        int framePosition = 1;
        int dst = 0;
        MsbBitReader bits = new(input);

        while (dst < output.Length)
        {
            if (bits.GetBit() != 0)
            {
                byte value = (byte)bits.GetBits(8);
                output[dst++] = value;
                frame[framePosition++ & FrameMask] = value;
            }
            else
            {
                int offset = bits.GetBits(12);
                int count = bits.GetBits(4) + 2;
                for (int i = 0; i < count && dst < output.Length; i++)
                {
                    byte value = frame[(offset + i) & FrameMask];
                    output[dst++] = value;
                    frame[framePosition++ & FrameMask] = value;
                }
            }
        }

        return output;
    }

    public static byte[] Pack(byte[] input)
    {
        if (input.Length == 0)
            return [];

        MsbBitWriter writer = new();
        int[] heads = new int[1 << 16];
        int[] previous = new int[input.Length];
        Array.Fill(heads, -1);
        Array.Fill(previous, -1);

        int position = 0;
        while (position < input.Length)
        {
            Match match = FindLongestMatch(input, position, heads, previous);
            if (match.Length >= MinMatch)
            {
                writer.WriteBit(0);
                writer.WriteBits(match.Offset, 12);
                writer.WriteBits(match.Length - 2, 4);

                int end = position + match.Length;
                while (position < end)
                    InsertPosition(input, position++, heads, previous);
            }
            else
            {
                writer.WriteBit(1);
                writer.WriteBits(input[position], 8);
                InsertPosition(input, position++, heads, previous);
            }
        }

        writer.WriteBit(0);
        writer.WriteBits(0, 12);
        return writer.ToArray();
    }

    private static Match FindLongestMatch(byte[] input, int position, int[] heads, int[] previous)
    {
        if (position + 1 >= input.Length)
            return default;

        int bestLength = 0;
        int bestOffset = 0;
        int minCandidate = Math.Max(0, position - FrameSize);
        int maxLength = Math.Min(MaxMatch, input.Length - position);
        int candidates = 0;

        for (int candidate = heads[Hash(input, position)];
             candidate >= minCandidate && candidate >= 0 && candidates < MaxCandidates;
             candidate = previous[candidate], candidates++)
        {
            int encodedOffset = (candidate + 1) & FrameMask;
            if (encodedOffset == 0)
                continue;

            int length = 0;
            while (length < maxLength && input[position + length] == input[candidate + length])
                length++;

            if (length > bestLength)
            {
                bestLength = length;
                bestOffset = encodedOffset;
                if (bestLength == maxLength)
                    break;
            }
        }

        return bestLength >= MinMatch ? new Match(bestOffset, bestLength) : default;
    }

    private static void InsertPosition(byte[] input, int position, int[] heads, int[] previous)
    {
        if (position + 1 >= input.Length)
            return;

        int hash = Hash(input, position);
        previous[position] = heads[hash];
        heads[hash] = position;
    }

    private static int Hash(byte[] input, int position)
    {
        return input[position] << 8 | input[position + 1];
    }

    private readonly record struct Match(int Offset, int Length);
}
