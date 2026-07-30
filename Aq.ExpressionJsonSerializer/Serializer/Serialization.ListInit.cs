using System;
using System.Linq.Expressions;

namespace Aq.ExpressionJsonSerializer
{
    partial class Serializer
    {
        private bool ListInitExpression(Expression expr)
        {
            // This tested DefaultExpression until it was corrected, so it never matched
            // the node it is named for.
            var expression = expr as ListInitExpression;
            if (expression == null) { return false; }

            Prop("typeName", "listInit");
            Prop("newExpression", Expression(expression.NewExpression));
            Prop("initializers", Enumerable(expression.Initializers, ElementInit));

            return true;
        }

        // ElementInit is not an Expression, so it gets its own writer rather than going
        // through the node dispatch.
        private Action ElementInit(ElementInit initializer)
        {
            return () => ElementInitInternal(initializer);
        }

        private void ElementInitInternal(ElementInit initializer)
        {
            if (initializer == null) {
                _writer.WriteNull();
                return;
            }

            _writer.WriteStartObject();
            Prop("addMethod", Method(initializer.AddMethod));
            Prop("arguments", Enumerable(initializer.Arguments, Expression));
            _writer.WriteEndObject();
        }
    }
}
