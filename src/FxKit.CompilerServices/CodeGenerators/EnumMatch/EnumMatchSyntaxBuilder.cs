using FxKit.CompilerServices.Utilities;

namespace FxKit.CompilerServices.CodeGenerators.EnumMatch;

/// <summary>
///     Syntax builder for the <see cref="EnumMatchGenerator"/>.
/// </summary>
internal static class EnumMatchSyntaxBuilder
{
    /// <summary>
    ///     Generate the extension class.
    /// </summary>
    /// <param name="enumToGenerate"></param>
    /// <returns></returns>
    internal static string GenerateMatchExtensionClass(EnumToGenerate enumToGenerate)
    {
        using var writer = new IndentedTextWriter();
        writer.WriteLine(SourceGenerationHelper.AutoGeneratedHeader);
        writer.WriteLine("using System;");
        writer.WriteLine();
        writer.WriteLine($"namespace {enumToGenerate.ContainingNamespace};\n");
        writer.WriteLine($"public static partial class {enumToGenerate.Identifier}MatchExtension");
        using (writer.WriteBlock())
        {
            WriteMatchFunction(writer, enumToGenerate, func: true);
            writer.WriteLine();
            WriteMatchFunction(writer, enumToGenerate, func: false);
        }

        return writer.ToString();
    }

    /// <summary>
    ///     Writes the match function.
    /// </summary>
    /// <param name="writer">
    ///     The indented text writer.
    /// </param>
    /// <param name="enumToGenerate">
    ///     The enum to generate the match function for.
    /// </param>
    /// <param name="func">
    ///     Whether the match function uses functions as arms or plain values.
    /// </param>
    private static void WriteMatchFunction(
        IndentedTextWriter writer,
        EnumToGenerate enumToGenerate,
        bool func)
    {
        writer.WriteLine(
            content: $"""
                      /// <summary>
                      ///     Perform an exhaustive match on the enum value.
                      /// </summary>
                      [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                      public static TMatchResult Match<TMatchResult>(
                          this {enumToGenerate.Name} source,
                      """,
            isMultiline: true);
        writer.IncreaseIndent();

        // Generate a parameter for each enum field.
        var lastIndex = enumToGenerate.Members.Count - 1;
        for (var index = 0; index < enumToGenerate.Members.Count; index++)
        {
            var member = enumToGenerate.Members[index];
            if (func)
            {
                writer.Write($"Func<TMatchResult> {member}");
            }
            else
            {
                writer.Write($"TMatchResult {member}");
            }

            if (index != lastIndex)
            {
                writer.WriteLine(",");
            }
        }

        writer.WriteLine(") => source switch");
        writer.WriteLine("{");
        writer.IncreaseIndent();
        foreach (var member in enumToGenerate.Members)
        {
            if (func)
            {
                writer.WriteLine($"{enumToGenerate.Name}.{member} => {member}(),");
            }
            else
            {
                writer.WriteLine($"{enumToGenerate.Name}.{member} => {member},");
            }
        }

        writer.WriteLine("_ => throw new ArgumentOutOfRangeException(nameof(source), source, null)");
        writer.DecreaseIndent();
        writer.WriteLine("};");
        writer.DecreaseIndent();
    }
}
