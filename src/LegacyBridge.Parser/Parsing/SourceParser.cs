using LegacyBridge.Parser.Ir;

namespace LegacyBridge.Parser.Parsing;

/// <summary>Picks VFP vs PowerBuilder from the file extension, same IR either way.</summary>
public static class SourceParser
{
    public static bool IsLegacySource(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".prg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".sru", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".srw", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".srf", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".srd", StringComparison.OrdinalIgnoreCase);
    }

    public static IrProgram Parse(string source, string sourceName, bool strict = false)
    {
        var ext = Path.GetExtension(sourceName);
        if (ext.Equals(".srd", StringComparison.OrdinalIgnoreCase))
            return PbParser.ParseDataWindow(source, sourceName);
        if (ext.Equals(".sru", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".srw", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".srf", StringComparison.OrdinalIgnoreCase))
            return PbParser.Parse(source, sourceName, strict);
        return VfpParser.Parse(source, sourceName, strict);
    }
}
