using System;
using System.Linq.Expressions;

namespace Aq.ExpressionJsonSerializer
{
    partial class Serializer
    {
        private bool SwitchExpression(Expression expr)
        {
            var expression = expr as SwitchExpression;
            if (expression == null) { return false; }

            Prop("typeName", "switch");
            Prop("switchValue", Expression(expression.SwitchValue));
            Prop("defaultBody", Expression(expression.DefaultBody));
            Prop("comparison", Method(expression.Comparison));
            Prop("cases", Enumerable(expression.Cases, SwitchCase));

            return true;
        }

        // SwitchCase is not an Expression, so it gets its own writer rather than going
        // through the node dispatch.
        private Action SwitchCase(SwitchCase switchCase)
        {
            return () => SwitchCaseInternal(switchCase);
        }

        private void SwitchCaseInternal(SwitchCase switchCase)
        {
            if (switchCase == null) {
                _writer.WriteNull();
                return;
            }

            _writer.WriteStartObject();
            Prop("testValues", Enumerable(switchCase.TestValues, Expression));
            Prop("body", Expression(switchCase.Body));
            _writer.WriteEndObject();
        }
    }
}
