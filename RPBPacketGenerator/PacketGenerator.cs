using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace RPBPacketGenerator
{
        [Generator]
        public class PacketGenerator : ISourceGenerator
        {
            public void Execute(GeneratorExecutionContext context)
            {
//#if DEBUG
//            if (!Debugger.IsAttached)
//            {
//                Debugger.Launch();
//            }

//#endif

            var packets = ReflectiveEnumerator.GetEnumerableOfType(context).ToList();
                foreach (var namedTypeSymbol in packets)
                {
                    _generatePacketClass(namedTypeSymbol,context);
                }

            }

            public void Initialize(GeneratorInitializationContext context)
            {
                // No initialization required for this one
            }
            private static void _generatePacketClass(INamedTypeSymbol symbol, GeneratorExecutionContext context)
            {
                var fields = symbol.GetMembers().Where(x => x.Kind == SymbolKind.Field)
                    .Select(x => (IFieldSymbol)x).Where(fieldSymbol => !fieldSymbol.IsStatic).ToList();

                var sizeMethodBuilder = new StringBuilder();
                var serializeMethodBuilder = new StringBuilder();
                var deserializeMethodBuilder = new StringBuilder();

                for (var i = 0; i < fields.Count; i++)
                {
                    var fieldType = fields[i].Type.Name;
                    var fieldName = fields[i].Name;
                    if (fields[i].Type.TypeKind == TypeKind.Array)
                        fieldType = "Array";
                    switch (fieldType)
                    {
                        case "String":
                        {
                            sizeMethodBuilder.Append($"{fieldName}.Length + 4");
                            deserializeMethodBuilder.Append($"\t    {fieldName} = buffer.ReadString()");
                            break;
                        }
                        case "Array":
                        {
                            var arrayType = (IArrayTypeSymbol) fields[i].Type;
                            switch (arrayType.ElementType.Name)
                            {
                                case "Byte":
                                {
                                    sizeMethodBuilder.Append($"{fieldName}.Length + 4");
                                    deserializeMethodBuilder.Append($"\t    {fieldName} = buffer.ReadBytes()");
                                    break;
                                }
                            }

                            break;
                        }
                        default:
                        {
                            sizeMethodBuilder.Append($"sizeof({fieldType})");
                            deserializeMethodBuilder.Append($"\t    {fieldName} = buffer.Read<{fieldType}>()");
                            break;
                        }
                    }
                    serializeMethodBuilder.AppendLine($"\t    buffer.Write({fieldName});");
                    deserializeMethodBuilder.Append(i < fields.Count - 1 ? ",\n" : "\n");
                    sizeMethodBuilder.Append(i < fields.Count - 1 ? " + " : ";");

                }
                var className = symbol.Name;
                var attribute = symbol.GetAttributes()[0];
                var nameSpace = symbol.ContainingNamespace;
                var category = attribute.ConstructorArguments[0].Value?.ToString();
                var protocol = attribute.ConstructorArguments[1].Value?.ToString();
            var classString = $@"
using System;
using System.Runtime.CompilerServices;
using RPBUtilities;
using RPBPacketBase;

namespace {nameSpace}
{{

public partial class {className} : RPBPacket
{{
    private static readonly int _id = BitConverter.ToInt32(new byte[]{{{category},{protocol},0,0}},0);
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
            }
        
    }


}
