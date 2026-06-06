using KaguyaArcTool.Arc;

namespace KaguyaArcTool.Commands;

internal static class ProgramMain
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintMainUsage();
            return 0;
        }

        string command = args[0].ToLowerInvariant();
        return command switch
        {
            "ar" when args.Length == 1 => PrintArchiveUsage(),
            "sc" when args.Length == 1 => PrintScriptUsage(),
            "img" when args.Length == 1 => PrintImageUsage(),
            "sc" when args.Length == 4 && args[1].Equals("-e", StringComparison.OrdinalIgnoreCase) => ScriptCommands.Export(args[2], args[3]),
            "sc" when args.Length is 5 or 6 && args[1].Equals("-i", StringComparison.OrdinalIgnoreCase) => ScriptCommands.Import(args[2], args[3], args[4], ParseWrapWidth(args)),
            "img" when args.Length == 4 && args[1].Equals("-d", StringComparison.OrdinalIgnoreCase) => ImageCommands.Decode(args[2], args[3]),
            "img" when args.Length == 4 && args[1].Equals("-e", StringComparison.OrdinalIgnoreCase) => ImageCommands.Encode(args[2], args[3]),
            "-p" when args.Length == 2 => ArchiveCommands.Info(args[1]),
            "-u" when args.Length == 3 => ArchiveCommands.Unpack(args[1], args[2]),
            "-c" when args.Length == 4 => ArchiveCommands.Pack(args[1], args[2], ArcKindExtensions.ParsePackType(args[3])),
            "ar" when args.Length == 3 && args[1].Equals("-p", StringComparison.OrdinalIgnoreCase) => ArchiveCommands.Info(args[2]),
            "ar" when args.Length == 4 && args[1].Equals("-u", StringComparison.OrdinalIgnoreCase) => ArchiveCommands.Unpack(args[2], args[3]),
            "ar" when args.Length == 5 && args[1].Equals("-c", StringComparison.OrdinalIgnoreCase) => ArchiveCommands.Pack(args[2], args[3], ArcKindExtensions.ParsePackType(args[4])),
            _ => UsageError()
        };
    }

    private static int PrintArchiveUsage()
    {
        Console.WriteLine("UF01/AF01 archive:");
        Console.WriteLine("Print info: KaguyaTool -p <archive.arc>");
        Console.WriteLine("Unpack:     KaguyaTool -u <archive.arc> <output_dir>");
        Console.WriteLine("Pack:       KaguyaTool -c <input_dir> <archive.arc> <type> (1: UF01 / 2: AF01 / 3: WFL1+ARI)");
        return 0;
    }

    private static int PrintScriptUsage()
    {
        Console.WriteLine("TBLSTR script");
        Console.WriteLine("Export: KaguyaTool sc -e tbl_folder out_folder");
        Console.WriteLine("Import: KaguyaTool sc -i tbl_folder in_scr_folder out_tbl [wrap value]");
        return 0;
    }

    private static int PrintImageUsage()
    {
        Console.WriteLine("Image tool");
        Console.WriteLine("Decode: KaguyaTool img -d src_folder png_folder");
        Console.WriteLine("Encode: KaguyaTool img -e png_folder out_folder");
        return 0;
    }

    private static void PrintMainUsage()
    {
        Console.WriteLine("KaguyaTool [option]");
        Console.WriteLine("Option:");
        Console.WriteLine("ar\t\t- Archive Tool (UF01/AF01/WFL1+ARI)");
        Console.WriteLine("sc\t\t- Script Tool");
        Console.WriteLine("img\t\t- Image Tool (AP/\\x04APS3)");
    }

    private static int UsageError()
    {
        PrintMainUsage();
        return 1;
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "/?";
    }

    private static int ParseWrapWidth(string[] args)
    {
        if (args.Length == 5)
            return 0;

        if (!int.TryParse(args[5], out int width) || width < 0)
            throw new ArgumentException("wrap value must be a non-negative integer");

        return width;
    }
}
