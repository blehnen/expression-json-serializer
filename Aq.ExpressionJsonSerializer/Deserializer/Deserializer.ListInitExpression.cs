using System;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer
{
    partial class Deserializer
    {
        private ListInitExpression ListInitExpression(
            ExpressionType nodeType, Type type, JObject obj)
        {
            var newExpression = Prop(obj, "newExpression", Expression) as NewExpression;
            var initializers = Prop(obj, "initializers", Enumerable(ElementInit));

            switch (nodeType) {
                case ExpressionType.ListInit:
                    return Expr.ListInit(newExpression, initializers);
                default:
                    throw new NotSupportedException();
            }
        }

        private ElementInit ElementInit(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) {
                return null;
            }

            var obj = (JObject) token;
            var addMethod = Prop(obj, "addMethod", Method);
            var arguments = Prop(obj, "arguments", Enumerable(Expression));

            return Expr.ElementInit(addMethod, arguments);
        }
    }
}
