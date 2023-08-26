namespace Sia.CodeGenerators;

using System.Text;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

[Generator]
internal partial class ComponentPropertyGenerator : IIncrementalGenerator
{
    private readonly record struct CodeGenerationInfo(
        INamespaceSymbol Namespace, ImmutableArray<TypeDeclarationSyntax> ParentTypes,
        TypeDeclarationSyntax ContainingType, string ValueName, string ValueType,
        ImmutableDictionary<string, TypedConstant> Arguments);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context => {
            context.AddSource("SiaPropertyAttribute.g.cs",
                SourceText.From(SiaPropertyAttributeSource, Encoding.UTF8));
        });

        var codeGenInfos = context.SyntaxProvider.ForAttributeWithMetadataName(
            SiaPropertyAttributeName,
            static (syntaxNode, token) =>
                FindParentNode<TypeDeclarationSyntax>(syntaxNode, out var parent)
                    && (parent.IsKind(SyntaxKind.StructDeclaration) || parent.IsKind(SyntaxKind.RecordStructDeclaration))
                    && parent.Modifiers.Any(SyntaxKind.PartialKeyword)
                    && CheckWritable(syntaxNode),
            static (syntax, token) => {
                FindParentNode<TypeDeclarationSyntax>(syntax.TargetNode, out var containingType);
                return (syntax, containingType!, ParentTypes: GetParentTypes(containingType!));
            })
            .Where(static t => t.ParentTypes.All(
                static typeDecl => typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword)))
            .Select(static (t, token) => {
                static string GetFullType(SemanticModel model, SyntaxNode typeNode, CancellationToken token)
                    => model.GetTypeInfo(typeNode, token).Type!.ToDisplayString();
                
                static string GetVariableType(SemanticModel model, VariableDeclaratorSyntax syntax, CancellationToken token) {
                    var parentDecl = (VariableDeclarationSyntax)syntax.Parent!;
                    return GetFullType(model, parentDecl.Type, token);
                };

                var (syntax, containtingType, parentTypes) = t;
                var arguments = syntax.Attributes[0].NamedArguments.ToImmutableDictionary();

                return new CodeGenerationInfo {
                    Namespace = syntax.TargetSymbol.ContainingNamespace,
                    ParentTypes = parentTypes,
                    ContainingType = containtingType,
                    Arguments = arguments,
                    ValueName = syntax.TargetSymbol.Name,
                    ValueType = syntax.TargetNode switch {
                        PropertyDeclarationSyntax propSyntax =>
                            GetFullType(syntax.SemanticModel, propSyntax.Type, token),
                        VariableDeclaratorSyntax varSyntax =>
                            GetVariableType(syntax.SemanticModel, varSyntax, token),
                        ParameterSyntax paramSyntax =>
                            GetFullType(syntax.SemanticModel, paramSyntax.Type!, token),
                        _ => throw new InvalidDataException("Invalid syntax")
                    }
                };
            });
        
        context.RegisterSourceOutput(codeGenInfos, static (context, info) => {
            var builder = new StringBuilder();
            using var writer = new StringWriter(builder, CultureInfo.InvariantCulture);
            using var source = new IndentedTextWriter(writer, "    ");

            source.WriteLine("// <auto-generated/>");
            source.WriteLine("#nullable enable");
            source.WriteLine();

            var setCommandName = info.Arguments.TryGetValue("SetCommand", out var setCmdName)
                ? setCmdName.Value!.ToString()! : $"Set{info.ValueName}";
            GenerateSetCommand(setCommandName, info, source);

            var fileName = GenerateFileName(info);
            context.AddSource(fileName, writer.ToString());
        });
    }

    private static bool CheckWritable(SyntaxNode node)
        => node switch {
            PropertyDeclarationSyntax propSyntax =>
                propSyntax.AccessorList!.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)),
            VariableDeclaratorSyntax varSyntax =>
                FindParentNode<FieldDeclarationSyntax>(varSyntax, out var fieldDecl)
                    && !fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
            ParameterSyntax paramSyntax =>
                paramSyntax.Parent!.Parent is RecordDeclarationSyntax recordDecl
                    && !recordDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
            _ => false
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool FindParentNode<TNode>(
        SyntaxNode node, [MaybeNullWhen(false)] out TNode result)
        where TNode : SyntaxNode
    {
        SyntaxNode? currNode = node;
        while (currNode != null) {
            var parent = currNode.Parent;
            if (parent is TNode casted) {
                result = casted;
                return true;
            }
            currNode = parent;
        }
        result = default;
        return false;
    }

    private static ImmutableArray<TypeDeclarationSyntax> GetParentTypes(TypeDeclarationSyntax decl)
    {
        var builder = ImmutableArray.CreateBuilder<TypeDeclarationSyntax>();
        var parent = decl.Parent;

        while (parent != null) {
            if (parent is TypeDeclarationSyntax typeDecl) {
                builder.Add(typeDecl);
            }
            parent = parent.Parent;
        }

        return builder.ToImmutable();
    }

    private static string GenerateFileName(in CodeGenerationInfo info)
    {
        var builder = new StringBuilder();
        builder.Append(info.Namespace.ToDisplayString());
        builder.Append('.');
        foreach (var parentType in info.ParentTypes) {
            builder.Append(parentType.Identifier.ToString());
            builder.Append('.');
        }
        builder.Append(info.ContainingType.Identifier.ToString());
        builder.Append('.');
        builder.Append(info.ValueName);
        builder.Append(".g.cs");
        return builder.ToString();
    }

    private static void GenerateSetCommand(string commandName, in CodeGenerationInfo info, IndentedTextWriter source)
    {
        var hasNamespace = !info.Namespace.IsGlobalNamespace;
        if (hasNamespace) {
            source.Write("namespace ");
            source.WriteLine(info.Namespace.ToDisplayString());
            source.WriteLine("{");
            source.Indent++;
        }

        foreach (var typeDecl in info.ParentTypes) {
            if (typeDecl.Modifiers.Any(SyntaxKind.StaticKeyword)) {
                source.Write("static ");
            }
            switch (typeDecl.Kind()) {
            case SyntaxKind.ClassDeclaration:
                source.Write("partial class ");
                break;
            case SyntaxKind.StructDeclaration:
                source.Write("partial struct ");
                break;
            case SyntaxKind.RecordDeclaration:
                source.Write("partial record ");
                break;
            case SyntaxKind.RecordStructDeclaration:
                source.Write("partial record struct ");
                break;
            }
            source.WriteLine(typeDecl.Identifier.ToString());
            source.WriteLine("{");
            source.Indent++;
            break;
        }

        source.Write("partial ");
        source.Write(info.ContainingType.Kind() switch {
            SyntaxKind.ClassDeclaration => "class ",
            SyntaxKind.StructConstraint => "struct ",
            SyntaxKind.RecordDeclaration => "record ",
            SyntaxKind.RecordStructDeclaration => "record struct ",
            _ => throw new InvalidOperationException("Invalid containing type")
        });

        var containingTypeName = info.ContainingType.Identifier.ToString();
        source.WriteLine(containingTypeName);
        source.WriteLine("{");
        source.Indent++;

        source.Write("public sealed class ");
        source.Write(commandName);
        source.Write(" : global::Sia.PropertyCommand<");
        source.Write(commandName);
        source.Write(", ");
        source.Write(info.ValueType);
        source.WriteLine(">");
        source.WriteLine("{");
        source.Indent++;

        source.WriteLine("public override void Execute(in global::Sia.EntityRef target)");
        source.Indent++;
        source.Write("=> target.Get<");
        source.Write(containingTypeName);
        source.Write(">().");
        source.Write(info.ValueName);
        source.WriteLine(" = Value;");
        source.Indent--;

        source.Indent--;
        source.WriteLine("}");

        source.Indent--;
        source.WriteLine("}");

        var parentTypeCount = info.ParentTypes.Length;
        for (int i = 0; i < parentTypeCount; ++i) {
            source.Indent--;
            source.WriteLine("}");
        }

        if (hasNamespace) {
            source.Indent--;
            source.WriteLine("}");
        }

        Debug.Assert(source.Indent == 0);
    }
}