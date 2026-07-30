using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Aq.ExpressionJsonSerializer
{
    partial class Serializer
    {
        private readonly ConcurrentDictionary<ParameterExpression, string>
            _parameterExpressions = new ConcurrentDictionary<ParameterExpression, string>();

        private bool ParameterExpression(Expression expr)
        {
            var expression = expr as ParameterExpression;
            if (expression == null) { return false; }

            string name;
            if (!_parameterExpressions.TryGetValue(expression, out name)) {
                // Reduce() in ExpressionInternal rewrites compound assignment and
                // increment/decrement into temporaries that have no Name. Writing a null
                // name produced JSON the deserializer could not read back: it keys
                // parameters by name in a ConcurrentDictionary, and TryGetValue(null)
                // throws ArgumentNullException. Synthesise a stable name instead, using
                // the same "#" convention the goto and loop serializers already use for
                // unnamed label targets.
                name = expression.Name ?? "#" + expression.GetHashCode();
                _parameterExpressions[expression] = name;
            }

            Prop("typeName", "parameter");
            Prop("name", name);

            return true;
        }
    }
}
