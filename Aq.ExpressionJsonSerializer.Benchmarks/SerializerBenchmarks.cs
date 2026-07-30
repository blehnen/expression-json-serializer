using System;
using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Newtonsoft.Json;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer.Benchmarks
{
    /// <summary>
    /// Measures per-call serialize/deserialize cost and allocation.
    ///
    /// The question this exists to answer: Serializer and Deserializer are constructed
    /// fresh on every call and never shared across threads, yet each holds its parameter
    /// and label-target maps in a ConcurrentDictionary. ConcurrentDictionary's default
    /// constructor allocates a lock array sized from Environment.ProcessorCount, so the
    /// cost is paid per operation on a path DotNetWorkQueue hits for every LINQ message.
    ///
    /// Allocated bytes is the number that matters here, more than mean time.
    /// </summary>
    [MemoryDiagnoser]
    public class SerializerBenchmarks
    {
        private JsonSerializerSettings _settings;
        private LambdaExpression _predicate;
        private LambdaExpression _statements;
        private string _predicateJson;
        private string _statementsJson;

        [GlobalSetup]
        public void Setup()
        {
            _settings = new JsonSerializerSettings();
            _settings.Converters.Add(new ExpressionJsonConverter(
                Assembly.GetAssembly(typeof(SerializerBenchmarks))));

            _predicate = BuildPredicate();
            _statements = BuildStatements();

            _predicateJson = JsonConvert.SerializeObject(_predicate, _settings);
            _statementsJson = JsonConvert.SerializeObject(_statements, _settings);
        }

        /// <summary>Shape a DotNetWorkQueue LINQ message actually looks like.</summary>
        private static LambdaExpression BuildPredicate()
        {
            Expression<Func<Message, bool>> e = m => m.Value > 10 && m.Name != null;
            return e;
        }

        /// <summary>
        /// Statement tree with a block, a loop, a break label and several locals -- the
        /// shape that exercises both the parameter map and the label-target map.
        /// </summary>
        private static LambdaExpression BuildStatements()
        {
            var m = Expr.Parameter(typeof(Message), "m");
            var i = Expr.Variable(typeof(int), "i");
            var total = Expr.Variable(typeof(int), "total");
            var breakLabel = Expr.Label(typeof(int), "break");

            var body = Expr.Block(
                new[] { i, total },
                Expr.Assign(i, Expr.Constant(0)),
                Expr.Assign(total, Expr.Field(m, "Value")),
                Expr.Loop(
                    Expr.IfThenElse(
                        Expr.LessThan(i, Expr.Constant(10)),
                        Expr.Block(
                            Expr.PostIncrementAssign(i),
                            Expr.AddAssign(total, i)),
                        Expr.Break(breakLabel, total)),
                    breakLabel));

            return Expr.Lambda(body, m);
        }

        [Benchmark(Description = "Serialize predicate")]
        public string SerializePredicate()
        {
            return JsonConvert.SerializeObject(_predicate, _settings);
        }

        [Benchmark(Description = "Deserialize predicate")]
        public object DeserializePredicate()
        {
            return JsonConvert.DeserializeObject<LambdaExpression>(_predicateJson, _settings);
        }

        [Benchmark(Description = "Serialize statements")]
        public string SerializeStatements()
        {
            return JsonConvert.SerializeObject(_statements, _settings);
        }

        [Benchmark(Description = "Deserialize statements")]
        public object DeserializeStatements()
        {
            return JsonConvert.DeserializeObject<LambdaExpression>(_statementsJson, _settings);
        }

        public sealed class Message
        {
            public int Value;
            public string Name { get; set; }
        }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<SerializerBenchmarks>(null, args);
        }
    }
}
