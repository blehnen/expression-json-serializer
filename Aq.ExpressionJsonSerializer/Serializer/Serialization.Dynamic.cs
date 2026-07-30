using System;
using System.Linq.Expressions;

namespace Aq.ExpressionJsonSerializer
{
    partial class Serializer
    {
        private bool DynamicExpression(Expression expr)
        {
            var expression = expr as DynamicExpression;
            if (expression == null) { return false; }

            throw new NotSupportedException(
                "DynamicExpression cannot be serialized. Its Binder is a CallSiteBinder, a "
                + "runtime object encoding language-specific call-site semantics together "
                + "with its own cache state. There is no general way to write one out and "
                + "rebuild an equivalent binder in another process. Resolve the dynamic "
                + "call to a concrete MethodCallExpression before serializing.");
        }
    }
}
