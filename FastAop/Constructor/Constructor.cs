using FastAop.Model;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace FastAop.Constructor
{
    internal static class Constructor
    {
        private static ConcurrentDictionary<string, ConstructorModel> cache = new ConcurrentDictionary<string, ConstructorModel>();
        internal static int depth = 0;

        internal static void Param(Type paramType, Type aopType = null)
        {
            if (paramType.isSysType())
                return;

            if (!paramType.IsInterface && paramType.GetInterfaces().Any())
            {
                var interfaceType = paramType.GetInterfaces().First();
                FastAop.ServiceInstance.SetValue(interfaceType, FastAop.Instance(paramType, interfaceType, aopType));
                FastAop.ServiceAopType.SetValue(interfaceType, aopType);
                FastAop.ServiceType.SetValue(interfaceType, paramType);
                return;
            }
            else if (!paramType.IsInterface && !paramType.GetInterfaces().Any())
            {
                Dic.SetValueDyn(paramType, FastAopDyn.Instance(paramType, aopType));
                FastAop.ServiceAopType.SetValue(paramType, aopType);
                FastAop.ServiceType.SetValue(paramType, paramType);
                return;
            }

            if (paramType.IsInterface && FastAop.ServiceInstance.GetValue(paramType) == null)
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

                    FastAop.ServiceInstance.SetValue(paramType, FastAop.Instance(param, paramType, aopType));
                    FastAop.ServiceAopType.SetValue(paramType, aopType);
                    FastAop.ServiceType.SetValue(paramType, param);
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
            var key = $"{serviceType.FullName}_{interfaceType?.FullName}";
            var model = new ConstructorModel();
            cache.TryGetValue(key, out model);
            if (model == null)
            {
                model = new ConstructorModel();
                var list = serviceType.GetRuntimeFields().ToList();
                serviceType.GetConstructors().ToList().ForEach(a =>
                {
                    a.GetParameters().ToList().ForEach(p =>
                    {
                        var paramType = list.FindAll(l => l.FieldType == p.ParameterType && l.GetCustomAttribute<Autowired>() == null);
                        if (paramType.Count == 1 && paramType[0] != null && !paramType[0].Attributes.HasFlag(FieldAttributes.InitOnly))
                            throw new AopException($"{serviceType.FullName} Field {paramType[0]} must attribute readonly");

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
                        else if (p.ParameterType.IsInterface)
                            model.dynParam.Add(FastAop.ServiceInstance.GetValue(p.ParameterType));
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
                cache.TryAdd(key, model);
                return model;
            }
            else
                return model;
        }
    }
}

namespace System.Collections.Concurrent
{
    using FastAop;
    using static FastAop.FastAop;

    internal static class Dic
    {
        internal static object GetValue(this ConcurrentDictionary<Type, object> item, Type key)
        {
            if (key == null)
                return null;

            if (item == null)
                return null;

            object obj;

            if (item.Keys.ToList().Exists(a => a == key))
            {
                obj = item[key];
                var aopType = ServiceAopType.GetValue(key);
                var serviceType = ServiceType.GetValue(key);

                if (obj == null || ServiceTime.GetValue(key) == ServiceLifetime.Transient)
                {
                    obj = Instance(serviceType, key, aopType);
                    ServiceInstance.SetValue(key, obj);
                }

                return obj;
            }
            else
                return null;
        }

        internal static ConcurrentDictionary<Type, object> SetValue(this ConcurrentDictionary<Type, object> item, Type key, object value)
        {
            if (key == null)
                return null;

            if (item == null)
                return null;

            if (item.Keys.ToList().Exists(a => a == key))
                item[key] = value;
            else
                item.TryAdd(key, value);

            return item;
        }

        internal static Type GetValue(this ConcurrentDictionary<Type, Type> item, Type key)
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

        internal static ServiceLifetime GetValue(this ConcurrentDictionary<Type, ServiceLifetime> item, Type key)
        {
            if (key == null)
                return ServiceLifetime.Scoped;

            if (item == null)
                return ServiceLifetime.Scoped;

            key = item.Keys.ToList().Find(a => a == key);

            if (item.Keys.ToList().Exists(a => a == key))
                return item[key];
            else
                return ServiceLifetime.Scoped;
        }

        internal static ConcurrentDictionary<Type, Type> SetValue(this ConcurrentDictionary<Type, Type> item, Type key, Type value)
        {
            if (key == null)
                return null;

            if (item == null)
                return null;

            if (item.Keys.ToList().Exists(a => a == key))
                item[key] = value;
            else
                item.TryAdd(key, value);

            return item;
        }

        internal static ConcurrentDictionary<Type, ServiceLifetime> SetValue(this ConcurrentDictionary<Type, ServiceLifetime> item, Type key, ServiceLifetime value)
        {
            if (key == null)
                return null;

            if (item == null)
                return null;

            if (item.Keys.ToList().Exists(a => a == key))
                item[key] = value;
            else
                item.TryAdd(key, value);

            return item;
        }

        internal static dynamic GetValueDyn(Type key)
        {
            if (key == null)
                return null;

            if (FastAopDyn.ServiceInstance == null)
                return null;

            object obj;
            if (FastAopDyn.ServiceInstance.Keys.ToList().Exists(a => a == key))
            {
                obj = FastAopDyn.ServiceInstance[key];

                var aopType = ServiceAopType.GetValue(key);
                var serviceType = ServiceType.GetValue(key);

                if (obj == null || ServiceTime.GetValue(key) == ServiceLifetime.Transient)
                {
                    obj = FastAopDyn.Instance(serviceType, aopType);
                    FastAopDyn.ServiceInstance.SetValue(key, obj);
                }

                return obj;
            }
            else
                return null;
        }

        internal static void SetValueDyn(Type key, dynamic value)
        {
            if (key == null)
                return;

            if (FastAopDyn.ServiceInstance == null)
                return;

            if (FastAopDyn.ServiceInstance.Keys.ToList().Exists(a => a == key))
                FastAopDyn.ServiceInstance[key] = value;
            else
                FastAopDyn.ServiceInstance.TryAdd(key, value);

            return;
        }
    }
}
