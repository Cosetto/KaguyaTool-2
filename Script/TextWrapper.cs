using System.Text;

namespace KaguyaArcTool.Script;

internal static class TextWrapper
{
    public static string Wrap(string text, int width)
    {
        if (width <= 0 || text.Length == 0)
            return text;

        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        StringBuilder output = new(text.Length + text.Length / Math.Max(width, 1));

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                output.Append('\n');

            WrapSingleLine(lines[i], width, output);
        }

        return output.ToString();
    }

    private static void WrapSingleLine(string line, int width, StringBuilder output)
    {
        int start = 0;
        while (start < line.Length)
        {
            int end = start;
            int lineWidth = 0;
            int lastSpace = -1;

            while (end < line.Length)
            {
                int charWidth = GetCharWidth(line[end]);
                if (lineWidth > 0 && lineWidth + charWidth > width)
                    break;

                if (line[end] is ' ' or '\t')
                    lastSpace = end;

                lineWidth += charWidth;
                end++;
            }

            if (end == line.Length)
            {
                output.Append(line, start, end - start);
                return;
            }

            if (lastSpace > start)
            {
                output.Append(line, start, lastSpace - start);
                output.Append('\n');
                start = lastSpace + 1;
            }
            else
            {
                output.Append(line, start, end - start);
                output.Append('\n');
                start = end;
            }

            while (start < line.Length && line[start] is ' ' or '\t')
                start++;
        }
    }

    private static int GetCharWidth(char c)
    {
        if (c <= 0x7F || (c >= 0xFF61 && c <= 0xFF9F))
            return 1;

        return 2;
    }
}
