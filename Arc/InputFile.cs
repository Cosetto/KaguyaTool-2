namespace KaguyaArcTool.Arc;

internal sealed record InputFile(string FilePath, string ArcName, uint Size)
{
    public static InputFile FromPath(string root, string path)
    {
        FileInfo info = new(path);
        if (info.Length > uint.MaxValue)
            throw new InvalidOperationException($"file is too large for ARC entry: {path}");

        string relative = Path.GetRelativePath(root, path)
            .Replace(Path.DirectorySeparatorChar, '\\')
            .Replace(Path.AltDirectorySeparatorChar, '\\');

        if (relative.Length == 0 || relative.StartsWith(@"..\", StringComparison.Ordinal) || relative.Contains(@"\..\", StringComparison.Ordinal))
            throw new InvalidOperationException($"invalid relative path: {path}");

        return new InputFile(path, relative, checked((uint)info.Length));
    }
}
