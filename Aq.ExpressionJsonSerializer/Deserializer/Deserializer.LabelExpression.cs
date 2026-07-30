using System;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer
{
    partial class Deserializer
    {
        private LabelExpression LabelExpression(
            ExpressionType nodeType, Type type, JObject obj)
        {
            var defaultValue = this.Expression(this.Prop(obj, "defaultValue"));
            var targetType = this.Type(this.Prop(obj, "targetType"));
            var targetName = this.Prop(obj, "targetName").Value<string>();

            switch (nodeType) {
                case ExpressionType.Label:
                    return Expr.Label(CreateLabelTarget(targetName, targetType), defaultValue);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
