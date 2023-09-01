using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace RPBPacketGenerator
{
    public static class ReflectiveEnumerator
    {

        public static IEnumerable<INamedTypeSymbol> GetEnumerableOfType(GeneratorExecutionContext context)
        {
            //var list = context.Compilation.SourceModule.ReferencedAssemblySymbols;
            //foreach (var symbol in list)
            //{
            //    var x = symbol.GetForwardedTypes();
            //    var y = symbol.GlobalNamespace;
            //    var i = y.GetNamespaceMembers();
            //}
            var objects = context.Compilation.GetSymbolsWithName(x => true, SymbolFilter.Type).Select(y => (INamedTypeSymbol)y)
                .Where(x => x.GetAttributes().Any(attr=>attr.AttributeClass?.Name == "Packet"))
                .ToList();
            return objects;
        }
    }
}