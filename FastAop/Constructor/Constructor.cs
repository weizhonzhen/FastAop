using FastAop.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FastAop.Constructor
{
    internal static class Constructor
    {
        internal static int depth = 0;

        internal static void Param(Type paramType, Type aopType)
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
                Dic.SetValueDyn(FastAopDyn._types, paramType, FastAopDyn.Instance(paramType));
                return;
            }

            if (paramType.IsInterface && FastAop._types.GetValue(paramType) == null)
            {
                paramType.Assembly.GetTypes().ToList().ForEach(t =>
                {
                    if (t.GetInterfaces().Any() && !t.IsAbstract && !t.IsSealed && t.GetInterfaces().ToList().Exists(e => e == paramType))
                    {
                        depth++;
                        if (t.GetConstructors().Length > 0)
                        {
                            t.GetConstructors().ToList().ForEach(c =>
                            {
                                c.GetParameters().ToList().ForEach(p =>
                                {
                                    if (depth > 10)
                                        throw new Exception($"Type Name:{t.FullName},Parameters Type:{string.Join(",", c.GetParameters().Select(g => g.Name))} repeat using");

                                    Param(p.ParameterType, aopType);
                                });
                            });
                        }
                        FastAop._types.SetValue(paramType, FastAop.Instance(t, paramType));
                    }
                });
            }

            return;
        }

        internal static void Param(Type paramType, bool isServiceAttr)
        {
            if (paramType.isSysType())
                return;

            if (!paramType.IsInterface && paramType.GetInterfaces().Any() && isServiceAttr)
            {
                FastAop._types.SetValue(paramType.GetInterfaces().First(), FastAop.Instance(paramType, paramType.GetInterfaces().First()));
                return;
            }
            else if (!paramType.IsInterface && paramType.GetInterfaces().Any())
            {
                FastAop._types.SetValue(paramType.GetInterfaces().First(), paramType);
                return;
            }
            else if (!paramType.IsInterface && !paramType.GetInterfaces().Any() && isServiceAttr)
            {
                Dic.SetValueDyn(FastAopDyn._types, paramType, FastAopDyn.Instance(paramType));
                return;
            }
            else if (!paramType.IsInterface && !paramType.GetInterfaces().Any() && paramType.GetConstructors().Length == 0)
            {
                FastAop._types.SetValue(paramType, Activator.CreateInstance(paramType));
                return;
            }
            else if (!paramType.IsInterface && !paramType.GetInterfaces().Any() && paramType.GetConstructors().Length > 0)
            {
                var model = Get(paramType, null);
                FastAop._types.SetValue(paramType, Activator.CreateInstance(paramType, model.dynParam.ToArray()));
            }

            if (paramType.IsInterface && FastAop._types.GetValue(paramType) == null)
            {
                paramType.Assembly.GetTypes().ToList().ForEach(t =>
                {
                    if (t.GetInterfaces().Any() && !t.IsAbstract && !t.IsSealed && t.GetInterfaces().ToList().Exists(e => e == paramType))
                    {
                        if (t.GetConstructors().Length > 0)
                        {
                            depth++;
                            t.GetConstructors().ToList().ForEach(c =>
                            {
                                c.GetParameters().ToList().ForEach(p =>
                                {
                                    if (depth > 10)
                                        throw new Exception($"Type Name:{t.FullName},Parameters Type:{string.Join(",", c.GetParameters().Select(g => g.Name))} repeat using");

                                    Param(p.ParameterType, isServiceAttr);
                                });
                            });
                        }
                        FastAop._types.SetValue(paramType, FastAop.Instance(t, paramType));
                    }
                });
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
                    if (!p.ParameterType.IsAbstract && !p.ParameterType.IsInterface)
                        model.dynParam.Add(Activator.CreateInstance(p.ParameterType));
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

        internal static Object GetValueDyn(this Dictionary<Type, dynamic> item, Type key)
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

        internal static Dictionary<Type, dynamic> SetValueDyn(Dictionary<Type, dynamic> item, Type key, dynamic value)
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
    }
}
