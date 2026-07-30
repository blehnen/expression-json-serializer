using System;
using System.Linq.Expressions;

namespace Aq.ExpressionJsonSerializer
{
    partial class Serializer
    {
        private bool MemberInitExpression(Expression expr)
        {
            var expression = expr as MemberInitExpression;
            if (expression == null) { return false; }

            Prop("typeName", "memberInit");
            Prop("newExpression", Expression(expression.NewExpression));
            Prop("bindings", Enumerable(expression.Bindings, MemberBinding));

            return true;
        }

        // MemberBinding is not an Expression, so it gets its own writer. It has three
        // forms, and MemberMemberBinding nests, so this recurses.
        private Action MemberBinding(MemberBinding binding)
        {
            return () => MemberBindingInternal(binding);
        }

        private void MemberBindingInternal(MemberBinding binding)
        {
            if (binding == null) {
                _writer.WriteNull();
                return;
            }

            _writer.WriteStartObject();
            Prop("bindingType", Enum(binding.BindingType));
            Prop("member", Member(binding.Member));

            switch (binding.BindingType) {
                case MemberBindingType.Assignment:
                    Prop("expression",
                        Expression(((MemberAssignment) binding).Expression));
                    break;
                case MemberBindingType.ListBinding:
                    Prop("initializers",
                        Enumerable(((MemberListBinding) binding).Initializers, ElementInit));
                    break;
                case MemberBindingType.MemberBinding:
                    Prop("bindings",
                        Enumerable(((MemberMemberBinding) binding).Bindings, MemberBinding));
                    break;
                default:
                    throw new NotSupportedException(
                        "Unsupported member binding type: " + binding.BindingType);
            }

            _writer.WriteEndObject();
        }
    }
}
