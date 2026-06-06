namespace KaguyaArcTool.Arc;

internal static class ArchiveCompressionPolicy
{
    public static bool ShouldCompress(InputFile file)
    {
        return string.Equals(Path.GetExtension(file.ArcName), ".prs", StringComparison.OrdinalIgnoreCase)
            && file.Size > 0
            && file.Size <= int.MaxValue;
    }
}
