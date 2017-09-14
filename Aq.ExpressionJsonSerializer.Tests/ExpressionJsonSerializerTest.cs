using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace Aq.ExpressionJsonSerializer.Tests
{
    public class ExpressionJsonSerializerTest
    {
        [Fact]
        public void Assignment()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.A + c.B));
        }

        [Fact]
        public void BitwiseAnd()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.A & c.B));
        }

        [Fact]
        public void LogicalAnd()
        {
            TestExpression((Expression<Func<Context, bool>>) (c => c.A > 0 && c.B > 0));
        }

        [Fact]
        public void ArrayIndex()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.Array[0]));
        }

        [Fact]
        public void ArrayLength()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.Array.Length));
        }

        [Fact]
        public void Method()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.Method()));
        }

        [Fact]
        public void MethodWithArguments()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.Method("B")));
        }

        [Fact]
        public void Coalesce()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.C ?? c.A));
        }

        [Fact]
        public void Conditional()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.C == null ? c.A : c.B));
        }

        [Fact]
        public void Convert()
        {
            TestExpression((Expression<Func<Context, int>>) (c => (short) (c.C ?? 0)));
        }

        [Fact]
        public void Decrement()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.A - 1));
        }

        [Fact]
        public void DivisionWithCast()
        {
            TestExpression((Expression<Func<Context, float>>) (c => (float) c.A / c.B));
        }

        [Fact]
        public void Equality()
        {
            TestExpression((Expression<Func<Context, bool>>) (c => c.A == c.B));
        }

        [Fact]
        public void Xor()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.A ^ c.B));
        }

        [Fact]
        public void LinqExtensions()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.Array.FirstOrDefault()));
        }

        [Fact]
        public void GreaterThan()
        {
            TestExpression((Expression<Func<Context, bool>>) (c => c.A > c.B));
        }

        [Fact]
        public void Increment()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.A + 1));
        }

        [Fact]
        public void Indexer()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c["A"]));
        }

        [Fact]
        public void Invoke()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.Func()));
        }

        [Fact]
        public void Constant()
        {
            TestExpression((Expression<Func<Context, bool>>) (c => false));
        }

        [Fact]
        public void Lambda()
        {
            TestExpression((Expression<Func<Context, int>>) (c => ((Func<Context, int>) (_ => _.A))(c)));
        }

        [Fact]
        public void LeftShift()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.A << c.C ?? 0));
        }

        [Fact]
        public void PropertyAccess()
        {
            TestExpression((Expression<Func<Context, int>>) (c => c.B));
        }

        [Fact]
        public void Negation()
        {
            TestExpression((Expression<Func<Context, int>>) (c => -c.A));
        }

        [Fact]
        public void New()
        {
            TestExpression((Expression<Func<Context, object>>) (c => new object()));    
        }

        [Fact]
        public void NewWithArguments()
        {
            TestExpression((Expression<Func<Context, object>>) (c => new String('s', 1)));
        }

        [Fact]
        public void InitArray()
        {
            TestExpression((Expression<Func<Context, int[]>>) (c => new[] { 0 }));
        }

        [Fact]
        public void InitEmptyArray()
        {
            TestExpression((Expression<Func<Context, int[,]>>) (c => new int[3, 2]));
        }


#if NETFULL
        [Fact]
        //JSON.net won't handle objects as part of an expression when in .net standard/core
        public void TypeAs()
        {
            TestExpression((Expression<Func<Context, object>>) (c => c as object));
        }
#else
        [Fact]
        //JSON.net won't handle objects as part of an expression when in .net standard/core
        public void TypeAs()
        {
            Assert.Throws<System.Runtime.Serialization.SerializationException>(() => TestExpression((Expression<Func<Context, object>>) (c => c as object)));
        }
#endif

        [Fact]
        public void TypeOf()
        {
            TestExpression((Expression<Func<Context, bool>>) (c => c is Context));
        }

        [Fact]
        public void TypeIs()
        {
            TestExpression((Expression<Func<Context, bool>>) (c => c is object));
        }

        [Fact]
        public void MethodResultCast()
        {
            TestExpression((Expression<Func<Context, int>>) (c => (int) c.Method3()));
        }

        [Fact]
        public void LambdaMultiThreaded()
        {
            var count = 100;
            var tasks = new Task[count];

            Parallel.For(0, count,
                index =>
                {
                    var t =
                        Task.Factory.StartNew(
                            Lambda);
                    tasks[index] = t;
                });

            Task.WaitAll(tasks);
        }

        private sealed class Context
        {
            public int A;
            public int B { get; set; }
            public int? C;
            public int[] Array;
            public int this[string key]
            {
                get
                {
                    switch (key) {
                        case "A": return this.A;
                        case "B": return this.B;
                        case "C": return this.C ?? 0;
                        default: throw new NotImplementedException();
                    }
                }
            }
            public Func<int> Func;
            public int Method() { return this.A; }
            public int Method(string key) { return this[key]; }
            public object Method3() { return this.A; }
        }

        private static void TestExpression(LambdaExpression source)
        {
            var random = new Random();
            int u;
            var context = new Context {
                A = random.Next(),
                B = random.Next(),
                C = (u = random.Next(0, 2)) == 0 ? null : (int?) u,
                Array = new[] { random.Next() },
                Func = () => u
            };

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new ExpressionJsonConverter(
                Assembly.GetAssembly(typeof (ExpressionJsonSerializerTest))
            ));

            var json = JsonConvert.SerializeObject(source, settings);
            var target = JsonConvert.DeserializeObject<LambdaExpression>(json, settings);

            Assert.Equal(
                ExpressionResult(source, context),
                ExpressionResult(target, context)
            );
        }

        private static string ExpressionResult(LambdaExpression expr, Context context)
        {
            return JsonConvert.SerializeObject(expr.Compile().DynamicInvoke(context));
        }
    }
}
