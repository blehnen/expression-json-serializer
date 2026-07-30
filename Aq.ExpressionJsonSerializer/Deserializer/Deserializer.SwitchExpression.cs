using System;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer
{
    partial class Deserializer
    {
        private SwitchExpression SwitchExpression(
            ExpressionType nodeType, Type type, JObject obj)
        {
            var switchValue = Prop(obj, "switchValue", Expression);
            var defaultBody = Prop(obj, "defaultBody", Expression);
            var comparison = Prop(obj, "comparison", Method);
            var cases = Prop(obj, "cases", Enumerable(SwitchCase));

            switch (nodeType) {
                case ExpressionType.Switch:
                    return Expr.Switch(type, switchValue, defaultBody, comparison, cases);
                default:
                    throw new NotSupportedException();
            }
        }

        private SwitchCase SwitchCase(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) {
                return null;
            }

            var obj = (JObject) token;
            var body = Prop(obj, "body", Expression);
            var testValues = Prop(obj, "testValues", Enumerable(Expression));

            return Expr.SwitchCase(body, testValues);
        }
    }
}
