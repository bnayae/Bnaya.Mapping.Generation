using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class GenerationInput
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationInput"/> class.
    /// </summary>
    /// <param name="syntax">The syntax can be class or record declaration syntax.</param>
    /// <param name="symbol">The symbol.</param>
    /// <exception cref="System.NullReferenceException">symbol</exception>
    public GenerationInput(TypeDeclarationSyntax syntax, INamedTypeSymbol? symbol)
    {
        Syntax = syntax;
        Symbol = symbol ?? throw new NullReferenceException(nameof(symbol));
    }

    public TypeDeclarationSyntax Syntax { get; }
    public INamedTypeSymbol Symbol { get; }
}
