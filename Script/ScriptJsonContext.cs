using System.Text.Json.Serialization;

namespace KaguyaArcTool.Script;

[JsonSerializable(typeof(List<ScriptLine>))]
internal sealed partial class ScriptJsonContext : JsonSerializerContext;
