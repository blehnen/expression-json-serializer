using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Aq.ExpressionJsonSerializer
{
    partial class Deserializer
    {
        private static readonly ConcurrentDictionary<Assembly, ConcurrentDictionary<string, ConcurrentDictionary<string, Type>>>
            TypeCache = new ConcurrentDictionary<Assembly, ConcurrentDictionary<string, ConcurrentDictionary<string, Type>>>();

        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, ConcurrentDictionary<string, ConstructorInfo>>>
            ConstructorCache = new ConcurrentDictionary<Type, ConcurrentDictionary<string, ConcurrentDictionary<string, ConstructorInfo>>>();

        private Type Type(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) {
                return null;
            }

            var obj = (JObject) token;
            var assemblyName = Prop(obj, "assemblyName", t => t.Value<string>());
            var typeName = Prop(obj, "typeName", t => t.Value<string>());
            var generic = Prop(obj, "genericArguments", Enumerable(Type));

            ConcurrentDictionary<string, ConcurrentDictionary<string, Type>> assemblies;
            if (!TypeCache.TryGetValue(_assembly, out assemblies)) {
                assemblies = new ConcurrentDictionary<string, ConcurrentDictionary<string, Type>>();
                TypeCache[_assembly] = assemblies;
            }

            ConcurrentDictionary<string, Type> types;
            if (!assemblies.TryGetValue(assemblyName, out types)) {
                types = new ConcurrentDictionary<string, Type>();
                assemblies[assemblyName] = types;
            }

            Type type;
            if (!types.TryGetValue(typeName, out type)) {
                type = _assembly.GetType(typeName);
                if (type == null) {
                    var assembly = Assembly.Load(new AssemblyName(assemblyName));
                    type = assembly.GetType(typeName);
                }
                if (type == null) {
                    throw new Exception(
                        "Type could not be found: "
                        + assemblyName + "." + typeName
                    );
                }
                types[typeName] = type;
            }

            if (generic != null && type.IsGenericTypeDefinition) {
                type = type.MakeGenericType(generic.ToArray());
            }

            return type;
        }

        private ConstructorInfo Constructor(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) {
                return null;
            }

            var obj = (JObject) token;
            var type = Prop(obj, "type", Type);
            var name = Prop(obj, "name").Value<string>();
            var signature = Prop(obj, "signature").Value<string>();

            ConstructorInfo constructor;
            ConcurrentDictionary<string, ConstructorInfo> cache2;
            ConcurrentDictionary<string, ConcurrentDictionary<string, ConstructorInfo>> cache1;

            if (!ConstructorCache.TryGetValue(type, out cache1)) {
                constructor = ConstructorInternal(type, name, signature);

                cache2 = new ConcurrentDictionary<
                    string, ConstructorInfo>();
                cache2.TryAdd(signature, constructor);

                cache1 = new ConcurrentDictionary<
                    string, ConcurrentDictionary<
                        string, ConstructorInfo>>();
                cache1.TryAdd(name, cache2);
                
                ConstructorCache[type] = cache1;
            }
            else if (!cache1.TryGetValue(name, out cache2)) {
                constructor = ConstructorInternal(type, name, signature);

                cache2 = new ConcurrentDictionary<
                    string, ConstructorInfo>();
                cache2.TryAdd(signature, constructor);

                cache1[name] = cache2;
            }
            else if (!cache2.TryGetValue(signature, out constructor)) {
                constructor = ConstructorInternal(type, name, signature);
                cache2[signature] = constructor;
            }

            return constructor;
        }

        private ConstructorInfo ConstructorInternal(
            Type type, string name, string signature)
        {
            var constructor = type
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(c => c.Name == name && c.ToString() == signature);
            
            if (constructor == null) {
                constructor = type
                    .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(c => c.Name == name && c.ToString() == signature);
                
                if (constructor == null) {
                    throw new Exception(
                        "Constructor for type \""
                        + type.FullName +
                        "\" with signature \""
                        + signature +
                        "\" could not be found"
                    );
                }
            }

            return constructor;
        }

        private MethodInfo Method(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) {
                return null;
            }

            var obj = (JObject) token;
            var type = Prop(obj, "type", Type);
            var name = Prop(obj, "name").Value<string>();
            var signature = Prop(obj, "signature").Value<string>();
            var generic = Prop(obj, "generic", Enumerable(Type));

            var methods = type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static
            );
            var method = methods.First(m => m.Name == name && m.ToString() == signature);

            if (generic != null && method.IsGenericMethodDefinition) {
                method = method.MakeGenericMethod(generic.ToArray());
            }

            return method;
        }

        private PropertyInfo Property(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) {
                return null;
            }

            var obj = (JObject) token;
            var type = Prop(obj, "type", Type);
            var name = Prop(obj, "name").Value<string>();
            var signature = Prop(obj, "signature").Value<string>();

            var properties = type.GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static
            );
            return properties.First(p => p.Name == name && p.ToString() == signature);
        }

        private MemberInfo Member(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) {
                return null;
            }

            var obj = (JObject) token;
            var type = Prop(obj, "type", Type);
            var name = Prop(obj, "name").Value<string>();
            var signature = Prop(obj, "signature").Value<string>();
            var memberType = (MemberTypes) Prop(obj, "memberType").Value<int>();

            var members = type.GetMembers(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static
            );
            return members.First(p => p.MemberType == memberType
                && p.Name == name && p.ToString() == signature);
        }
    }
}
