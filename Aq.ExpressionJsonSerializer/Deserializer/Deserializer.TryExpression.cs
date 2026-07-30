using System;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer
{
    partial class Deserializer
    {
        private TryExpression TryExpression(
            ExpressionType nodeType, Type type, JObject obj)
        {
            var body = Prop(obj, "body", Expression);
            var fault = Prop(obj, "fault", Expression);
            var @finally = Prop(obj, "finally", Expression);
            var handlers = Prop(obj, "handlers", Enumerable(CatchBlock));

            switch (nodeType) {
                case ExpressionType.Try:
                    // A fault handler cannot coexist with catch handlers or a finally
                    // block, and MakeTry enforces that. A tree that was valid when
                    // serialized stays valid, and a payload that is not gets rejected here
                    // rather than being silently reshaped into something that loads.
                    return Expr.MakeTry(type, body, @finally, fault, handlers);
                default:
                    throw new NotSupportedException();
            }
        }

        private CatchBlock CatchBlock(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) {
                return null;
            }

            var obj = (JObject) token;
            var test = Prop(obj, "test", Type);
            var variable = Prop(obj, "variable", ParameterExpression);
            var body = Prop(obj, "body", Expression);
            var filter = Prop(obj, "filter", Expression);

            return Expr.MakeCatchBlock(test, variable, body, filter);
        }
    }
}
