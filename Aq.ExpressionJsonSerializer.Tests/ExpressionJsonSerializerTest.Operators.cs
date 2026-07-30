using System;
using System.Linq.Expressions;
using Xunit;
using Expr = System.Linq.Expressions.Expression;

namespace Aq.ExpressionJsonSerializer.Tests
{
    /// <summary>
    /// Coverage for the binary and unary operator dispatch. Both deserializers switch on
    /// roughly 20-40 ExpressionType arms each; the lambda-syntax tests in the sibling file
    /// only reach a handful of them.
    ///
    /// Compound assignment nodes (AddAssign and friends) are only reachable when the
    /// target is a local variable. Against a member or index target they are reducible,
    /// and ExpressionInternal reduces before dispatch, so they never reach those arms.
    /// </summary>
    public partial class ExpressionJsonSerializerTest
    {
        private static void TestInt(Func<ParameterExpression, Expression> build)
        {
            var c = Expr.Parameter(typeof(Context), "c");
            TestExpression(Expr.Lambda(build(c), c));
        }

        /// <summary>Wraps body in a block with a seeded int local, for assignment operators.</summary>
        private static void TestWithLocal(Func<ParameterExpression, Expression> build, int seed = 12)
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var v = Expr.Variable(typeof(int), "v");
            TestExpression(Expr.Lambda(
                Expr.Block(new[] { v },
                    Expr.Assign(v, Expr.Constant(seed)),
                    build(v),
                    v),
                c));
        }

        // ---- Arithmetic ---------------------------------------------------------------

        [Fact] public void OpSubtract() { TestInt(c => Expr.Subtract(Expr.Field(c, "A"), Expr.Constant(3))); }
        [Fact] public void OpMultiply() { TestInt(c => Expr.Multiply(Expr.Field(c, "A"), Expr.Constant(3))); }
        [Fact] public void OpDivide() { TestInt(c => Expr.Divide(Expr.Field(c, "A"), Expr.Constant(7))); }
        [Fact] public void OpModulo() { TestInt(c => Expr.Modulo(Expr.Field(c, "A"), Expr.Constant(7))); }

        [Fact] public void OpAddChecked() { TestInt(c => Expr.AddChecked(Expr.Constant(2), Expr.Constant(3))); }
        [Fact] public void OpSubtractChecked() { TestInt(c => Expr.SubtractChecked(Expr.Constant(9), Expr.Constant(4))); }
        [Fact] public void OpMultiplyChecked() { TestInt(c => Expr.MultiplyChecked(Expr.Constant(6), Expr.Constant(7))); }

        [Fact]
        public void OpPower()
        {
            // Power maps to Math.Pow and requires double operands.
            TestInt(c => Expr.Power(Expr.Constant(2.0), Expr.Constant(10.0)));
        }

        // ---- Bitwise and shifts -------------------------------------------------------

        [Fact] public void OpAnd() { TestInt(c => Expr.And(Expr.Field(c, "A"), Expr.Constant(0xFF))); }
        [Fact] public void OpOr() { TestInt(c => Expr.Or(Expr.Field(c, "A"), Expr.Constant(0x0F))); }
        [Fact] public void OpExclusiveOr() { TestInt(c => Expr.ExclusiveOr(Expr.Field(c, "A"), Expr.Constant(0x33))); }
        [Fact] public void OpLeftShift() { TestInt(c => Expr.LeftShift(Expr.Constant(3), Expr.Constant(4))); }
        [Fact] public void OpRightShift() { TestInt(c => Expr.RightShift(Expr.Field(c, "A"), Expr.Constant(2))); }

        // ---- Logical / comparison -----------------------------------------------------

        [Fact] public void OpAndAlso() { TestInt(c => Expr.AndAlso(Expr.Constant(true), Expr.GreaterThan(Expr.Field(c, "A"), Expr.Constant(0)))); }
        [Fact] public void OpOrElse() { TestInt(c => Expr.OrElse(Expr.Constant(false), Expr.LessThan(Expr.Field(c, "A"), Expr.Constant(0)))); }
        [Fact] public void OpGreaterThanOrEqual() { TestInt(c => Expr.GreaterThanOrEqual(Expr.Field(c, "A"), Expr.Constant(0))); }
        [Fact] public void OpLessThanOrEqual() { TestInt(c => Expr.LessThanOrEqual(Expr.Field(c, "A"), Expr.Constant(0))); }
        [Fact] public void OpNotEqual() { TestInt(c => Expr.NotEqual(Expr.Field(c, "A"), Expr.Constant(0))); }

        [Fact]
        public void OpCoalesce()
        {
            TestInt(c => Expr.Coalesce(Expr.Field(c, "C"), Expr.Constant(-1)));
        }

        [Fact]
        public void OpArrayIndex()
        {
            TestInt(c => Expr.ArrayIndex(Expr.Field(c, "Array"), Expr.Constant(0)));
        }

        // ---- Compound assignment (local-variable targets stay unreduced) ---------------

        [Fact] public void OpAddAssign() { TestWithLocal(v => Expr.AddAssign(v, Expr.Constant(5))); }
        [Fact] public void OpSubtractAssign() { TestWithLocal(v => Expr.SubtractAssign(v, Expr.Constant(5))); }
        [Fact] public void OpMultiplyAssign() { TestWithLocal(v => Expr.MultiplyAssign(v, Expr.Constant(5))); }
        [Fact] public void OpDivideAssign() { TestWithLocal(v => Expr.DivideAssign(v, Expr.Constant(3))); }
        [Fact] public void OpModuloAssign() { TestWithLocal(v => Expr.ModuloAssign(v, Expr.Constant(5))); }
        [Fact] public void OpAndAssign() { TestWithLocal(v => Expr.AndAssign(v, Expr.Constant(0x0F))); }
        [Fact] public void OpOrAssign() { TestWithLocal(v => Expr.OrAssign(v, Expr.Constant(0x30))); }
        [Fact] public void OpExclusiveOrAssign() { TestWithLocal(v => Expr.ExclusiveOrAssign(v, Expr.Constant(0x11))); }
        [Fact] public void OpLeftShiftAssign() { TestWithLocal(v => Expr.LeftShiftAssign(v, Expr.Constant(2))); }
        [Fact] public void OpRightShiftAssign() { TestWithLocal(v => Expr.RightShiftAssign(v, Expr.Constant(1))); }
        [Fact] public void OpAddAssignChecked() { TestWithLocal(v => Expr.AddAssignChecked(v, Expr.Constant(5))); }
        [Fact] public void OpSubtractAssignChecked() { TestWithLocal(v => Expr.SubtractAssignChecked(v, Expr.Constant(5))); }
        [Fact] public void OpMultiplyAssignChecked() { TestWithLocal(v => Expr.MultiplyAssignChecked(v, Expr.Constant(3))); }

        [Fact]
        public void OpPowerAssign()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            var v = Expr.Variable(typeof(double), "v");
            TestExpression(Expr.Lambda(
                Expr.Block(new[] { v },
                    Expr.Assign(v, Expr.Constant(2.0)),
                    Expr.PowerAssign(v, Expr.Constant(3.0)),
                    v),
                c));
        }

        // ---- Unary --------------------------------------------------------------------

        [Fact] public void OpNegate() { TestInt(c => Expr.Negate(Expr.Field(c, "A"))); }
        [Fact] public void OpNegateChecked() { TestInt(c => Expr.NegateChecked(Expr.Constant(5))); }
        [Fact] public void OpUnaryPlus() { TestInt(c => Expr.UnaryPlus(Expr.Field(c, "A"))); }
        [Fact] public void OpOnesComplement() { TestInt(c => Expr.OnesComplement(Expr.Field(c, "A"))); }
        [Fact] public void OpNot() { TestInt(c => Expr.Not(Expr.GreaterThan(Expr.Field(c, "A"), Expr.Constant(0)))); }
        [Fact] public void OpIsTrue() { TestInt(c => Expr.IsTrue(Expr.GreaterThan(Expr.Field(c, "A"), Expr.Constant(0)))); }
        [Fact] public void OpIsFalse() { TestInt(c => Expr.IsFalse(Expr.GreaterThan(Expr.Field(c, "A"), Expr.Constant(0)))); }
        [Fact] public void OpArrayLength() { TestInt(c => Expr.ArrayLength(Expr.Field(c, "Array"))); }
        [Fact] public void OpIncrement() { TestInt(c => Expr.Increment(Expr.Field(c, "A"))); }
        [Fact] public void OpDecrement() { TestInt(c => Expr.Decrement(Expr.Field(c, "A"))); }
        [Fact] public void OpConvertChecked() { TestInt(c => Expr.ConvertChecked(Expr.Constant(5L), typeof(int))); }

        [Fact] public void OpPreIncrementAssign() { TestWithLocal(v => Expr.PreIncrementAssign(v)); }
        [Fact] public void OpPreDecrementAssign() { TestWithLocal(v => Expr.PreDecrementAssign(v)); }
        [Fact] public void OpPostDecrementAssign() { TestWithLocal(v => Expr.PostDecrementAssign(v)); }

        [Fact]
        public void OpUnbox()
        {
            var c = Expr.Parameter(typeof(Context), "c");
            // Unbox needs a genuinely boxed operand typed as object.
            var boxed = Expr.Convert(Expr.Constant(42), typeof(object));
            TestExpression(Expr.Lambda(Expr.Unbox(boxed, typeof(int)), c));
        }
    }
}
