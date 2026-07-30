using System;
using System.Linq.Expressions;

namespace Aq.ExpressionJsonSerializer
{
    partial class Serializer
    {
        private bool TryExpression(Expression expr)
        {
            var expression = expr as TryExpression;
            if (expression == null) { return false; }

            Prop("typeName", "try");
            Prop("body", Expression(expression.Body));
            Prop("fault", Expression(expression.Fault));
            Prop("finally", Expression(expression.Finally));
            Prop("handlers", Enumerable(expression.Handlers, CatchBlock));

            return true;
        }

        // CatchBlock is not an Expression, so it gets its own writer rather than going
        // through the node dispatch. The variable is written as a normal parameter node so
        // it shares the unnamed-parameter naming in Serialization.Parameter.
        private Action CatchBlock(CatchBlock catchBlock)
        {
            return () => CatchBlockInternal(catchBlock);
        }

        private void CatchBlockInternal(CatchBlock catchBlock)
        {
            if (catchBlock == null) {
                _writer.WriteNull();
                return;
            }

            _writer.WriteStartObject();
            Prop("test", Type(catchBlock.Test));
            Prop("variable", Expression(catchBlock.Variable));
            Prop("body", Expression(catchBlock.Body));
            Prop("filter", Expression(catchBlock.Filter));
            _writer.WriteEndObject();
        }
    }
}
