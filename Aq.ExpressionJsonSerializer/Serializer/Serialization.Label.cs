using System.Linq.Expressions;

namespace Aq.ExpressionJsonSerializer
{
    partial class Serializer
    {
        private bool LabelExpression(Expression expr)
        {
            var expression = expr as LabelExpression;
            if (expression == null) { return false; }

            // The target is written as a name/type pair rather than as a node, matching
            // Serialization.Goto and Serialization.Loop. Deserializer.CreateLabelTarget
            // interns by name, so a goto and the label it lands on resolve to the same
            // LabelTarget instance on the way back in.
            this.Prop("typeName", "label");
            this.Prop("defaultValue", this.Expression(expression.DefaultValue));
            this.Prop("targetName", expression.Target.Name ?? "#" + expression.Target.GetHashCode());
            this.Prop("targetType", this.Type(expression.Target.Type));

            return true;
        }
    }
}
