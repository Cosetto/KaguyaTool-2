namespace KaguyaArcTool.Arc;

internal static class ArcKindExtensions
{
    public static ArcKind ParsePackType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "1" or "uf" or "uf01" or "arcuf" => ArcKind.Uf01,
            "2" or "af" or "af01" or "arcaf" => ArcKind.Af01,
            "3" or "wfl" or "wfl1" or "ari" or "arcari" => ArcKind.Wfl1,
            _ => throw new ArgumentException("pack type must be 1 for ArcUF/UF01, 2 for ArcAF/AF01, or 3 for WFL1/ARI")
        };
    }

    public static string Describe(this ArcKind kind)
    {
        return kind switch
        {
            ArcKind.Uf01 => "ArcUF / UF01",
            ArcKind.Af01 => "ArcAF / AF01",
            ArcKind.Wfl1 => "WFL1 / ARI",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public static string Signature(this ArcKind kind)
    {
        return kind switch
        {
            ArcKind.Uf01 => "UF01",
            ArcKind.Af01 => "AF01",
            ArcKind.Wfl1 => "WFL1",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
