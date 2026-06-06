using System.Text.Json.Serialization;

namespace KaguyaArcTool.Image;

[JsonSerializable(typeof(ImageMetadata))]
internal sealed partial class ImageJsonContext : JsonSerializerContext;
