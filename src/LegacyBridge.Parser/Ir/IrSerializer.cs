using System.Text.Json;

namespace LegacyBridge.Parser.Ir;

public static class IrSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string ToJson(IrProgram program) =>
        JsonSerializer.Serialize(program, Options);
}
