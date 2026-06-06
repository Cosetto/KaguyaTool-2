using System.Text.Json;
using KaguyaArcTool.Image;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace KaguyaArcTool.Commands;

internal static class ImageCommands
{
    private static readonly ImageJsonContext JsonContext = new(new JsonSerializerOptions { WriteIndented = true });

    public static int Decode(string sourceDirectory, string pngDirectory)
    {
        string sourceRoot = Path.GetFullPath(sourceDirectory);
        string pngRoot = Path.GetFullPath(pngDirectory);
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException(sourceRoot);

        Directory.CreateDirectory(pngRoot);
        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            if (IsGeneratedFile(sourcePath))
                continue;

            byte[] data = File.ReadAllBytes(sourcePath);
            if (!TryDecode(data, Path.GetFileName(sourcePath), out Image<Rgba32>? image, out ImageMetadata? metadata))
                continue;

            using (image)
            {
                string relative = Path.GetRelativePath(sourceRoot, sourcePath);
                string pngPath = Path.Combine(pngRoot, Path.ChangeExtension(relative, ".png"));
                string metadataPath = GetMetadataPath(pngPath);
                CreateParent(pngPath);
                image.SaveAsPng(pngPath);
                WriteMetadata(metadataPath, metadata!);
            }
        }

        Console.WriteLine("Decode success!");
        return 0;
    }

    public static int Encode(string pngDirectory, string outputDirectory)
    {
        string pngRoot = Path.GetFullPath(pngDirectory);
        string outputRoot = Path.GetFullPath(outputDirectory);
        if (!Directory.Exists(pngRoot))
            throw new DirectoryNotFoundException(pngRoot);

        Directory.CreateDirectory(outputRoot);
        foreach (string pngPath in Directory.EnumerateFiles(pngRoot, "*.png", SearchOption.AllDirectories))
        {
            string relativePng = Path.GetRelativePath(pngRoot, pngPath);
            string metadataPath = GetMetadataPath(pngPath);

            ImageMetadata metadata;
            bool hasMetadata = File.Exists(metadataPath);
            if (hasMetadata)
            {
                metadata = ReadMetadata(metadataPath);
            }
            else
            {
                Console.WriteLine("Warning: no metadata found, using default png's settings");
                metadata = CreateDefaultMetadata(Path.ChangeExtension(relativePng, ".ap"));
            }

            using Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(pngPath);
            if (hasMetadata && image.Width > metadata.Width)
                Console.WriteLine("Warning: Image width expanded");
            if (hasMetadata && image.Height > metadata.Height)
                Console.WriteLine("Warning: Image height expanded");

            metadata.Width = image.Width;
            metadata.Height = image.Height;

            string outputRelative = ResolveOutputRelative(relativePng, metadata);
            string outputPath = Path.Combine(outputRoot, outputRelative);
            CreateParent(outputPath);

            byte[] apData = ApImage.Encode(image, metadata.Bpp);
            byte[] encoded = metadata.Format.Equals("APS3", StringComparison.OrdinalIgnoreCase)
                ? Aps3Image.Encode(apData, metadata, outputPath)
                : apData;
            File.WriteAllBytes(outputPath, encoded);
        }

        Console.WriteLine("Encode success!");
        return 0;
    }

    private static bool TryDecode(byte[] data, string originalName, out Image<Rgba32>? image, out ImageMetadata? metadata)
    {
        image = null;
        metadata = null;

        if (Aps3Image.IsAps3(data))
        {
            Aps3ReadResult result = Aps3Image.Decode(data, originalName);
            metadata = result.Metadata;
            image = ApImage.Decode(result.ApData, metadata);
            return true;
        }

        if (ApImage.IsAp(data))
        {
            metadata = ApImage.ReadMetadata(data, originalName);
            image = ApImage.Decode(data, metadata);
            return true;
        }

        return false;
    }

    private static ImageMetadata CreateDefaultMetadata(string originalName)
    {
        return new ImageMetadata
        {
            Format = "AP",
            OriginalName = originalName,
            Bpp = 24
        };
    }

    private static string ResolveOutputRelative(string relativePng, ImageMetadata metadata)
    {
        string extension = Path.GetExtension(metadata.OriginalName);
        if (string.IsNullOrEmpty(extension))
            extension = metadata.Format.Equals("APS3", StringComparison.OrdinalIgnoreCase) ? ".ap3" : ".ap";

        return Path.ChangeExtension(relativePng, extension);
    }

    private static string GetMetadataPath(string pngPath)
    {
        string? parent = Path.GetDirectoryName(pngPath);
        string name = Path.GetFileNameWithoutExtension(pngPath) + ".metadata.json";
        return string.IsNullOrEmpty(parent) ? name : Path.Combine(parent, name);
    }

    private static void WriteMetadata(string path, ImageMetadata metadata)
    {
        CreateParent(path);
        string json = JsonSerializer.Serialize(metadata, JsonContext.ImageMetadata);
        File.WriteAllText(path, json);
    }

    private static ImageMetadata ReadMetadata(string path)
    {
        using FileStream input = File.OpenRead(path);
        ImageMetadata? metadata = JsonSerializer.Deserialize(input, JsonContext.ImageMetadata);
        return metadata ?? throw new InvalidDataException($"empty image metadata: {path}");
    }

    private static bool IsGeneratedFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static void CreateParent(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
    }
}
