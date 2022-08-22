using FastAop;
using FastAop.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FastAop.Constructor
{
    internal static class Constructor
    {
        internal static int depth = 0;

        internal static void Param(Type paramType, Type aopType=null)
        {
            if (paramType.isSysType())
                return;

            if (!paramType.IsInterface && paramType.GetInterfaces().Any())
            {
                FastAop._types.SetValue(paramType.GetInterfaces().First(), FastAop.Instance(paramType, paramType.GetInterfaces().First()));
                return;
            }
            else if (!paramType.IsInterface && !paramType.GetInterfaces().Any())
            {
                Dic.SetValueDyn(paramType, FastAopDyn.Instance(paramType,aopType));
                return;
            }

            if (paramType.IsInterface && FastAop._types.GetValue(paramType) == null)
            {
                var param = paramType.Assembly.GetTypes().ToList().Find(t => t.GetInterfaces().Any() && !t.IsAbstract && !t.IsSealed && t.GetInterfaces().ToList().Exists(e => e == paramType));
                if (param == null)
                    return;

                if (param.GetInterfaces().Any() && !param.IsAbstract && !param.IsSealed && param.GetInterfaces().ToList().Exists(e => e == paramType))
                {
                    depth++;
                    if (param.GetConstructors().Length > 0)
                    {
                        param.GetConstructors().ToList().ForEach(c =>
                        {
                            c.GetParameters().ToList().ForEach(p =>
                            {
                                if (depth > 10)
                                    throw new Exception($"Type Name:{param.FullName},Parameters Type:{string.Join(",", c.GetParameters().Select(g => g.Name))} repeat using");

                                Param(p.ParameterType, aopType);
                            });
                        });
                    }
                    FastAop._types.SetValue(paramType, FastAop.Instance(param, paramType,aopType));
                }
                return;
            }
            return;
        }

        internal static bool isSysType(this Type type)
        {
            if (type.IsPrimitive || type.Equals(typeof(string)) || type.Equals(typeof(decimal)) || type.Equals(typeof(DateTime)))
                return true;
            else
                return false;
        }

        internal static ConstructorModel Get(Type serviceType, Type interfaceType)
        {
            var model = new ConstructorModel();
            serviceType.GetConstructors().ToList().ForEach(a =>
            {
                a.GetParameters().ToList().ForEach(p =>
                {
                    model.constructorType.Add(p.ParameterType);
                    model.dynType.Add(p.ParameterType);
                    if (p.ParameterType.isSysType() && !p.ParameterType.IsValueType)
                        model.dynParam.Add("");
                    else if (p.ParameterType.isSysType() && p.ParameterType.IsValueType)
                        model.dynParam.Add(Activator.CreateInstance(p.ParameterType));
                    else if (!p.ParameterType.IsAbstract && !p.ParameterType.IsInterface && Dic.GetValueDyn(p.ParameterType) != null)
                        model.dynParam.Add(Dic.GetValueDyn(p.ParameterType));
                    else if (!p.ParameterType.IsAbstract && !p.ParameterType.IsInterface)
                        model.dynParam.Add(Activator.CreateInstance(p.ParameterType));
                    else if (FastAop._types.GetValue(p.ParameterType) == null && p.ParameterType.IsInterface && p.ParameterType.IsGenericType)
                        throw new Exception($"FastAop.InitGeneric Method，{serviceType.FullName} Constructor have Parameter Generic Type");
                    else if (FastAop._types.GetValue(p.ParameterType) == null && p.ParameterType.IsInterface)
                        throw new Exception($"can't find {p.ParameterType.Name} Instance class");
                    else if (p.ParameterType.IsInterface)
                        model.dynParam.Add(FastAop._types.GetValue(p.ParameterType));
                });
            });

            if (interfaceType != null)
            {
                model.interfaceType = interfaceType;
                model.dynType.Add(interfaceType);
            }

            if (interfaceType == null)
                model.dynType.Add(serviceType);

            model.serviceType = serviceType;

            return model;
        }
    }
}

namespace System.Collections.Generic
{
    internal static class Dic
    {
        internal static Object GetValue(this Dictionary<Type, object> item, Type key)
        {
            if (key == null)
                return null;

            if (item == null)
                return null;

            key = item.Keys.ToList().Find(a => a == key);

            if (item.Keys.ToList().Exists(a => a == key))
                return item[key];
            else
                return null;
        }

        internal static Dictionary<Type, object> SetValue(this Dictionary<Type, object> item, Type key, object value)
        {
            if (key == null)
                return null;

            if (item == null)
                return null;

            if (item.Keys.ToList().Exists(a => a == key))
                item[key] = value;
            else
                item.Add(key, value);

            return item;
        }

        internal static dynamic GetValueDyn(Type key)
        {
            if (key == null)
                return null;

            if (FastAopDyn._types == null)
                return null;

            key = FastAopDyn._types.Keys.ToList().Find(a => a == key);

            if (FastAopDyn._types.Keys.ToList().Exists(a => a == key))
                return FastAopDyn._types[key];
            else
                return null;
        }

        internal static void SetValueDyn(Type key, dynamic value)
        {
            if (key == null)
                return;

            if (FastAopDyn._types == null)
                return;

            if (FastAopDyn._types.Keys.ToList().Exists(a => a == key))
                FastAopDyn._types[key] = value;
            else
                FastAopDyn._types.Add(key, value);

            return;
        }
    }
}
