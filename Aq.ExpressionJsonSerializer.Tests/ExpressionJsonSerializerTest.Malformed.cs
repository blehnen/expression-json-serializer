using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer.Tests
{
    /// <summary>
    /// Behaviour on payloads the serializer would never produce. Deserialization binds to
    /// whatever the JSON names, so a payload from a different version -- or a hand-written
    /// one -- can reach node combinations the round trip never exercises. These pin the
    /// rejection path rather than leaving it to chance.
    /// </summary>
    public partial class ExpressionJsonSerializerTest
    {
        private static void DeserializeTampered(LambdaExpression source, Action<JObject> tamper)
        {
            var settings = ReflectionSettings();
            var obj = JObject.Parse(JsonConvert.SerializeObject(source, settings));
            tamper(obj);
            JsonConvert.DeserializeObject<LambdaExpression>(obj.ToString(), settings);
        }

        private static void SetNodeTypeWhere(JObject root, string typeName, string nodeType)
        {
            foreach (var token in root.Descendants()) {
                var o = token as JObject;
                if (o != null && (string) o["typeName"] == typeName) {
                    o["nodeType"] = nodeType;
                }
            }
        }

        /// <summary>
        /// Relabels a node so the deserializer dispatches it somewhere the serializer would
        /// never send it. The only way to reach handlers for nodes this library refuses to
        /// write, short of hand-building a whole payload.
        /// </summary>
        private static void SetTypeNameWhere(JObject root, string from, string to)
        {
            foreach (var token in root.Descendants()) {
                var o = token as JObject;
                if (o != null && (string) o["typeName"] == from) {
                    o["typeName"] = to;
                }
            }
        }

        [Fact]
        public void UnknownTypeNameIsRejected()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var source = Expr.Lambda(Expr.Add(Expr.Field(c, "A"), Expr.Constant(1)), c);

            Assert.Throws<NotSupportedException>(() => DeserializeTampered(source, o => {
                foreach (var t in o.Descendants()) {
                    var obj = t as JObject;
                    if (obj != null && (string) obj["typeName"] == "binary") {
                        obj["typeName"] = "somethingElse";
                    }
                }
            }));
        }

        [Fact]
        public void BinaryNodeWithUnsupportedNodeTypeIsRejected()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var source = Expr.Lambda(Expr.Add(Expr.Field(c, "A"), Expr.Constant(1)), c);

            Assert.Throws<NotSupportedException>(
                () => DeserializeTampered(source, o => SetNodeTypeWhere(o, "binary", "Block")));
        }

        [Fact]
        public void UnaryNodeWithUnsupportedNodeTypeIsRejected()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var source = Expr.Lambda(Expr.Negate(Expr.Field(c, "A")), c);

            Assert.Throws<NotSupportedException>(
                () => DeserializeTampered(source, o => SetNodeTypeWhere(o, "unary", "Block")));
        }

        [Fact]
        public void ListInitNodeWithUnsupportedNodeTypeIsRejected()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var source = Expr.Lambda(
                Expr.Property(
                    Expr.ListInit(Expr.New(typeof(List<int>)), Expr.Constant(1)),
                    "Count"),
                c);

            Assert.Throws<NotSupportedException>(
                () => DeserializeTampered(source, o => SetNodeTypeWhere(o, "listInit", "Block")));
        }

        [Fact]
        public void MemberInitNodeWithUnsupportedNodeTypeIsRejected()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var source = Expr.Lambda(MemberInitBody(c), c);

            Assert.Throws<NotSupportedException>(
                () => DeserializeTampered(source, o => SetNodeTypeWhere(o, "memberInit", "Block")));
        }

        [Fact]
        public void UnknownMemberBindingTypeIsRejected()
        {
            // MemberBindingType has three values and all three are handled, so the only way
            // to reach the default arm is a payload naming a value outside the enum.
            // Enum.Parse accepts a bare number, which is how a foreign or corrupted payload
            // would present one.
            var c = Expr.Parameter(typeof(Context), "c");
            var source = Expr.Lambda(MemberInitBody(c), c);

            var ex = Assert.Throws<NotSupportedException>(
                () => DeserializeTampered(source, o => {
                    foreach (var token in o.Descendants()) {
                        var obj = token as JObject;
                        if (obj != null && obj["bindingType"] != null) {
                            obj["bindingType"] = "99";
                        }
                    }
                }));

            Assert.Contains("Unsupported member binding type", ex.Message);
        }

        // ---- Null reflection payloads --------------------------------------------------

        [Fact]
        public void ArrayAccessHasNoIndexerProperty()
        {
            // Expr.ArrayAccess builds an IndexExpression whose Indexer is null, which is the
            // only way the serializer writes a null property payload.
            var c = Expr.Parameter(typeof(Context), "c");
            var source = Expr.Lambda(
                Expr.ArrayAccess(Expr.Field(c, "Array"), Expr.Constant(0)), c);

            var json = JObject.Parse(JsonConvert.SerializeObject(source, ReflectionSettings()));
            var index = FindByTypeName(json, "index");
            Assert.NotNull(index);
            Assert.Equal(JTokenType.Null, index["indexer"].Type);

            TestExpression(source);
        }

        [Fact]
        public void ValueTypeNewHasNoConstructor()
        {
            // A parameterless value-type construction has a null ConstructorInfo. The
            // serializer writes null for it; the deserializer cannot rebuild the node from
            // that, so this documents a genuine round-trip limitation rather than asserting
            // success.
            var c = Expr.Parameter(typeof(Context), "c");
            var source = Expr.Lambda(Expr.New(typeof(int)), c);

            var settings = ReflectionSettings();
            var json = JObject.Parse(JsonConvert.SerializeObject(source, settings));
            var node = FindByTypeName(json, "new");
            Assert.NotNull(node);
            Assert.Equal(JTokenType.Null, node["constructor"].Type);

            Assert.ThrowsAny<Exception>(
                () => JsonConvert.DeserializeObject<LambdaExpression>(json.ToString(), settings));
        }

        private static JObject FindByTypeName(JObject root, string typeName)
        {
            if ((string) root["typeName"] == typeName) { return root; }
            foreach (var token in root.Descendants()) {
                var o = token as JObject;
                if (o != null && (string) o["typeName"] == typeName) { return o; }
            }
            return null;
        }
    }
}
