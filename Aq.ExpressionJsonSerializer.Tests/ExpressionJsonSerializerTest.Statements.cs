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
    /// Coverage for the statement-level expression nodes. These cannot be produced by C#
    /// lambda syntax, so unlike the tests in the sibling file they are built by hand with
    /// the <see cref="Expression"/> factory methods and wrapped in a lambda for the
    /// round-trip harness.
    ///
    /// Loop, goto, label and block are the nodes this fork exists to support, so they are
    /// covered first.
    /// </summary>
    public partial class ExpressionJsonSerializerTest
    {
        private static ParameterExpression Ctx()
        {
            return Expr.Parameter(typeof(Context), "c");
        }

        private static void TestBody(ParameterExpression ctx, Expression body)
        {
            TestExpression(Expr.Lambda(body, ctx));
        }

        /// <summary>
        /// Round-trip fidelity check that never calls Compile(): serialize, deserialize,
        /// serialize again, and require the two payloads to match.
        ///
        /// Needed because .NET Framework's DynamicMethod cannot emit fault blocks or
        /// exception filters -- DynamicILGenerator.BeginFaultBlock and
        /// BeginExceptFilterBlock throw "The requested operation is invalid for
        /// DynamicMethod". That is a runtime limit on executing those constructs at all,
        /// not a serializer limit, and it would fire on the source expression before any
        /// serialization happened. Comparing payloads still proves this library preserved
        /// the tree.
        /// </summary>
        private static void TestBodyWithoutCompiling(ParameterExpression ctx, Expression body)
        {
            var source = Expr.Lambda(body, ctx);

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new ExpressionJsonConverter(
                Assembly.GetAssembly(typeof(ExpressionJsonSerializerTest))));

            var first = JsonConvert.SerializeObject(source, settings);
            var target = JsonConvert.DeserializeObject<LambdaExpression>(first, settings);

            Assert.NotNull(target);
            Assert.Equal(source.Body.NodeType, target.Body.NodeType);
            Assert.Equal(first, JsonConvert.SerializeObject(target, settings));
        }

        // ---- Block -------------------------------------------------------------------

        [Fact]
        public void BlockWithLocalVariable()
        {
            var c = Ctx();
            var v = Expr.Variable(typeof(int), "v");

            TestBody(c, Expr.Block(
                new[] { v },
                Expr.Assign(v, Expr.Field(c, "A")),
                Expr.AddAssign(v, Expr.Constant(1)),
                v));
        }

        [Fact]
        public void BlockWithoutVariables()
        {
            var c = Ctx();

            TestBody(c, Expr.Block(
                Expr.Constant(1),
                Expr.Field(c, "A")));
        }

        // ---- Loop / goto / label -----------------------------------------------------

        [Fact]
        public void LoopWithBreak()
        {
            var c = Ctx();
            var i = Expr.Variable(typeof(int), "i");
            var breakLabel = Expr.Label(typeof(int), "break");

            // for (i = 0; i < 5; i++) ; return i;
            TestBody(c, Expr.Block(
                new[] { i },
                Expr.Assign(i, Expr.Constant(0)),
                Expr.Loop(
                    Expr.IfThenElse(
                        Expr.LessThan(i, Expr.Constant(5)),
                        Expr.PostIncrementAssign(i),
                        Expr.Break(breakLabel, i)),
                    breakLabel)));
        }

        [Fact]
        public void LoopWithBreakAndContinue()
        {
            var c = Ctx();
            var i = Expr.Variable(typeof(int), "i");
            var total = Expr.Variable(typeof(int), "total");
            var breakLabel = Expr.Label(typeof(int), "break");
            var continueLabel = Expr.Label("continue");

            // Exercises the three-argument Expr.Loop overload, i.e. the continue-label
            // branch of Deserializer.LoopExpression.
            TestBody(c, Expr.Block(
                new[] { i, total },
                Expr.Assign(i, Expr.Constant(0)),
                Expr.Assign(total, Expr.Constant(0)),
                Expr.Loop(
                    Expr.IfThenElse(
                        Expr.LessThan(i, Expr.Constant(5)),
                        Expr.Block(
                            Expr.PostIncrementAssign(i),
                            Expr.AddAssign(total, i),
                            Expr.Continue(continueLabel)),
                        Expr.Break(breakLabel, total)),
                    breakLabel,
                    continueLabel)));
        }

        // ---- Deliberately unsupported nodes ------------------------------------------
        //
        // DebugInfoExpression and DynamicExpression are not gaps to be filled. Neither has
        // a serializable form: DebugInfo carries a SymbolDocumentInfo describing a source
        // file in the originating assembly, and Dynamic carries a CallSiteBinder, a
        // runtime object with per-site cache state. Both throw NotSupportedException with
        // an explanation of why and what to do instead.

        [Fact]
        public void DebugInfoIsRejectedWithAnExplanation()
        {
            var c = Ctx();
            var doc = Expr.SymbolDocument("Source.cs");

            var body = Expr.Block(
                Expr.DebugInfo(doc, 1, 1, 1, 20),
                Expr.Field(c, "A"));

            var ex = Assert.Throws<NotSupportedException>(() => TestBody(c, body));
            Assert.Contains("DebugInfoExpression cannot be serialized", ex.Message);
            Assert.Contains("SymbolDocumentInfo", ex.Message);
        }

        [Fact]
        public void DynamicIsRejectedWithAnExplanation()
        {
            var c = Ctx();

            // A CallSiteBinder is normally supplied by a language binder such as
            // Microsoft.CSharp, but the base class is public and abstract, so a minimal
            // one is enough to build a real DynamicExpression without that dependency.
            var body = Expr.Dynamic(new StubBinder(), typeof(int), Expr.Field(c, "A"));

            var ex = Assert.Throws<NotSupportedException>(() => TestBody(c, body));
            Assert.Contains("DynamicExpression cannot be serialized", ex.Message);
            Assert.Contains("CallSiteBinder", ex.Message);
        }

        private sealed class StubBinder : System.Runtime.CompilerServices.CallSiteBinder
        {
            public override Expression Bind(
                object[] args,
                System.Collections.ObjectModel.ReadOnlyCollection<ParameterExpression> parameters,
                LabelTarget returnLabel)
            {
                return Expr.Return(returnLabel, Expr.Constant(0));
            }
        }

        [Fact]
        public void DebugInfoPayloadIsRejectedOnRead()
        {
            // The serializer never emits one, so reach the deserializer path the only way
            // a real caller could: a payload written by something else.
            var c = Ctx();
            var source = Expr.Lambda(Expr.Field(c, "A"), c);

            var ex = Assert.Throws<NotSupportedException>(
                () => DeserializeTampered(source, o => SetTypeNameWhere(o, "member", "debugInfo")));

            Assert.Contains("DebugInfoExpression cannot be deserialized", ex.Message);
        }

        [Fact]
        public void DynamicPayloadIsRejectedOnRead()
        {
            var c = Ctx();
            var source = Expr.Lambda(Expr.Field(c, "A"), c);

            var ex = Assert.Throws<NotSupportedException>(
                () => DeserializeTampered(source, o => SetTypeNameWhere(o, "member", "dynamic")));

            Assert.Contains("DynamicExpression cannot be deserialized", ex.Message);
        }

        // ---- Label (goto landing sites) ----------------------------------------------

        [Fact]
        public void GotoJumpOverStatement()
        {
            var c = Ctx();
            var v = Expr.Variable(typeof(int), "v");
            var target = Expr.Label("skip");

            TestBody(c, Expr.Block(
                new[] { v },
                Expr.Assign(v, Expr.Constant(1)),
                Expr.Goto(target),
                Expr.Assign(v, Expr.Constant(2)),   // jumped over
                Expr.Label(target),
                v));
        }

        [Fact]
        public void LabelWithDefaultValue()
        {
            var c = Ctx();
            var target = Expr.Label(typeof(int), "result");

            TestBody(c, Expr.Block(
                Expr.Goto(target, Expr.Field(c, "A")),
                Expr.Label(target, Expr.Constant(-1))));
        }

        [Fact]
        public void LabelDefaultValueIsUsedWhenNotJumpedTo()
        {
            var c = Ctx();
            var target = Expr.Label(typeof(int), "result");

            // Falls through to the label without a goto, so the default value is the
            // result. Distinguishes a serialized default from a dropped one.
            TestBody(c, Expr.Block(Expr.Label(target, Expr.Constant(-1))));
        }

        [Fact]
        public void ReturnGotoKind()
        {
            var c = Ctx();
            var target = Expr.Label(typeof(int), "return");

            TestBody(c, Expr.Block(
                Expr.Return(target, Expr.Field(c, "A")),
                Expr.Label(target, Expr.Constant(0))));
        }

        // ---- Switch --------------------------------------------------------------------

        [Fact]
        public void Switch()
        {
            var c = Ctx();

            TestBody(c, Expr.Switch(
                Expr.Constant(2),
                Expr.Constant(-1),
                Expr.SwitchCase(Expr.Constant(10), Expr.Constant(1)),
                Expr.SwitchCase(Expr.Constant(20), Expr.Constant(2))));
        }

        [Fact]
        public void SwitchTakesDefaultBranch()
        {
            var c = Ctx();

            TestBody(c, Expr.Switch(
                Expr.Constant(99),
                Expr.Field(c, "A"),
                Expr.SwitchCase(Expr.Constant(10), Expr.Constant(1))));
        }

        [Fact]
        public void SwitchCaseWithMultipleTestValues()
        {
            var c = Ctx();

            TestBody(c, Expr.Switch(
                Expr.Constant(3),
                Expr.Constant(-1),
                Expr.SwitchCase(Expr.Constant(10), Expr.Constant(1), Expr.Constant(2), Expr.Constant(3))));
        }

        [Fact]
        public void SwitchOnStringUsesComparisonMethod()
        {
            var c = Ctx();
            // A string switch carries a Comparison MethodInfo, which the default int
            // switches leave null.
            TestBody(c, Expr.Switch(
                Expr.Constant("b"),
                Expr.Constant(-1),
                Expr.SwitchCase(Expr.Constant(1), Expr.Constant("a")),
                Expr.SwitchCase(Expr.Constant(2), Expr.Constant("b"))));
        }

        // ---- Try -----------------------------------------------------------------------

        [Fact]
        public void TryCatch()
        {
            var c = Ctx();

            TestBody(c, Expr.TryCatch(
                Expr.Block(
                    Expr.Throw(Expr.New(typeof(InvalidOperationException))),
                    Expr.Constant(1)),
                Expr.Catch(typeof(InvalidOperationException), Expr.Constant(42))));
        }

        [Fact]
        public void TryCatchNotTaken()
        {
            var c = Ctx();

            TestBody(c, Expr.TryCatch(
                Expr.Field(c, "A"),
                Expr.Catch(typeof(InvalidOperationException), Expr.Constant(42))));
        }

        [Fact]
        public void TryCatchWithExceptionVariable()
        {
            var c = Ctx();
            var ex = Expr.Parameter(typeof(InvalidOperationException), "ex");

            TestBody(c, Expr.TryCatch(
                Expr.Block(
                    Expr.Throw(Expr.New(typeof(InvalidOperationException))),
                    Expr.Constant(1)),
                Expr.Catch(ex, Expr.Property(Expr.Property(ex, "Message"), "Length"))));
        }

        [Fact]
        public void TryCatchWithFilter()
        {
            var c = Ctx();
            var ex = Expr.Parameter(typeof(InvalidOperationException), "ex");

            var body = Expr.TryCatch(
                Expr.Block(
                    Expr.Throw(Expr.New(typeof(InvalidOperationException))),
                    Expr.Constant(1)),
                Expr.Catch(ex, Expr.Constant(7), Expr.Constant(true)));

#if NETFULL
            // net48 cannot compile an exception filter into a DynamicMethod; the payload
            // still round-trips. See TestBodyWithoutCompiling.
            TestBodyWithoutCompiling(c, body);
#else
            TestBody(c, body);
#endif
        }

        [Fact]
        public void TryFinally()
        {
            var c = Ctx();
            var v = Expr.Variable(typeof(int), "v");

            TestBody(c, Expr.Block(
                new[] { v },
                Expr.TryFinally(
                    Expr.Assign(v, Expr.Constant(1)),
                    Expr.Assign(v, Expr.Constant(2))),
                v));
        }

        [Fact]
        public void TryFault()
        {
            var c = Ctx();

            // Fault is mutually exclusive with catch/finally in MakeTry, so it needs its
            // own tree rather than being folded into one of the above.
            var body = Expr.TryFault(
                Expr.Field(c, "A"),
                Expr.Constant(0));

#if NETFULL
            // net48 cannot compile a fault block into a DynamicMethod; the payload still
            // round-trips. See TestBodyWithoutCompiling.
            TestBodyWithoutCompiling(c, body);
#else
            TestBody(c, body);
#endif
        }

        [Fact]
        public void TryCatchMultipleHandlers()
        {
            var c = Ctx();

            TestBody(c, Expr.TryCatch(
                Expr.Block(
                    Expr.Throw(Expr.New(typeof(InvalidOperationException))),
                    Expr.Constant(1)),
                Expr.Catch(typeof(ArgumentException), Expr.Constant(11)),
                Expr.Catch(typeof(InvalidOperationException), Expr.Constant(22)),
                Expr.Catch(typeof(Exception), Expr.Constant(33))));
        }

        // ---- Default / index / runtime variables -------------------------------------

        [Fact]
        public void DefaultValue()
        {
            var c = Ctx();
            TestBody(c, Expr.Default(typeof(int)));
        }

        [Fact]
        public void IndexerAccess()
        {
            var c = Ctx();
            var indexer = typeof(Context).GetProperty(
                "Item", BindingFlags.Public | BindingFlags.Instance);

            // MakeIndex produces an IndexExpression, which C# indexer syntax does not --
            // that lowers to a get_Item MethodCallExpression instead.
            TestBody(c, Expr.MakeIndex(c, indexer, new Expression[] { Expr.Constant("A") }));
        }

        [Fact]
        public void RuntimeVariablesNode()
        {
            var c = Ctx();
            var v = Expr.Variable(typeof(int), "v");

            // The RuntimeVariables result is discarded; the node still round-trips.
            TestBody(c, Expr.Block(
                new[] { v },
                Expr.Assign(v, Expr.Constant(3)),
                Expr.RuntimeVariables(v),
                v));
        }

        // ---- ListInit / MemberInit ---------------------------------------------------
        //
        // Both are reducible, and ExpressionInternal used to lower them to a block with a
        // temporary before dispatch, so their handlers were never reached. That worked on
        // .NET 8/10 but not on .NET Framework 4.8, where the reduced block declares no
        // variables while its body still references the temporary, and the deserialized
        // tree failed to compile with "referenced from scope '', but it is not defined".
        //
        // Both are now serialized natively, so these run unguarded on every target (#8).

        private static MemberExpression ListInitBody(ParameterExpression c)
        {
            return Expr.Property(
                Expr.ListInit(
                    Expr.New(typeof(List<int>)),
                    Expr.Constant(1),
                    Expr.Constant(2),
                    Expr.Constant(3)),
                "Count");
        }

        private static BinaryExpression MemberInitBody(ParameterExpression c)
        {
            var init = Expr.MemberInit(
                Expr.New(typeof(Context)),
                Expr.Bind(typeof(Context).GetField("A"), Expr.Constant(11)),
                Expr.Bind(typeof(Context).GetProperty("B"), Expr.Constant(22)));

            return Expr.Add(Expr.Field(init, "A"), Expr.Property(init, "B"));
        }

        [Fact]
        public void ListInit()
        {
            var c = Ctx();
            TestBody(c, ListInitBody(c));
        }

        [Fact]
        public void MemberInit()
        {
            var c = Ctx();
            TestBody(c, MemberInitBody(c));
        }

        [Fact]
        public void ListInitIsSerializedNatively()
        {
            // Guards the fix rather than just the symptom: if the reduce loop starts
            // lowering these again, the payload becomes a block and this fails even on
            // platforms where the round trip still happens to work.
            var c = Ctx();
            var json = JObject.Parse(JsonConvert.SerializeObject(
                Expr.Lambda(ListInitBody(c), c), ReflectionSettings()));

            Assert.NotNull(FindByTypeName(json, "listInit"));
        }

        [Fact]
        public void MemberInitIsSerializedNatively()
        {
            var c = Ctx();
            var json = JObject.Parse(JsonConvert.SerializeObject(
                Expr.Lambda(MemberInitBody(c), c), ReflectionSettings()));

            Assert.NotNull(FindByTypeName(json, "memberInit"));
        }

        [Fact]
        public void MemberInitWithNestedListBinding()
        {
            // MemberListBinding: initialising a collection member in place rather than
            // assigning a new one. Exercises the ListBinding arm of MemberBinding.
            var c = Ctx();

            var init = Expr.MemberInit(
                Expr.New(typeof(Basket)),
                Expr.ListBind(
                    typeof(Basket).GetProperty("Items"),
                    Expr.ElementInit(
                        typeof(List<int>).GetMethod("Add"), Expr.Constant(4)),
                    Expr.ElementInit(
                        typeof(List<int>).GetMethod("Add"), Expr.Constant(5))));

            TestBody(c, Expr.Property(Expr.Property(init, "Items"), "Count"));
        }

        [Fact]
        public void MemberInitWithNestedMemberBinding()
        {
            // MemberMemberBinding: initialising members of a nested object in place.
            // Exercises the recursive arm of MemberBinding.
            var c = Ctx();

            var init = Expr.MemberInit(
                Expr.New(typeof(Basket)),
                Expr.MemberBind(
                    typeof(Basket).GetProperty("Owner"),
                    Expr.Bind(typeof(Holder).GetProperty("Count"), Expr.Constant(9))));

            TestBody(c, Expr.Property(Expr.Property(init, "Owner"), "Count"));
        }

        public sealed class Holder
        {
            public int Count { get; set; }
        }

        public sealed class Basket
        {
            public Basket()
            {
                Items = new List<int>();
                Owner = new Holder();
            }

            public List<int> Items { get; private set; }
            public Holder Owner { get; private set; }
        }
    }
}
