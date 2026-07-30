using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Aq.ExpressionJsonSerializer
{
    partial class Serializer
    {
        // Per-call state. A Serializer is constructed by Serialize and never escapes to
        // another thread, so a plain Dictionary is sufficient and avoids the lock array
        // ConcurrentDictionary allocates from Environment.ProcessorCount on every call.
        // The shared TypeCache in Serializer.Reflection stays concurrent.
        private readonly Dictionary<ParameterExpression, string>
            _parameterExpressions = new Dictionary<ParameterExpression, string>();

        private bool ParameterExpression(Expression expr)
        {
            var expression = expr as ParameterExpression;
            if (expression == null) { return false; }

            string name;
            if (!_parameterExpressions.TryGetValue(expression, out name)) {
                // Reduce() in ExpressionInternal rewrites compound assignment and
                // increment/decrement into temporaries that have no Name. Writing a null
                // name produced JSON the deserializer could not read back: it keys
                // parameters by name in a dictionary, and TryGetValue(null)
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
