using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using KaguyaArcTool.Script;

namespace KaguyaArcTool.Commands;

internal static class ScriptCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    private static readonly ScriptJsonContext JsonContext = new(JsonOptions);

    public static int Export(string tblPath, string outputDirectory)
    {
        List<(string Source, string Relative)> files = CollectInputFiles(tblPath);
        string outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);

        foreach ((string source, string relative) in files)
        {
            TblStrTable table = TblStrTable.Load(source);
            List<ScriptLine> lines = TblStrScript.Export(table);

            string outputPath = Path.Combine(outputRoot, Path.ChangeExtension(relative, ".json"));
            string? parent = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            string json = JsonSerializer.Serialize(lines, JsonContext.ListScriptLine).Replace("\\u3000", "　", StringComparison.Ordinal);
            File.WriteAllText(outputPath, json);
        }

        Console.WriteLine("Export success!");
        return 0;
    }

    public static int Import(string tblPath, string scriptPath, string outputPath, int wrapWidth)
    {
        List<(string Source, string Relative)> files = CollectInputFiles(tblPath);
        bool singleInput = File.Exists(tblPath);
        string fullOutputPath = Path.GetFullPath(outputPath);

        if (!singleInput)
            Directory.CreateDirectory(fullOutputPath);

        foreach ((string source, string relative) in files)
        {
            string jsonPath = ResolveJsonPath(scriptPath, relative, singleInput);
            using FileStream json = File.OpenRead(jsonPath);
            List<ScriptLine>? lines = JsonSerializer.Deserialize(json, JsonContext.ListScriptLine);
            if (lines is null)
                throw new InvalidDataException($"empty script JSON: {jsonPath}");

            TblStrTable baseTable = TblStrTable.Load(source);
            TblStrTable imported = TblStrScript.Import(baseTable, lines, wrapWidth);
            string target = ResolveOutputPath(fullOutputPath, relative, singleInput);
            imported.Save(target);
        }

        Console.WriteLine("Import success!");
        return 0;
    }

    private static List<(string Source, string Relative)> CollectInputFiles(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            if (string.Equals(Path.GetExtension(fullPath), ".ari", StringComparison.OrdinalIgnoreCase))
                fullPath = Path.ChangeExtension(fullPath, ".arc");

            return new List<(string, string)> { (fullPath, Path.GetFileName(fullPath)) };
        }

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException(fullPath);

        List<(string Source, string Relative)> files = Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
            .Where(IsScriptTableCandidate)
            .Select(file => (Source: file, Relative: Path.GetRelativePath(fullPath, file)))
            .OrderBy(file => file.Relative, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
            throw new InvalidOperationException("input directory does not contain TBLSTR table files");

        return files;
    }

    private static bool IsScriptTableCandidate(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".ari", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(Path.GetFileName(path), "tblstr.arc", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            using FileStream file = File.OpenRead(path);
            Span<byte> signature = stackalloc byte[4];
            if (file.Length >= signature.Length)
            {
                file.ReadExactly(signature);
                return signature.SequenceEqual("UF01"u8);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return false;
    }

    private static string ResolveJsonPath(string scriptPath, string relative, bool singleInput)
    {
        string fullScriptPath = Path.GetFullPath(scriptPath);
        if (File.Exists(fullScriptPath))
            return fullScriptPath;

        if (!Directory.Exists(fullScriptPath))
            throw new DirectoryNotFoundException(fullScriptPath);

        string jsonRelative = Path.ChangeExtension(relative, ".json");
        string jsonPath = Path.Combine(fullScriptPath, jsonRelative);
        if (!File.Exists(jsonPath))
        {
            if (singleInput)
            {
                string flatJsonPath = Path.Combine(fullScriptPath, Path.GetFileName(jsonRelative));
                if (File.Exists(flatJsonPath))
                    return flatJsonPath;
            }

            throw new FileNotFoundException("script JSON was not found", jsonPath);
        }

        return jsonPath;
    }

    private static string ResolveOutputPath(string outputPath, string relative, bool singleInput)
    {
        if (singleInput && Path.HasExtension(outputPath) && !Directory.Exists(outputPath))
            return outputPath;

        return Path.Combine(outputPath, relative);
    }
}
