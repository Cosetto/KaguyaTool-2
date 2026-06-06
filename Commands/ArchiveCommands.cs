using KaguyaArcTool.Arc;
using KaguyaArcTool.Compression;
using KaguyaArcTool.IO;

namespace KaguyaArcTool.Commands;

internal static class ArchiveCommands
{
    public static int Info(string archivePath)
    {
        using FileStream archive = File.OpenRead(archivePath);
        ArcArchive arc = ArcArchive.Read(archive, archivePath);
        ArchiveInfoPrinter.Print(arc, archive.Length);
        return 0;
    }

    public static int Unpack(string archivePath, string outputDirectory)
    {
        using FileStream archive = File.OpenRead(archivePath);
        ArcArchive arc = ArcArchive.Read(archive, archivePath);
        Console.WriteLine(arc.Kind.Signature());

        string outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);

        byte[] buffer = new byte[1024 * 1024];
        foreach (ArcEntry entry in arc.Entries)
        {
            Console.WriteLine(entry.Name);
            string outputPath = PathSafety.GetSafeOutputPath(outputRoot, entry.Name);
            string? parent = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            archive.Position = entry.Offset;
            using FileStream output = File.Create(outputPath);
            if (entry.IsPacked)
            {
                byte[] packed = BinaryStream.ReadExactBytes(archive, checked((int)entry.StoredSize));
                byte[] unpacked = KaguyaLz.Unpack(packed, checked((int)entry.UnpackedSize));
                output.Write(unpacked);
            }
            else
            {
                BinaryStream.CopyExactly(archive, output, entry.StoredSize, buffer);
            }
        }

        return 0;
    }

    public static int Pack(string inputDirectory, string archivePath, ArcKind kind)
    {
        string inputRoot = Path.GetFullPath(inputDirectory);
        if (!Directory.Exists(inputRoot))
            throw new DirectoryNotFoundException(inputRoot);

        List<InputFile> files = Directory.EnumerateFiles(inputRoot, "*", SearchOption.AllDirectories)
            .Select(path => InputFile.FromPath(inputRoot, path))
            .OrderBy(file => file.ArcName, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
            throw new InvalidOperationException("input directory does not contain files");

        string? parent = Path.GetDirectoryName(Path.GetFullPath(archivePath));
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        Console.WriteLine(kind.Signature());
        foreach (InputFile file in files)
            Console.WriteLine(file.ArcName);

        using FileStream output = File.Create(archivePath);
        ArcArchive.Write(output, files, kind, archivePath);

        return 0;
    }
}
