using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
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

        // ---- Unimplemented nodes -----------------------------------------------------
        //
        // LabelExpression, SwitchExpression and TryExpression are stubs that throw
        // NotImplementedException on BOTH the serializer and deserializer side (as are
        // DebugInfoExpression and DynamicExpression, which cannot be constructed here
        // without a call site or debug info provider).
        //
        // These tests pin the current behaviour so the limitation is visible and a future
        // implementation has a failing test to flip. They are not an endorsement of the
        // gap. Note the practical consequence: Expr.Loop with break/continue works, but a
        // bare Expr.Label node does not, so goto/label blocks cannot round-trip.
        //
        // ListInit and MemberInit have stubs too, but those are dead code -- CanReduce is
        // true for both, so ExpressionInternal reduces them to blocks before dispatch.
        // The ListInit and MemberInit tests above exercise them through that path.

        [Fact]
        public void LabelNodeIsNotImplemented()
        {
            var c = Ctx();
            var v = Expr.Variable(typeof(int), "v");
            var target = Expr.Label("skip");

            Assert.Throws<NotImplementedException>(() => TestBody(c, Expr.Block(
                new[] { v },
                Expr.Assign(v, Expr.Constant(1)),
                Expr.Goto(target),
                Expr.Assign(v, Expr.Constant(2)),   // would be jumped over
                Expr.Label(target),
                v)));
        }

        [Fact]
        public void LabelWithDefaultValueIsNotImplemented()
        {
            var c = Ctx();
            var target = Expr.Label(typeof(int), "result");

            Assert.Throws<NotImplementedException>(() => TestBody(c, Expr.Block(
                Expr.Goto(target, Expr.Field(c, "A")),
                Expr.Label(target, Expr.Constant(-1)))));
        }

        [Fact]
        public void SwitchIsNotImplemented()
        {
            var c = Ctx();

            Assert.Throws<NotImplementedException>(() => TestBody(c, Expr.Switch(
                Expr.Constant(2),
                Expr.Constant(-1),
                Expr.SwitchCase(Expr.Constant(10), Expr.Constant(1)),
                Expr.SwitchCase(Expr.Constant(20), Expr.Constant(2)))));
        }

        [Fact]
        public void TryCatchIsNotImplemented()
        {
            var c = Ctx();

            Assert.Throws<NotImplementedException>(() => TestBody(c, Expr.TryCatch(
                Expr.Block(
                    Expr.Throw(Expr.New(typeof(InvalidOperationException))),
                    Expr.Constant(1)),
                Expr.Catch(typeof(InvalidOperationException), Expr.Constant(42)))));
        }

        [Fact]
        public void TryFinallyIsNotImplemented()
        {
            var c = Ctx();
            var v = Expr.Variable(typeof(int), "v");

            Assert.Throws<NotImplementedException>(() => TestBody(c, Expr.Block(
                new[] { v },
                Expr.TryFinally(
                    Expr.Assign(v, Expr.Constant(1)),
                    Expr.Assign(v, Expr.Constant(2))),
                v)));
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
        // Both nodes are reducible, so ExpressionInternal rewrites them to a block with a
        // temporary before dispatch and their stub handlers are never reached.
        //
        // That reduction round-trips on .NET 8/10 but NOT on .NET Framework 4.8, where the
        // deserialized tree fails to compile with "variable '#nnnnn' ... referenced from
        // scope '', but it is not defined" -- the temporary's declaration and its uses do
        // not resolve to the same ParameterExpression after the name-based round trip.
        // Tracked in #8. Guarded rather than deleted so the platform difference stays
        // visible; this mirrors how TypeAs handles a Newtonsoft platform difference in the
        // sibling file.

        private static Expression ListInitBody(ParameterExpression c)
        {
            return Expr.Property(
                Expr.ListInit(
                    Expr.New(typeof(List<int>)),
                    Expr.Constant(1),
                    Expr.Constant(2),
                    Expr.Constant(3)),
                "Count");
        }

        private static Expression MemberInitBody(ParameterExpression c)
        {
            var init = Expr.MemberInit(
                Expr.New(typeof(Context)),
                Expr.Bind(typeof(Context).GetField("A"), Expr.Constant(11)),
                Expr.Bind(typeof(Context).GetProperty("B"), Expr.Constant(22)));

            return Expr.Add(Expr.Field(init, "A"), Expr.Property(init, "B"));
        }

#if NETFULL
        [Fact]
        public void ListInit()
        {
            var c = Ctx();
            Assert.ThrowsAny<Exception>(() => TestBody(c, ListInitBody(c)));
        }

        [Fact]
        public void MemberInit()
        {
            var c = Ctx();
            Assert.ThrowsAny<Exception>(() => TestBody(c, MemberInitBody(c)));
        }
#else
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
#endif
    }
}
