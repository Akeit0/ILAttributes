using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace PrivateProxy.Generator;

internal static class EmitHelper
{
    public static string ToCode(this Accessibility accessibility)
    {
        switch (accessibility)
        {
            case Accessibility.NotApplicable:
                return "";
            case Accessibility.Private:
                return "private";
            case Accessibility.ProtectedAndInternal:
                return "private protected";
            case Accessibility.Protected:
                return "protected";
            case Accessibility.Internal:
                return "internal";
            case Accessibility.ProtectedOrInternal:
                return "protected internal";
            case Accessibility.Public:
                return "public";
            default:
                return "";
        }
    }

    public static string ToParameterPrefix(this RefKind kind)
    {
        switch (kind)
        {
            case RefKind.Out: return "out ";
            case RefKind.Ref: return "ref ";
            case RefKind.In: return "in ";
            // case RefKind.RefReadOnlyParameter: return "ref readonly ";
            case (RefKind)4: return "ref readonly ";
            case RefKind.None: return "";
            default: return "";
        }
    }

    public static string ToUseParameterPrefix(this RefKind kind)
    {
        switch (kind)
        {
            case RefKind.Out: return "out ";
            case RefKind.Ref: return "ref ";
            case RefKind.In: return "in ";
            case (RefKind)4: return "in "; // ref readonly
            case RefKind.None: return "";
            default: return "";
        }
    }

  
    public static string GetConstraintsString(ImmutableArray<ITypeParameterSymbol> typeParameters)
    {
        var stringBuilder = new StringBuilder();
        foreach (var typeParameter in typeParameters)
        {
            var hasConstraint = false;

            void AppendWhere()
            {
                if (!hasConstraint)
                {
                    hasConstraint = true;
                    stringBuilder.Append(" where ");
                    stringBuilder.Append(typeParameter.Name);
                    stringBuilder.Append(":");
                }
                else
                {
                    stringBuilder.Append(",");
                }

            }
            if (typeParameter.DeclaredAccessibility == Accessibility.NotApplicable)
            {
                if (typeParameter.IsUnmanagedType)
                {
                    AppendWhere();
                    stringBuilder.Append("unmanaged");
                }
                else if (typeParameter.IsReferenceType)
                {
                    AppendWhere();
                    stringBuilder.Append("class");
                }
                else if (typeParameter.IsValueType)
                {
                    AppendWhere();
                    stringBuilder.Append("struct");
                }
            }

            foreach (var constraint in typeParameter.ConstraintTypes)
            {
               
                AppendWhere();
                stringBuilder.Append(constraint.ToFullyQualifiedFormatString());
            }

        }

        return stringBuilder.ToString();
    }
    public static bool TryGetConstraintsString(ImmutableArray<ITypeParameterSymbol> typeParameters,out string result)
    {
        var stringBuilder = new StringBuilder();
        foreach (var typeParameter in typeParameters)
        {
            var hasConstraint = false;

            void AppendWhere()
            {
                if (!hasConstraint)
                {
                    hasConstraint = true;
                    stringBuilder.Append(" where ");
                    stringBuilder.Append(typeParameter.Name);
                    stringBuilder.Append(":");
                }
                else
                {
                    stringBuilder.Append(",");
                }

            }
            {
                if (typeParameter.IsUnmanagedType)
                {
                    AppendWhere();
                    stringBuilder.Append("unmanaged");
                }
                else if (typeParameter.IsReferenceType)
                {
                    AppendWhere();
                    stringBuilder.Append("class");
                }
                else if (typeParameter.IsValueType)
                {
                    AppendWhere();
                    stringBuilder.Append("struct");
                }
            }

            foreach (var constraint in typeParameter.ConstraintTypes)
            {
                if (constraint.DeclaredAccessibility != Accessibility.Public)
                {
                    result = "";
                    return false;
                }
                AppendWhere();
                stringBuilder.Append(constraint.ToFullyQualifiedFormatString());
            }

        }

        result= stringBuilder.ToString();
        return true;
    }
    public static string ToFullyQualifiedFormatString(this ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    public static string ForEachLine<T>(string indent, IEnumerable<T> values, Func<T, string> lineSelector)
    {
        return string.Join(Environment.NewLine, values.Select(x => indent + lineSelector(x)));
    }

    public static string ForLine(string indent, int begin, int end, Func<int, string> lineSelector)
    {
        return string.Join(Environment.NewLine, Enumerable.Range(begin, end - begin).Select(x => indent + lineSelector(x)));
    }

    public static string If(bool condition, string code)
    {
        return condition ? code : "";
    }

    public static string If(bool condition, string ifCode, string elseCode)
    {
        return condition ? ifCode : elseCode;
    }

}
