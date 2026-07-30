using System;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer
{
    partial class Deserializer
    {
        private MemberInitExpression MemberInitExpression(
            ExpressionType nodeType, Type type, JObject obj)
        {
            var newExpression = Prop(obj, "newExpression", Expression) as NewExpression;
            var bindings = Prop(obj, "bindings", Enumerable(MemberBinding));

            switch (nodeType) {
                case ExpressionType.MemberInit:
                    return Expr.MemberInit(newExpression, bindings);
                default:
                    throw new NotSupportedException();
            }
        }

        private MemberBinding MemberBinding(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) {
                return null;
            }

            var obj = (JObject) token;
            var bindingType = Prop(obj, "bindingType", Enum<MemberBindingType>);
            var member = Prop(obj, "member", Member);

            switch (bindingType) {
                case MemberBindingType.Assignment:
                    return Expr.Bind(member, Prop(obj, "expression", Expression));
                case MemberBindingType.ListBinding:
                    return Expr.ListBind(member,
                        Prop(obj, "initializers", Enumerable(ElementInit)));
                case MemberBindingType.MemberBinding:
                    return Expr.MemberBind(member,
                        Prop(obj, "bindings", Enumerable(MemberBinding)));
                default:
                    throw new NotSupportedException(
                        "Unsupported member binding type: " + bindingType);
            }
        }
    }
}
