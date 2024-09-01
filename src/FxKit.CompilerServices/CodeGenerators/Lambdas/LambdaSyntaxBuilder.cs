using FxKit.CompilerServices.Utilities;

// ReSharper disable InvertIf
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
namespace FxKit.CompilerServices.CodeGenerators.Lambdas;

/// <summary>
///     Syntax builder for the <see cref="LambdaGenerator"/>.
/// </summary>
internal static class LambdaSyntaxBuilder
{
    /// <summary>
    ///     Generates code for the lambda generation.
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    internal static string Generate(LambdaGenerationFile source)
    {
        using var writer = new IndentedTextWriter();
        writer.WriteLine("using System;\n");
        writer.WriteLine($"namespace {source.Namespace};\n");

        using (TypeHierarchyWriter.WriteTypeHierarchy(writer, source.TypeHierarchy))
        {
            // At this point we're inside the generated type, with correct indentation.
            for (var index = 0; index < source.Methods.Length; index++)
            {
                var descriptor = source.Methods[index];
                if (index > 0)
                {
                    writer.WriteLine();
                }

                switch (descriptor.Target)
                {
                    case LambdaTarget.Constructor:
                        writer.WriteLine(
                            $"""
                             /// <summary>
                             ///     The {descriptor.TypeOrMethodName} constructor as a Func.
                             /// </summary>
                             """,
                            isMultiline: true);
                        break;
                    case LambdaTarget.Method:
                        writer.WriteLine(
                            $"""
                             /// <summary>
                             ///     The {descriptor.TypeOrMethodName} method as a Func.
                             /// </summary>
                             """,
                            isMultiline: true);
                        break;
                }

                writer.Write("public static readonly Func<");
                foreach (var param in descriptor.Parameters)
                {
                    writer.Write($"{param.FullyQualifiedTypeName}, ");
                }

                writer.Write($"{descriptor.ReturnType}> ");
                if (descriptor.Target == LambdaTarget.Method)
                {
                    writer.Write(descriptor.TypeOrMethodName);
                }

                writer.Write("λ = (");
                writer.WriteParameterNames(descriptor.Parameters);
                writer.Write(") => ");

                switch (descriptor.Target)
                {
                    case LambdaTarget.Constructor:
                        writer.Write("new(");
                        break;
                    case LambdaTarget.Method:
                        writer.Write($"{descriptor.TypeOrMethodName}(");
                        break;
                }

                writer.WriteParameterNames(descriptor.Parameters);
                writer.Write(");");
            }
        }

        return writer.ToString();
    }
}
