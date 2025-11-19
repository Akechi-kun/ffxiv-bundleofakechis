using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace InteropSourceGenerators.Extensions;

internal static class TypeDeclarationSyntaxExtensions
{
    public static string GetNameWithTypeDeclarationList(this TypeDeclarationSyntax typeDeclarationSyntax)
    {
        return typeDeclarationSyntax.Identifier.ToString() + typeDeclarationSyntax.TypeParameterList;
    }
}
