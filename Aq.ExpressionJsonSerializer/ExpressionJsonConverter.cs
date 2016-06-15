using System;
using System.Linq.Expressions;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aq.ExpressionJsonSerializer
{
    public class ExpressionJsonConverter : JsonConverter
    {
        private static readonly Type TypeOfExpression = typeof (Expression);

        public ExpressionJsonConverter(Assembly resolvingAssembly)
        {
            _assembly = resolvingAssembly;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == TypeOfExpression
                || objectType.IsSubclassOf(TypeOfExpression);
        }

        public override void WriteJson(
            JsonWriter writer, object value, JsonSerializer serializer)
        {
            Serializer.Serialize(writer, serializer, (Expression) value);
        }

        public override object ReadJson(
            JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            return Deserializer.Deserialize(
                _assembly, JToken.ReadFrom(reader)
            );
        }

        private readonly Assembly _assembly;
    }
}
