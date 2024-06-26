﻿using System.Text;
using FxKit.CompilerServices.Utilities;

namespace FxKit.CompilerServices.CodeGenerators;

/// <summary>
///     All the ugly syntax building stuff for the union generator.
/// </summary>
internal static class UnionSyntaxBuilder
{
    private const string Indentation = "    ";

    /// <summary>
    ///     Generates the union members source.
    /// </summary>
    /// <param name="union"></param>
    /// <returns></returns>
    public static (string Hint, string Source) GenerateUnionMembers(UnionToGenerate union)
    {
        var sb = new StringBuilder(2048);
        sb.AppendLine(SourceGenerationHelper.AutoGeneratedHeader);
        sb.AppendLine(SourceGenerationHelper.NullableEnabledDirective);

        // Add using statements.
        foreach (var ns in union.NamespacesToInclude)
        {
            sb.Append("using ").Append(ns).Append(';').AppendLine();
        }

        // Add namespace.
        sb.AppendLine();
        sb.Append("namespace ").Append(union.UnionNamespace).Append(';').AppendLine();

        // Add the outer record.
        sb.Append('\n')
            .Append(union.Accessibility)
            .Append(" abstract partial record ")
            .Append(union.UnionName)
            .Append(
                @"
{");

        // Print the Union members.
        foreach (var constructor in union.Constructors)
        {
            sb.AppendLine();
            PrintUnionConstructor(sb, union, constructor);
        }

        sb.AppendLine();

        // Print the Match method.
        PrintUnionMatchMethod(sb, union);

        // End of the outer record
        sb.Append('}').AppendLine();

        return (Hint: $"{union.UnionName}_Union.Generated.cs", Source: sb.ToString());
    }

    /// <summary>
    ///     Prints the individual union constructor-related code.
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="union"></param>
    /// <param name="constructor"></param>
    private static void PrintUnionConstructor(
        StringBuilder sb,
        UnionToGenerate union,
        UnionConstructor constructor)
    {
        sb.Append(Indentation).Append("public sealed partial record ").Append(constructor.MemberName);
        sb.Append(" : ").AppendLine(union.UnionName);
        sb.Append(Indentation).Append('{').AppendLine();
        PrintUnionConstructorOf(sb, union, constructor);
        sb.AppendLine();
        sb.AppendLine();
        PrintUnionConstructorLambda(sb, union, constructor);
        sb.AppendLine();
        sb.Append(Indentation).Append('}').AppendLine();
    }

    /// <summary>
    ///     Prints the .Of static method.
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="union"></param>
    /// <param name="constructor"></param>
    private static void PrintUnionConstructorOf(
        StringBuilder sb,
        UnionToGenerate union,
        UnionConstructor constructor)
    {
        sb.Append(Indentation)
            .Append(Indentation)
            .AppendLine("/// <summary>");
        sb.Append(Indentation)
            .Append(Indentation)
            .Append(@"///     The same as ""new ")
            .Append(constructor.MemberName)
            .AppendLine(@""" but the return type is that of the base type.");
        sb.Append(Indentation)
            .Append(Indentation)
            .AppendLine("/// </summary>");
        sb.Append(Indentation)
            .Append(Indentation)
            .AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.Append(Indentation)
            .Append(Indentation)
            .AppendLine("[ExcludeFromCodeCoverage]");
        sb.Append(Indentation).Append(Indentation).AppendLine("[DebuggerHidden]");
        sb.Append(Indentation)
            .Append(Indentation)
            .Append("public static ")
            .Append(union.UnionName)
            .Append(" Of(");
        var needsComma = false;
        foreach (var param in constructor.Parameters)
        {
            if (needsComma)
            {
                sb.Append(',');
            }

            sb.AppendLine();
            sb.Append(Indentation).Append(Indentation).Append(Indentation);
            sb.Append(param.FullyQualifiedTypeName).Append(' ').Append(param.ParameterName);

            needsComma = true;
        }

        sb.AppendLine(") =>");
        sb.Append(Indentation).Append(Indentation).Append(Indentation);
        PrintNewExpression(sb, constructor);
    }

    /// <summary>
    ///     Prints the λ func.
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="union"></param>
    /// <param name="constructor"></param>
    private static void PrintUnionConstructorLambda(
        StringBuilder sb,
        UnionToGenerate union,
        UnionConstructor constructor)
    {
        sb.Append(Indentation)
            .Append(Indentation)
            .AppendLine("/// <summary>");
        sb.Append(Indentation)
            .Append(Indentation)
            .AppendLine(@"///     A Func variant for 'Of'");
        sb.Append(Indentation)
            .Append(Indentation)
            .AppendLine("/// </summary>");
        sb.Append(Indentation)
            .Append(Indentation)
            .Append("public static readonly Func<");

        var needsComma = false;
        foreach (var param in constructor.Parameters)
        {
            if (needsComma)
            {
                sb.Append(", ");
            }

            sb.Append(param.FullyQualifiedTypeName);

            needsComma = true;
        }

        // If we have printed a type name for the parameters, then we need to
        // add another command since it would currently have written only "Func<TypeName".
        if (needsComma)
        {
            sb.Append(", ");
        }

        // Alias the lambda to `Of`.
        sb.Append(union.UnionName).Append("> λ = Of;");
    }

    /// <summary>
    ///     Prints the New expression.
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="constructor"></param>
    private static void PrintNewExpression(StringBuilder sb, UnionConstructor constructor)
    {
        sb.Append("new ").Append(constructor.MemberName).Append('(');
        var needsComma = false;
        foreach (var param in constructor.Parameters)
        {
            if (needsComma)
            {
                sb.Append(',');
            }

            sb.AppendLine();
            sb.Append(Indentation).Append(Indentation).Append(Indentation).Append(Indentation);
            sb.Append(param.ParameterName);

            needsComma = true;
        }

        sb.Append(");");
    }

    /// <summary>
    ///     Prints a Match method that takes in Func.
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="union"></param>
    private static void PrintUnionMatchMethod(StringBuilder sb, UnionToGenerate union)
    {
        sb.Append(Indentation).AppendLine("/// <summary>");
        sb.Append(Indentation)
            .AppendLine("///     Performs an exhaustive match on the union constituents.");
        sb.Append(Indentation).AppendLine("/// </summary>");
        sb.Append(Indentation).Append("public TResult Match<TResult>(");

        // Print the parameter names, they will be the names of the constituents.
        var needsComma = false;
        foreach (var constructor in union.Constructors)
        {
            if (needsComma)
            {
                sb.Append(',');
            }

            sb.AppendLine();
            sb.Append(Indentation).Append(Indentation);
            sb.Append("Func<")
                .Append(constructor.MemberName)
                .Append(", TResult> ")
                .Append(constructor.MemberName);
            needsComma = true;
        }

        // Print the switch expression.
        sb.AppendLine(") => this switch");
        sb.Append(Indentation).AppendLine("{");

        // Print the switch arms per union constituent.
        foreach (var constructor in union.Constructors)
        {
            sb.Append(Indentation)
                .Append(Indentation)
                .Append(union.UnionName)
                .Append('.')
                .Append(constructor.MemberName)
                .Append(" x => ")
                .Append(constructor.MemberName)
                .Append("(x)")
                .Append(',')
                .AppendLine();
        }

        // Default arm
        sb.Append(Indentation)
            .Append(Indentation)
            .Append(
                @"_ => throw new ArgumentOutOfRangeException(message: $""The type '{this.GetType()}' is not a known variant of ")
            .Append(union.UnionName)
            .Append(@""", innerException: null)")
            .AppendLine();

        // Close the method
        sb.Append(Indentation)
            .Append("};")
            .AppendLine();
    }
}
