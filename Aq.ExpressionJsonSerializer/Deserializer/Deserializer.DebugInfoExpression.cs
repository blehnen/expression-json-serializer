using System;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer
{
    partial class Deserializer
    {
        private DebugInfoExpression DebugInfoExpression(
            ExpressionType nodeType, Type type, JObject obj)
        {
            // Unreachable through this library's own output, since the serializer refuses
            // to write these. Kept so a hand-written or foreign payload gets the same
            // explanation rather than a NullReferenceException further down.
            throw new NotSupportedException(
                "DebugInfoExpression cannot be deserialized. It carries a "
                + "SymbolDocumentInfo describing a source file in the assembly the tree was "
                + "built from, which cannot be meaningfully reconstructed here. See "
                + "Serialization.DebugInfo for the full reasoning.");
        }
    }
}
