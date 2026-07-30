using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Aq.ExpressionJsonSerializer
{
    partial class Serializer
    {
        private const string SignatureProp = "signature";

        private static readonly ConcurrentDictionary<Type, Tuple<string, string, Type[]>>
            TypeCache = new ConcurrentDictionary<Type, Tuple<string, string, Type[]>>();

        private Action Type(Type type)
        {
            return () => TypeInternal(type);
        }

        private void TypeInternal(Type type)
        {
            if (type == null) {
                _writer.WriteNull();
            }
            else {
                Tuple<string, string, Type[]> tuple;
                if (!TypeCache.TryGetValue(type, out tuple)) {
                    var assemblyName = type.Assembly.FullName;
                    if (type.IsGenericType) {
                        var def = type.GetGenericTypeDefinition();
                        tuple = new Tuple<string, string, Type[]>(
                            def.Assembly.FullName, def.FullName,
                            type.GetGenericArguments()
                        );
                    }
                    else {
                        tuple = new Tuple<string, string, Type[]>(
                            assemblyName, type.FullName, null);
                    }
                    TypeCache[type] = tuple;
                }

                _writer.WriteStartObject();
                Prop("assemblyName", tuple.Item1);
                Prop("typeName", tuple.Item2);
                Prop("genericArguments", Enumerable(tuple.Item3, Type));
                _writer.WriteEndObject();
            }
        }

        private Action Constructor(ConstructorInfo constructor)
        {
            return () => ConstructorInternal(constructor);
        }

        private void ConstructorInternal(ConstructorInfo constructor)
        {
            if (constructor == null) {
                _writer.WriteNull();
            }
            else {
                _writer.WriteStartObject();
                Prop("type", Type(constructor.DeclaringType));
                Prop("name", constructor.Name);
                Prop(SignatureProp, constructor.ToString());
                _writer.WriteEndObject();
            }
        }

        private Action Method(MethodInfo method)
        {
            return () => MethodInternal(method);
        }

        private void MethodInternal(MethodInfo method)
        {
            if (method == null) {
                _writer.WriteNull();
            }
            else {
                _writer.WriteStartObject();
                if (method.IsGenericMethod) {
                    var meth = method.GetGenericMethodDefinition();
                    var generic = method.GetGenericArguments();

                    Prop("type", Type(meth.DeclaringType));
                    Prop("name", meth.Name);
                    Prop(SignatureProp, meth.ToString());
                    Prop("generic", Enumerable(generic, Type));
                }
                else {
                    Prop("type", Type(method.DeclaringType));
                    Prop("name", method.Name);
                    Prop(SignatureProp, method.ToString());
                }
                _writer.WriteEndObject();
            }
        }

        private Action Property(PropertyInfo property)
        {
            return () => PropertyInternal(property);
        }

        private void PropertyInternal(PropertyInfo property)
        {
            if (property == null) {
                _writer.WriteNull();
            }
            else {
                _writer.WriteStartObject();
                Prop("type", Type(property.DeclaringType));
                Prop("name", property.Name);
                Prop(SignatureProp, property.ToString());
                _writer.WriteEndObject();
            }
        }

        private Action Member(MemberInfo member)
        {
            return () => MemberInternal(member);
        }

        private void MemberInternal(MemberInfo member)
        {
            if (member == null) {
                _writer.WriteNull();
            }
            else {
                _writer.WriteStartObject();
                Prop("type", Type(member.DeclaringType));
                Prop("memberType", (int) member.MemberType);
                Prop("name", member.Name);
                Prop(SignatureProp, member.ToString());
                _writer.WriteEndObject();
            }
        }
    }
}
