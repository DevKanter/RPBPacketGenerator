using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using RPBUtilities.Logging.Loggers;
using RPBUtilities.Logging;

namespace RPBPacketGenerator
{
    [Generator]
    public class PacketGenerator : ISourceGenerator
    {
        private enum logenum{
            COMMON
        }
        public void Execute(GeneratorExecutionContext context)
        {
            Logger<logenum>.Initialize(new Dictionary<logenum, IRPBLogger>
            {
                {logenum.COMMON, new FileLogger("generated", LogLevel.FULL)}
            });
#if DEBUG
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }

#endif

            var packets = ReflectiveEnumerator.GetRPBPackets(context).ToList();
            foreach (var namedTypeSymbol in packets) _generatePacketClass(namedTypeSymbol, context);

            var dataList = ReflectiveEnumerator.GetRPBPacketDataList(context).ToList();
            foreach (var namedTypeSymbol in dataList) _generatePacketData(namedTypeSymbol,context);

        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // No initialization required for this one
        }

        private static void _generatePacketClass(INamedTypeSymbol symbol, GeneratorExecutionContext context)
        {
            var fields = symbol.GetMembers().Where(x => x.Kind == SymbolKind.Field)
                .Select(x => (IFieldSymbol) x).Where(fieldSymbol => !fieldSymbol.IsStatic).ToList();

            var sizeMethodBuilder = new StringBuilder();
            var serializeMethodBuilder = new StringBuilder();
            var deserializeMethodBuilder = new StringBuilder();

            for (var i = 0; i < fields.Count; i++)
            {
                var fieldType = fields[i].Type.Name;
                var fieldName = fields[i].Name;
                if (fields[i].Type.TypeKind == TypeKind.Array)
                    fieldType = "Array";
                if (fields[i].Type.TypeKind == TypeKind.Class)
                    fieldType = "Class";
                switch (fieldType)
                {
                    case "Array":
                        var arrayType = (IArrayTypeSymbol)fields[i].Type;
                        switch (arrayType.ElementType.Name)
                        {
                            case "Byte":
                                sizeMethodBuilder.Append($"{fieldName}.Length + 4");
                                deserializeMethodBuilder.Append($"\t    {fieldName} = buffer.ReadBytes()");
                                serializeMethodBuilder.AppendLine($"\t    buffer.Write({fieldName});");
                                break;
                            case "String":
                                sizeMethodBuilder.Append($"4 + {fieldName}.Sum(s => s.Length) + ({fieldName}.Length * 4)");
                                deserializeMethodBuilder.Append($"\t    {fieldName} = buffer.ReadStringArray()");
                                serializeMethodBuilder.AppendLine($"\t    buffer.Write({fieldName});");
                                break;
                        }
                        break;
                    case "String":
                        sizeMethodBuilder.Append($"{fieldName}.Length + 4");
                        deserializeMethodBuilder.Append($"\t    {fieldName} = buffer.ReadString()");
                        serializeMethodBuilder.AppendLine($"\t    buffer.Write({fieldName});");
                        break;
                    case "Class":
                        switch (fields[i].Type.BaseType?.Name)
                        {
                            case "RPBPacketData":
                                sizeMethodBuilder.Append($"{fieldName}.GetSize()");
                                deserializeMethodBuilder.Append($"\t    {fieldName} = new {fieldType}()");
                                serializeMethodBuilder.AppendLine($"\t    {fieldName}.Serialize(buffer);");
                                break;
                        }
                        break;
                    default:
                        sizeMethodBuilder.Append($"sizeof({fieldType})");
                        deserializeMethodBuilder.Append($"\t    {fieldName} = buffer.Read<{fieldType}>()");
                        serializeMethodBuilder.AppendLine($"\t    buffer.Write({fieldName});");
                        break;

                }
                deserializeMethodBuilder.Append(i < fields.Count - 1 ? ",\n" : "\n");
                sizeMethodBuilder.Append(i < fields.Count - 1 ? " + " : ";");
            }

            var className = symbol.Name;
            
            var nameSpace = symbol.ContainingNamespace;
            var baseClass = _getLowestBaseClass(symbol.GetAttributes()[0].AttributeClass);
            var attribute = baseClass.GetAttributes()[0];
            var protocol = attribute.ConstructorArguments[0].Value?.ToString();
            var baseClassString = symbol.GetAttributes()[0].AttributeClass.DeclaringSyntaxReferences[0].SyntaxTree.ToString();
            var index = baseClassString.IndexOf(": base(") +7;
            var index2 = baseClassString.IndexOf(',', index);
            var category = baseClassString.Substring(index, index2 - index);
            var classString = $@"
using System;
using System.Runtime.CompilerServices;
using RPBUtilities;
using RPBPacketBase;

namespace {nameSpace}
{{

public partial class {className}
{{
    private static readonly int _id = BitConverter.ToInt32(new byte[]{{(byte){category},{protocol},0,0}},0);
    public override int PacketId => _id;
    public override int GetSize()
    {{
        return {sizeMethodBuilder}
    }}

    public override void Serialize(ByteBuffer buffer)
    {{
{serializeMethodBuilder}
    }}
    public override T Deserialize<T>(ByteBuffer buffer)
    {{
        return Unsafe.As<T>(new {className}()
        {{
{deserializeMethodBuilder}
        }});
    }}
}}
}}";
            context.AddSource($"{className}.g.cs", classString);
            Logger<logenum>.Log(classString, LogLevel.ERROR, logenum.COMMON);
        }

        private static void _generatePacketData(INamedTypeSymbol symbol, GeneratorExecutionContext context)
        {

        }

        private static INamedTypeSymbol _getLowestBaseClass(INamedTypeSymbol symbol)
        {
            while (symbol.BaseType != null && symbol.BaseType.Name != "Object" && symbol.BaseType.Name != "Attribute")
            {
                symbol = symbol.BaseType;
            }
            return symbol;
        }
    }
}