using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer.Tests
{
    /// <summary>
    /// Coverage for the reflection resolution layer: the constructor/method/property
    /// caches, and the failure paths taken when a payload names something that no longer
    /// exists. The failure paths matter because deserializing an expression tree binds to
    /// members by name and signature, so a payload written against a different build of
    /// the target assembly lands here.
    /// </summary>
    public partial class ExpressionJsonSerializerTest
    {
        private static JsonSerializerSettings ReflectionSettings()
        {
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new ExpressionJsonConverter(
                Assembly.GetAssembly(typeof(ExpressionJsonSerializerTest))));
            return settings;
        }

        /// <summary>Serialize, mutate the JSON, then deserialize the tampered payload.</summary>
        private static LambdaExpression RoundTripWith(
            LambdaExpression source, Action<JObject> tamper)
        {
            var settings = ReflectionSettings();
            var obj = JObject.Parse(JsonConvert.SerializeObject(source, settings));
            tamper(obj);
            return JsonConvert.DeserializeObject<LambdaExpression>(obj.ToString(), settings);
        }

        private static void Retarget(JObject root, string property, string newValue)
        {
            foreach (var token in root.Descendants()) {
                var o = token as JObject;
                if (o != null && o[property] != null && o[property].Type == JTokenType.String) {
                    o[property] = newValue;
                }
            }
        }

        // ---- Failure paths ------------------------------------------------------------

        [Fact]
        public void UnresolvableTypeThrowsTypeLoadException()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var source = Expr.Lambda(Expr.Field(c, "A"), c);

            var ex = Assert.Throws<TypeLoadException>(
                () => RoundTripWith(source, o => Retarget(o, "typeName", "Nope.NotAType")));

            Assert.Contains("Type could not be found", ex.Message);
        }

        [Fact]
        public void UnresolvableConstructorThrowsMissingMethodException()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            // A New node so the payload carries a constructor signature to corrupt.
            var source = Expr.Lambda(
                Expr.Property(
                    Expr.ListInit(Expr.New(typeof(List<int>)), Expr.Constant(1)),
                    "Count"),
                c);

            var ex = Assert.Throws<MissingMethodException>(
                () => RoundTripWith(source, o => Retarget(o, "signature", "Void .ctor(System.Guid)")));

            Assert.Contains("could not be found", ex.Message);
        }

        // ---- Cache paths --------------------------------------------------------------

        [Fact]
        public void DistinctConstructorsOnSameTypeUseTheSignatureCache()
        {
            // Two different constructors on one type exercise the second cache tier
            // (per-type -> per-name -> per-signature) rather than only the first.
            var c = Expr.Parameter(typeof(Context), "c");

            TestExpression(Expr.Lambda(
                Expr.Property(Expr.New(typeof(List<int>)), "Count"), c));

            TestExpression(Expr.Lambda(
                Expr.Property(
                    Expr.New(
                        typeof(List<int>).GetConstructor(new[] { typeof(int) }),
                        Expr.Constant(8)),
                    "Capacity"),
                c));
        }

        [Fact]
        public void RepeatedDeserializationHitsWarmCaches()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var source = Expr.Lambda(Expr.Call(c, typeof(Context).GetMethod("Method", Type.EmptyTypes)), c);

            // Same tree twice: the second pass resolves entirely from the warm caches.
            TestExpression(source);
            TestExpression(source);
        }

        // ---- New expression shapes ----------------------------------------------------

        [Fact]
        public void NewWithArgumentsAndMembers()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var ctor = typeof(Pair).GetConstructor(new[] { typeof(int), typeof(int) });

            var init = Expr.New(
                ctor,
                new Expression[] { Expr.Constant(3), Expr.Constant(4) },
                typeof(Pair).GetProperty("X"),
                typeof(Pair).GetProperty("Y"));

            TestExpression(Expr.Lambda(
                Expr.Add(Expr.Property(init, "X"), Expr.Property(init, "Y")), c));
        }

        [Fact]
        public void NewParameterlessConstructor()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            TestExpression(Expr.Lambda(
                Expr.Property(Expr.New(typeof(List<int>)), "Count"), c));
        }

        // ---- Generic method resolution -------------------------------------------------

        [Fact]
        public void GenericMethodCall()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var method = typeof(ExpressionJsonSerializerTest)
                .GetMethod("Identity", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(typeof(int));

            TestExpression(Expr.Lambda(Expr.Call(method, Expr.Field(c, "A")), c));
        }

        public static T Identity<T>(T value)
        {
            return value;
        }

        public sealed class Pair
        {
            public Pair(int x, int y) { X = x; Y = y; }
            public int X { get; private set; }
            public int Y { get; private set; }
        }
    }
}
