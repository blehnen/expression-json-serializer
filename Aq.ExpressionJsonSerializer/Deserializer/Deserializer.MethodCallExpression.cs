﻿using System;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer
{
    partial class Deserializer
    {
        private MethodCallExpression MethodCallExpression(
            ExpressionType nodeType, Type type, JObject obj)
        {
            var instance = Prop(obj, "object", Expression);
            var method = Prop(obj, "method", Method);
            var arguments = Prop(obj, "arguments", Enumerable(Expression));

            switch (nodeType) {
                case ExpressionType.ArrayIndex:
                    return Expr.ArrayIndex(instance, arguments);
                case ExpressionType.Call:
                    return Expr.Call(instance, method, arguments);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
