using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Newtonsoft.Json;

namespace Aq.ExpressionJsonSerializer
{
    internal sealed partial class Serializer
    {
        public static void Serialize(
            JsonWriter writer,
            JsonSerializer serializer,
            Expression expression)
        {
            var s = new Serializer(writer, serializer);
            s.ExpressionInternal(expression);
        }

        private readonly JsonWriter _writer;
        private readonly JsonSerializer _serializer;

        private Serializer(JsonWriter writer, JsonSerializer serializer)
        {
            _writer = writer;
            _serializer = serializer;
        }

        private Action Serialize(object value, Type type)
        {
            return () => _serializer.Serialize(_writer, value, type);
        }

        private void Prop(string name, bool value)
        {
            _writer.WritePropertyName(name);
            _writer.WriteValue(value);
        }

        private void Prop(string name, int value)
        {
            _writer.WritePropertyName(name);
            _writer.WriteValue(value);
        }

        private void Prop(string name, string value)
        {
            _writer.WritePropertyName(name);
            _writer.WriteValue(value);
        }

        private void Prop(string name, Action valueWriter)
        {
            _writer.WritePropertyName(name);
            valueWriter();
        }

        private Action Enum<TEnum>(TEnum value)
        {
            return () => EnumInternal(value);
        }

        private void EnumInternal<TEnum>(TEnum value)
        {
            _writer.WriteValue(System.Enum.GetName(typeof(TEnum), value));
        }

        private Action Enumerable<T>(IEnumerable<T> items, Func<T, Action> func)
        {
            return () => EnumerableInternal(items, func);
        }

        private void EnumerableInternal<T>(IEnumerable<T> items, Func<T, Action> func)
        {
            if (items == null) {
                _writer.WriteNull();
            }
            else {
                _writer.WriteStartArray();
                foreach (var item in items) {
                    func(item)();
                }
                _writer.WriteEndArray();
            }
        }

        private Action Expression(Expression expression)
        {
            return () => ExpressionInternal(expression);
        }

        private void ExpressionInternal(Expression expression)
        {
            if (expression == null) {
                _writer.WriteNull();
                return;
            }

            // Refuse dynamic nodes before reducing. DynamicExpression.CanReduce is true,
            // and the reduction rewrites it into a CallSite invocation that carries the
            // CallSiteBinder as a ConstantExpression. Serializing that constant walks the
            // binder's object graph and fails deep inside Newtonsoft with "Self
            // referencing loop detected for property 'ManifestModule'", which tells the
            // caller nothing. Checking first means they get the real reason.
            DynamicExpression(expression);

            while (expression.CanReduce) {
                expression = expression.Reduce();
            }

            _writer.WriteStartObject();

            Prop("nodeType", Enum(expression.NodeType));
            Prop("type", Type(expression.Type));

            // Each handler returns true once it has claimed the node and written its
            // payload. || short-circuits, so exactly one handler runs -- same dispatch
            // the previous `goto end` chain performed.
            var handled =
                BinaryExpression(expression)
                || BlockExpression(expression)
                || ConditionalExpression(expression)
                || ConstantExpression(expression)
                || DebugInfoExpression(expression)
                || DefaultExpression(expression)
                || DynamicExpression(expression)
                || GotoExpression(expression)
                || IndexExpression(expression)
                || InvocationExpression(expression)
                || LabelExpression(expression)
                || LambdaExpression(expression)
                || ListInitExpression(expression)
                || LoopExpression(expression)
                || MemberExpression(expression)
                || MemberInitExpression(expression)
                || MethodCallExpression(expression)
                || NewArrayExpression(expression)
                || NewExpression(expression)
                || ParameterExpression(expression)
                || RuntimeVariablesExpression(expression)
                || SwitchExpression(expression)
                || TryExpression(expression)
                || TypeBinaryExpression(expression)
                || UnaryExpression(expression);

            if (!handled) {
                throw new NotSupportedException(
                    "Unsupported expression node type: " + expression.NodeType);
            }

            _writer.WriteEndObject();
        }
    }
}
