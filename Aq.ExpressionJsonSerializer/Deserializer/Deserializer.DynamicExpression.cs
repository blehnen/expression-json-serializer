using System;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer
{
    partial class Deserializer
    {
        private DynamicExpression DynamicExpression(
            ExpressionType nodeType, Type type, JObject obj)
        {
            // Unreachable through this library's own output, since the serializer refuses
            // to write these. Kept so a hand-written or foreign payload gets the same
            // explanation rather than a NullReferenceException further down.
            throw new NotSupportedException(
                "DynamicExpression cannot be deserialized. Rebuilding one requires a "
                + "CallSiteBinder, which is a runtime object with no serializable form. See "
                + "Serialization.Dynamic for the full reasoning.");
        }
    }
}
