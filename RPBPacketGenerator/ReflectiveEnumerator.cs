using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace RPBPacketGenerator
{
    public static class ReflectiveEnumerator
    {
        public static IEnumerable<INamedTypeSymbol> GetRPBPackets(GeneratorExecutionContext context)
        {
            var objects = context.Compilation.GetSymbolsWithName(x => true, SymbolFilter.Type)
                .Select(y => (INamedTypeSymbol) y)
                .Where(x => x.GetAttributes().Any(attr => _getLowestBaseClassName(attr.AttributeClass) == "BasePacketAttribute"))
                .ToList();
            return objects;
        }

        public static IEnumerable<INamedTypeSymbol> GetRPBPacketDataList(GeneratorExecutionContext context)
        {
            var objects = context.Compilation.GetSymbolsWithName(x => true, SymbolFilter.Type)
                .Select(y => (INamedTypeSymbol)y)
                .Where(x =>x.BaseType?.Name == "RPBPacketData")
                .ToList();
            return objects;
        }

        private static string _getLowestBaseClassName(INamedTypeSymbol symbol)
        {
            var result = "";
            while (symbol.BaseType != null && symbol.BaseType.Name !="Object" && symbol.BaseType.Name != "Attribute")
            {
                result = symbol.BaseType.Name;
                symbol = symbol.BaseType;
            }
            return result;
        }
    }
}