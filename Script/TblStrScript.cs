namespace KaguyaArcTool.Script;

internal static class TblStrScript
{
    private const int MessageType = 0;
    private const int NameType = 2;
    private const int ChoiceType = 3;

    public static List<ScriptLine> Export(TblStrTable table)
    {
        if (table.LogicalIndexes is not null)
            return ExportAri(table);

        return ExportPhysical(table);
    }

    private static List<ScriptLine> ExportPhysical(TblStrTable table)
    {
        List<ScriptLine> lines = new(table.Entries.Count);

        for (int i = 0; i < table.Entries.Count; i++)
        {
            TblStrEntry entry = table.Entries[i];
            if (entry.Type == NameType
                && i + 1 < table.Entries.Count
                && table.Entries[i + 1].Type == MessageType
                && table.Entries[i + 1].IsIndexed)
            {
                lines.Add(new ScriptLine
                {
                    Name = entry.Text,
                    Message = table.Entries[i + 1].Text
                });
                i++;
                continue;
            }

            if (!entry.IsIndexed)
                continue;

            lines.Add(entry.Type switch
            {
                MessageType => new ScriptLine { Message = entry.Text },
                ChoiceType => new ScriptLine { Choice = entry.Text },
                _ => throw new InvalidDataException($"unsupported TBLSTR entry type {entry.Type} at index {i}")
            });
        }

        return lines;
    }

    public static TblStrTable Import(TblStrTable baseTable, IReadOnlyList<ScriptLine> lines, int wrapWidth)
    {
        if (baseTable.LogicalIndexes is not null)
            return ImportAri(baseTable, lines, wrapWidth);

        return ImportPhysical(baseTable, lines, wrapWidth);
    }

    private static List<ScriptLine> ExportAri(TblStrTable table)
    {
        List<TblStrEntry> entries = table.Entries;
        IReadOnlyList<int> logicalIndexes = table.LogicalIndexes!;
        List<ScriptLine> lines = new(entries.Count);
        int logicalCursor = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            TblStrEntry entry = entries[i];
            if (entry.Type == MessageType)
            {
                if (logicalCursor >= logicalIndexes.Count)
                    continue;

                int messageIndex = logicalIndexes[logicalCursor++];
                TblStrEntry messageEntry = entries[messageIndex];
                string? name = FindAriName(entries, i, messageIndex);
                lines.Add(name is null
                    ? new ScriptLine { Message = messageEntry.Text }
                    : new ScriptLine { Name = name, Message = messageEntry.Text });
                continue;
            }

            if (entry.Type == ChoiceType)
                lines.Add(new ScriptLine { Choice = entry.Text });
        }

        return lines;
    }

    private static TblStrTable ImportPhysical(TblStrTable baseTable, IReadOnlyList<ScriptLine> lines, int wrapWidth)
    {
        List<TblStrEntry> entries = new(baseTable.Entries.Count);
        int lineIndex = 0;

        for (int i = 0; i < baseTable.Entries.Count; i++)
        {
            TblStrEntry entry = baseTable.Entries[i];

            if (entry.Type == NameType
                && i + 1 < baseTable.Entries.Count
                && baseTable.Entries[i + 1].Type == MessageType
                && baseTable.Entries[i + 1].IsIndexed)
            {
                if (lineIndex >= lines.Count)
                    throw new InvalidDataException($"script JSON ended before base entry {i}");

                ScriptLine line = lines[lineIndex];
                ValidateLine(line, lineIndex, allowName: true, allowMessage: true, allowChoice: false);
                entries.Add(entry with { Text = line.Name ?? entry.Text });
                entries.Add(baseTable.Entries[i + 1] with { Text = WrapMessage(line.Message ?? string.Empty, wrapWidth) });
                i++;
                lineIndex++;
                continue;
            }

            if (!entry.IsIndexed)
            {
                entries.Add(entry);
                continue;
            }

            if (lineIndex >= lines.Count)
                throw new InvalidDataException($"script JSON ended before base entry {i}");

            ScriptLine currentLine = lines[lineIndex];
            switch (entry.Type)
            {
                case MessageType:
                    ValidateLine(currentLine, lineIndex, allowName: false, allowMessage: true, allowChoice: false);
                    entries.Add(entry with { Text = WrapMessage(currentLine.Message ?? string.Empty, wrapWidth) });
                    break;
                case ChoiceType:
                    ValidateLine(currentLine, lineIndex, allowName: false, allowMessage: false, allowChoice: true);
                    entries.Add(entry with { Text = WrapMessage(currentLine.Choice ?? string.Empty, wrapWidth) });
                    break;
                default:
                    throw new InvalidDataException($"unsupported TBLSTR entry type {entry.Type} at index {i}");
            }

            lineIndex++;
        }

        if (lineIndex != lines.Count)
            throw new InvalidDataException($"script JSON contains {lines.Count - lineIndex} extra entries");

        return baseTable.WithEntries(entries);
    }

    private static TblStrTable ImportAri(TblStrTable baseTable, IReadOnlyList<ScriptLine> lines, int wrapWidth)
    {
        List<TblStrEntry> baseEntries = baseTable.Entries;
        List<TblStrEntry> entries = baseEntries.ToList();
        IReadOnlyList<int> logicalIndexes = baseTable.LogicalIndexes!;
        int logicalCursor = 0;
        int lineIndex = 0;

        for (int i = 0; i < baseEntries.Count; i++)
        {
            TblStrEntry entry = baseEntries[i];
            if (entry.Type == MessageType)
            {
                if (logicalCursor >= logicalIndexes.Count)
                    continue;

                if (lineIndex >= lines.Count)
                    throw new InvalidDataException($"script JSON ended before logical TBLSTR entry {logicalCursor}");

                int messageIndex = logicalIndexes[logicalCursor++];
                ScriptLine line = lines[lineIndex++];
                int nameIndex = FindAriNameIndex(baseEntries, i, messageIndex);
                ValidateLine(line, lineIndex - 1, allowName: nameIndex >= 0, allowMessage: true, allowChoice: false);
                if (nameIndex >= 0 && line.Name is not null)
                    entries[nameIndex] = entries[nameIndex] with { Text = line.Name };

                entries[messageIndex] = entries[messageIndex] with { Text = WrapMessage(line.Message ?? string.Empty, wrapWidth) };
                continue;
            }

            if (entry.Type == ChoiceType)
            {
                if (lineIndex >= lines.Count)
                    throw new InvalidDataException($"script JSON ended before TBLSTR choice entry {i}");

                ScriptLine line = lines[lineIndex++];
                ValidateLine(line, lineIndex - 1, allowName: false, allowMessage: false, allowChoice: true);
                entries[i] = entry with { Text = WrapMessage(line.Choice ?? string.Empty, wrapWidth) };
            }
        }

        if (lineIndex != lines.Count)
            throw new InvalidDataException($"script JSON contains {lines.Count - lineIndex} extra entries");

        return baseTable.WithEntries(entries);
    }

    private static string? FindAriName(IReadOnlyList<TblStrEntry> entries, int physicalMessageIndex, int logicalMessageIndex)
    {
        int nameIndex = FindAriNameIndex(entries, physicalMessageIndex, logicalMessageIndex);
        return nameIndex >= 0 ? entries[nameIndex].Text : null;
    }

    private static int FindAriNameIndex(IReadOnlyList<TblStrEntry> entries, int physicalMessageIndex, int logicalMessageIndex)
    {
        if (physicalMessageIndex > 0 && entries[physicalMessageIndex - 1].Type == NameType)
            return physicalMessageIndex - 1;

        if (logicalMessageIndex > 0 && entries[logicalMessageIndex - 1].Type == NameType)
            return logicalMessageIndex - 1;

        return -1;
    }

    private static string WrapMessage(string text, int wrapWidth)
    {
        return wrapWidth > 0 ? TextWrapper.Wrap(text, wrapWidth) : text;
    }

    private static void ValidateLine(ScriptLine line, int lineIndex, bool allowName, bool allowMessage, bool allowChoice)
    {
        if (!allowName && line.Name is not null)
            throw new InvalidDataException($"unexpected name at JSON entry {lineIndex}");

        if (!allowMessage && line.Message is not null)
            throw new InvalidDataException($"unexpected message at JSON entry {lineIndex}");

        if (!allowChoice && line.Choice is not null)
            throw new InvalidDataException($"unexpected choice at JSON entry {lineIndex}");

        if (allowMessage && line.Message is null)
            throw new InvalidDataException($"missing message at JSON entry {lineIndex}");

        if (allowChoice && line.Choice is null)
            throw new InvalidDataException($"missing choice at JSON entry {lineIndex}");
    }
}
