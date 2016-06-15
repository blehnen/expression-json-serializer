using System;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer
{
    partial class Deserializer
    {
        private LoopExpression LoopExpression(
            ExpressionType nodeType, Type type, JObject obj)
        {
            throw new NotImplementedException();
        }
    }
}
