using System.Runtime.InteropServices.ComTypes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using static PrivateProxy.Generator.EmitHelper;
using System.Reflection.Metadata;
using System.Linq;
using System.Collections.Immutable;

namespace PrivateProxy.Generator;

[Generator(LanguageNames.CSharp)]
public partial class PrivateProxyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // context.RegisterPostInitializationOutput(EmitAttributes);

        var source = context.SyntaxProvider.ForAttributeWithMetadataName(
            "ILAttributes.PrivateProxy.GeneratePrivateProxyAttribute",
            static (node, token) => node is StructDeclarationSyntax or ClassDeclarationSyntax,
            static (context, token) => context);

        // NOTE: currently does not provide private metadata from external dll.
        // context.CompilationProvider.Select((x, token) => x.WithOptions(x.Options.WithMetadataImportOptions(MetadataImportOptions.All));

        context.RegisterSourceOutput(source, Emit);
    }


    [Flags]
    public enum PrivateProxyGenerateKinds
    {
        All = 0, // Field | Method | Property | Instance | Static
        Field = 1,
        Method = 2,
        Property = 4,
        Instance = 8,
        Static = 16,
    }

    static void Emit(SourceProductionContext context, GeneratorAttributeSyntaxContext source)
    {
        var attr = source.Attributes[0]; // allowMultiple:false
        GetAttributeParameters(attr, out var targetType, out var kind);

        var proxy = (INamedTypeSymbol)source.TargetSymbol;
        if (!Verify(context, (TypeDeclarationSyntax)source.TargetNode, proxy,
                targetType))
        {
            return;
        }
       

        var members = GetMembers(targetType, kind);
      
        if (members.Length == 0)
        {
            return;
        }

        //// Generate Code
        var code = EmitCode(proxy, targetType, members);
        AddSource(context, source.TargetSymbol, code);
    }

    static void GetAttributeParameters(AttributeData attr, out INamedTypeSymbol targetType,
        out PrivateProxyGenerateKinds kind)
    {
        // Extract attribute parameter
        // public GeneratePrivateProxyAttribute(Type target)
        // public GeneratePrivateProxyAttribute(Type target, PrivateProxyGenerateKinds generateKinds)

        targetType = (INamedTypeSymbol)attr.ConstructorArguments[0].Value!;

        if (attr.ConstructorArguments.Length == 1)
        {
            kind = PrivateProxyGenerateKinds.All;
        }
        else
        {
            kind = (PrivateProxyGenerateKinds)attr.ConstructorArguments[1].Value!;
        }
    }
    static bool CanExpose(ITypeSymbol typeSymbol)
    {
        var declaredAccessibility = typeSymbol.DeclaredAccessibility;
        switch (declaredAccessibility)
        {
            case Accessibility.Public:
                return true;
            case Accessibility.NotApplicable:
                return true;
            default:
                return false;
        }
    }
    static MetaMember[] GetMembers(INamedTypeSymbol targetType, PrivateProxyGenerateKinds kind)
    {
        var members = targetType.IsGenericType? targetType.OriginalDefinition.GetMembers(): targetType.GetMembers();


        var list = new List<MetaMember>(members.Length);

        kind = (kind == PrivateProxyGenerateKinds.All)
            ? PrivateProxyGenerateKinds.Field | PrivateProxyGenerateKinds.Method | PrivateProxyGenerateKinds.Property |
              PrivateProxyGenerateKinds.Instance | PrivateProxyGenerateKinds.Static
            : kind;

        var generateField = kind.HasFlag(PrivateProxyGenerateKinds.Field);
        var generateProperty = kind.HasFlag(PrivateProxyGenerateKinds.Property);
        var generateMethod = kind.HasFlag(PrivateProxyGenerateKinds.Method);
        var generateInstance = kind.HasFlag(PrivateProxyGenerateKinds.Instance);
        var generateStatic = kind.HasFlag(PrivateProxyGenerateKinds.Static);

        // If only set Static or Instance, generate all member kind
        if (!generateField && !generateProperty && !generateMethod)
        {
            generateField = generateProperty = generateMethod = true;
        }

        // If only set member kind, generate both static and instance
        if (!generateStatic && !generateInstance)
        {
            generateStatic = generateInstance = true;
        }

        foreach (var item in members)
        {
            if (!item.CanBeReferencedByName) continue;

            if (item.IsStatic && !generateStatic) continue;
            if (!item.IsStatic && !generateInstance) continue;

            // add field/property/method
            if (generateField && item is IFieldSymbol f)
            {

                // return type can not be exposed, don't generate
                if (!CanExpose(f.Type)) continue;
                // public member don't generate
                if (f.DeclaredAccessibility == Accessibility.Public) continue;

                list.Add(new(item));
            }
            else if (generateProperty && item is IPropertySymbol p)
            {
                if (!CanExpose(p.Type)) continue;

                if (p.DeclaredAccessibility == Accessibility.Public)
                {
                    var getPublic = true;
                    var setPublic = true;
                    if (p.GetMethod != null)
                    {
                        getPublic = p.GetMethod.DeclaredAccessibility == Accessibility.Public;
                    }

                    if (p.SetMethod != null)
                    {
                        setPublic = p.SetMethod.DeclaredAccessibility == Accessibility.Public;
                    }

                    if (getPublic && setPublic) continue;
                }

                list.Add(new(item));
            }
            else if (generateMethod && item is IMethodSymbol m)
            {
                if (targetType.IsGenericType && m.IsGenericMethod) continue;
                // both return type and parameter type can be exposed
                if (!CanExpose(m.ReturnType)) continue;
                foreach (var parameter in m.Parameters)
                {
                    if (!CanExpose(parameter.Type)) continue;
                }

                if (m.DeclaredAccessibility == Accessibility.Public) continue;
                var genericParams = m.TypeParameters;
                if(TryGetConstraintsString(genericParams, out var constraints))
                {
                    list.Add(new(m, genericParams,constraints));
                }
              
            }
        }

        return list.ToArray();
    }

    static bool Verify(SourceProductionContext context, TypeDeclarationSyntax typeSyntax, INamedTypeSymbol proxyType,
        INamedTypeSymbol targetType)
    {
        // Type Rule
        // ProxyClass: class -> allows class or struct
        //           : struct -> allows ref struct

        var hasError = false;

        // require partial
        if (!typeSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MustBePartial,
                typeSyntax.Identifier.GetLocation(), proxyType.Name));
            hasError = true;
        }

        // not allow readonly struct
        if (proxyType.IsValueType && proxyType.IsReadOnly)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.NotAllowReadOnly,
                typeSyntax.Identifier.GetLocation(), proxyType.Name));
            hasError = true;
        }

        // class, not allow ref struct
        if (targetType.IsReferenceType && proxyType.IsRefLikeType)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ClassNotAllowRefStruct,
                typeSyntax.Identifier.GetLocation(), proxyType.Name));
            hasError = true;
        }

        // struct, not allow class or struct(only allows ref struct)
        if (targetType.IsValueType)
        {
            if (proxyType.IsReferenceType)
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.StructNotAllowClass,
                    typeSyntax.Identifier.GetLocation(), proxyType.Name));
                hasError = true;
            }
            else if (proxyType.IsValueType && !proxyType.IsRefLikeType)
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.StructNotAllowStruct,
                    typeSyntax.Identifier.GetLocation(), proxyType.Name));
                hasError = true;
            }
        }

        // target type not allow `ref struct`
        if (targetType.IsValueType && targetType.IsRefLikeType)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.RefStructNotSupported,
                typeSyntax.Identifier.GetLocation()));
            hasError = true;
        }

        if (targetType.IsGenericType)
        {

            if (!targetType.IsUnboundGenericType)
            {
            
 
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.BoundedGenericsNotSupported,
                typeSyntax.Identifier.GetLocation(), targetType.GetType().ToString()));
                hasError = true;
            }
          /*  else
            {
                var constraints= GetConstraintsString(targetType.TypeParameters);
        ;
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InValidConstraints,
             typeSyntax.Identifier.GetLocation(), constraints));

            }*/
        }
        
        return !hasError;
    }
   
    static string EmitCode(ITypeSymbol proxyType, INamedTypeSymbol targetType, MetaMember[] members)
    {
        var typeDefName = targetType.ToFullyQualifiedFormatString();
        
        string   targetTypeFullName=typeDefName;
        var proxyTypeName = proxyType.ToFullyQualifiedFormatString();
        var typeGenerics = targetType.IsUnboundGenericType
            ? "<" + string.Join(", ", targetType.TypeParameters.Select(x => x.Name)) + ">"
            : "";
        var constraints = GetConstraintsString(targetType.TypeParameters);
        if (0< typeGenerics.Length)
        {
            targetTypeFullName = typeDefName.Split('<')[0]+typeGenerics;
        }
        
        var code = new StringBuilder();

        var accessibility = proxyType.DeclaredAccessibility.ToCode();
        var structOrClass = proxyType.IsReferenceType ? "class" : "struct";
        var refStruct = proxyType.IsRefLikeType ? "ref " : "";
        var isProxyStatic = proxyType.IsStatic;
        var proxyStatic = isProxyStatic ? "static " : " ";

        code.AppendLine($$"""
    [global::ILAttributes.ILProcess]
    {{refStruct}}{{proxyStatic}}partial {{structOrClass}} {{proxyType.Name}}{{typeGenerics}}{{constraints}}
    {
    """);
        if(!targetType.IsStatic)
        code.AppendLine($$"""
    {{If(proxyType.IsRefLikeType, $$"""
        global::ILAttributes.ByReference<{{targetTypeFullName}}> target__;
        public {{proxyType.Name}}({{refStruct}}{{targetTypeFullName}} target)
        {
        this.target__ =new global::ILAttributes.ByReference<{{targetTypeFullName}}> (ref target);
        }

""",
                                $$"""
        {{targetTypeFullName}} target__;
        public {{proxyType.Name}}({{targetTypeFullName}} target)
        {
            this.target__ = {{refStruct}}target;
        }
""")}}
""");
      

        var targetInstance = proxyType.IsRefLikeType ? "ref target__.Value" : "target__";

        foreach (var item in members)
        {
            var readonlyCode = item.IsRequireReadOnly ? "readonly " : "";
            var refReturn = item.IsRefReturn ? "ref " : "";
            var staticCode2 = item.IsStatic ? "static " : "";
            switch (item.MemberKind)
            {
                case MemberKind.Field:
                    code.AppendLine(
                        If(item.IsStatic, $$"""
        [UnsafeAccessor(UnsafeAccessorKind.StaticField, "{{item.Name}}", typeof({{typeDefName}}))]
        static extern ref {{readonlyCode}}{{item.MemberTypeFullName}} __{{item.Name}}__();

        public static ref {{readonlyCode}}{{item.MemberTypeFullName}} {{item.Name}} => ref __{{item.Name}}__();
""",
$$"""
        [UnsafeAccessor(UnsafeAccessorKind.Field, "{{item.Name}}")]
        static extern ref {{readonlyCode}}{{item.MemberTypeFullName}} __{{item.Name}}__({{refStruct}}{{targetTypeFullName}} target);

        public ref {{readonlyCode}}{{item.MemberTypeFullName}} {{item.Name}} => ref __{{item.Name}}__({{targetInstance}});
"""));


                    break;
                case MemberKind.Property:

                    if (item.HasGetMethod)
                    {
                        code.AppendLine(If(item.IsStatic, $$"""
        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, "get_{{item.Name}}", typeof({{typeDefName}}))]
        static extern {{refReturn}}{{item.MemberTypeFullName}} __get_{{item.Name}}__();

""", $$"""
        [UnsafeAccessor(UnsafeAccessorKind.Method, "get_{{item.Name}}")]
        static extern {{refReturn}}{{item.MemberTypeFullName}} __get_{{item.Name}}__({{refStruct}}{{targetTypeFullName}} target);
                                                                     
"""));
}

                    if (item.HasSetMethod)
                    {
                        code.AppendLine(If(item.IsStatic, $$"""
                                                                [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, "set_{{item.Name}}", typeof({{typeDefName}}))]
                                                                static extern void __set_{{item.Name}}__( {{item.MemberTypeFullName}} value);

                                                            """,
                            $$"""
                                    [UnsafeAccessor(UnsafeAccessorKind.Method, "get_{{item.Name}}")]
                                    static extern void __set_{{item.Name}}__({{refStruct}}{{targetTypeFullName}} target,{{item.MemberTypeFullName}} value);
                                
                              """));
                    }

                    code.AppendLine($$"""
        public {{staticCode2}}{{refReturn}}{{readonlyCode}}{{item.MemberTypeFullName}} {{item.Name}}
        {
""");
                    if (item.HasGetMethod)
                    {
                        code.AppendLine(If(item.IsStatic, $"            get => {refReturn}__get_{item.Name}__();",
                            $"          get => {refReturn}__get_{item.Name}__({targetInstance});"));
                    }

                    if (item.HasSetMethod)
                    {
                        code.AppendLine(If(item.IsStatic, $"            set => __set_{item.Name}__(value);",
                            $"          set => __set_{item.Name}__({targetInstance}, value);"));
                    }

                    code.AppendLine("       }"); // close property
                    break;
                case MemberKind.Method:
                    var generics = item.IsGeneric
                        ? "<" + string.Join(", ", item.GenericParameters.Select(x => x.Name)) + ">"
                        : "";
                    var parameters = string.Join(", ",
                        item.MethodParameters.Select(x =>
                            $"{x.RefKind.ToParameterPrefix()}{x.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {x.Name}"));
                    var parametersWithComma = (parameters != "" && !item.IsStatic) ? ", " + parameters : "";
                    var useParameters = string.Join(", ",
                        item.MethodParameters.Select(x => $"{x.RefKind.ToUseParameterPrefix()}{x.Name}"));
                    if (useParameters != "" && !item.IsStatic) useParameters = ", " + useParameters;

                    code.AppendLine(If(item.IsStatic, $$"""
        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, "{{item.Name}}", typeof({{typeDefName}}))]
        public static extern {{refReturn}}{{item.MemberTypeFullName}} {{item.Name}}{{generics}}({{parameters}}){{item.ConstraintsString}};

""", $$"""
        [UnsafeAccessor(UnsafeAccessorKind.Method, "{{item.Name}}")]
        static extern {{refReturn}}{{item.MemberTypeFullName}} __{{item.Name}}__{{generics}}({{refStruct}}{{targetTypeFullName}} target{{parametersWithComma}});
                                                               
        public {{refReturn}}{{readonlyCode}}{{item.MemberTypeFullName}} {{item.Name}}{{generics}}({{parameters}}){{item.ConstraintsString}} => {{refReturn}}__{{item.Name}}__({{targetInstance}}{{useParameters}});

"""));
                    break;
                default:
                    break;
            }
        }

        code.AppendLine("   }"); // close Proxy partial
        if (!targetType.IsStatic)
        {
            
            code.AppendLine($$"""

                                  {{accessibility}} static partial class {{targetType.Name}}PrivateProxyExtensions
                                  {
                                      public static {{proxyTypeName}} AsPrivateProxy{{typeGenerics}}(this {{refStruct}}{{targetTypeFullName}} target){{constraints}}
                                      {
                                          return new {{proxyTypeName}}({{refStruct}}target);
                                      }
                                  }
                              """);
        }

        return code.ToString();
    }

    static void AddSource(SourceProductionContext context, ISymbol targetSymbol, string code,
        string fileExtension = ".g.cs")
    {
        var fullType = targetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "")
            .Replace("<", "_")
            .Replace(">", "_");

        var sb = new StringBuilder();

        sb.AppendLine("""
                      // <auto-generated/>
                      #nullable enable
                      #pragma warning disable CS0108
                      #pragma warning disable CS0162
                      #pragma warning disable CS0164
                      #pragma warning disable CS0219
                      #pragma warning disable CS8600
                      #pragma warning disable CS8601
                      #pragma warning disable CS8602
                      #pragma warning disable CS8604
                      #pragma warning disable CS8619
                      #pragma warning disable CS8620
                      #pragma warning disable CS8631
                      #pragma warning disable CS8765
                      #pragma warning disable CS9074
                      #pragma warning disable CA1050

                      using System;
                      using System.Runtime.CompilerServices;
                      using System.Runtime.InteropServices;
                      using UnsafeAccessorAttribute=global::ILAttributes.ILUnsafeAccessorAttribute;
                      using UnsafeAccessorKind=global::ILAttributes.ILUnsafeAccessorKind;
                      """);

        var ns = targetSymbol.ContainingNamespace;
        if (!ns.IsGlobalNamespace)
        {
            sb.AppendLine($"namespace {ns} {{");
        }

        sb.AppendLine();

        sb.AppendLine(code);

        if (!ns.IsGlobalNamespace)
        {
            sb.AppendLine($"}}");
        }

        var sourceCode = sb.ToString();
        context.AddSource($"{fullType}{fileExtension}", sourceCode);
    }
}