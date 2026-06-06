namespace KaguyaArcTool.IO;

internal static class PathSafety
{
    public static string GetSafeOutputPath(string outputRoot, string archiveName)
    {
        string relative = archiveName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        while (relative.Length > 0 && (relative[0] == Path.DirectorySeparatorChar || relative[0] == Path.AltDirectorySeparatorChar))
            relative = relative[1..];

        string fullPath = Path.GetFullPath(Path.Combine(outputRoot, relative));
        string rootWithSeparator = outputRoot.EndsWith(Path.DirectorySeparatorChar)
            ? outputRoot
            : outputRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, outputRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"archive entry escapes output directory: {archiveName}");
        }

        return fullPath;
    }
}
