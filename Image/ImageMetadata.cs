using System.Text.Json.Serialization;

namespace KaguyaArcTool.Image;

internal sealed class ImageMetadata
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = "AP";

    [JsonPropertyName("original_name")]
    public string OriginalName { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("bpp")]
    public int Bpp { get; set; } = 24;

    [JsonPropertyName("offset_x")]
    public int OffsetX { get; set; }

    [JsonPropertyName("offset_y")]
    public int OffsetY { get; set; }

    [JsonPropertyName("compression")]
    public int Compression { get; set; }

    [JsonPropertyName("packed_size")]
    public int PackedSize { get; set; }

    [JsonPropertyName("unpacked_size")]
    public int UnpackedSize { get; set; }

    [JsonPropertyName("aps_header_base64")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApsHeaderBase64 { get; set; }
}
