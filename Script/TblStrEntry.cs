namespace KaguyaArcTool.Script;

internal sealed record TblStrEntry(
    int Type,
    string Text,
    bool IsIndexed = true,
    int StoredSize = -1,
    string? OriginalText = null);
