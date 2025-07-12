using FastAop.Constructor;
using FastAop.Factory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FastAop
{
    public static partial class FastAop
    {
        internal static ConcurrentDictionary<Type, object> ServiceInstance = new ConcurrentDictionary<Type, object>();
        internal static ConcurrentDictionary<Type, Type> ServiceAopType = new ConcurrentDictionary<Type, Type>();
        internal static ConcurrentDictionary<Type, Type> ServiceType = new ConcurrentDictionary<Type, Type>();
        internal static ConcurrentDictionary<Type, ServiceLifetime> ServiceTime = new ConcurrentDictionary<Type, ServiceLifetime>();
        internal static ConcurrentDictionary<Type, object> ServiceNoAop = new ConcurrentDictionary<Type, object>();

        public static void Init(string nameSpaceService, WebType webType, Type aopType = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (string.IsNullOrEmpty(nameSpaceService))
                return;

            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
            {
                if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                    try { Assembly.Load(a.Name); } catch (Exception) { }
            });

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                Type interfaceType;
                try
                {
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                    {
                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (b.IsGenericType && b.GetGenericArguments().ToList().Select(a => a.FullName).ToList().Exists(n => n == null))
                            return;

                        if (b.BaseType == typeof(FastAopAttribute))
                            return;

                        if (b.BaseType == typeof(Attribute))
                            return;

                        b.GetConstructors().ToList().ForEach(c =>
                        {
                            Constructor.Constructor.depth = 0;
                            c.GetParameters().ToList().ForEach(p =>
                            {
                                Constructor.Constructor.Param(p.ParameterType, aopType);
                            });
                        });

                        interfaceType = b;
                        if (!b.IsInterface && b.GetInterfaces().Any())
                        {
                            foreach (var iface in b.GetInterfaces())
                            {
                                ServiceAopType.SetValue(iface, aopType);
                                ServiceTime.SetValue(iface, lifetime);
                                ServiceInstance.SetValue(iface, Instance(b, iface, aopType));
                                ServiceType.SetValue(iface, b);
                            }
                        }
                        else if (!b.IsInterface && !b.GetInterfaces().Any())
                        {
                            var model = Constructor.Constructor.Get(b, null);
                            var obj = Activator.CreateInstance(b, model.dynParam.ToArray());
                            Dic.SetValueDyn(interfaceType, obj);
                            ServiceTime.SetValue(interfaceType, lifetime);
                            ServiceAopType.SetValue(interfaceType, aopType);
                            ServiceType.SetValue(interfaceType, b);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (ex is AopException)
                        throw ex;
                }
            });

            InitAutowired(nameSpaceService, aopType, lifetime);

            if (webType == WebType.WebForm)
                return;

            if (webType == WebType.Mvc)
                System.Web.Mvc.ControllerBuilder.Current.SetControllerFactory(new AopMvcFactory());

            if (webType == WebType.WebApi)
                System.Web.Http.GlobalConfiguration.Configuration.Services.Replace(typeof(System.Web.Http.Dispatcher.IHttpControllerActivator), new AopWebApiFactory());

            if (webType == WebType.MvcAndWebApi)
            {
                System.Web.Mvc.ControllerBuilder.Current.SetControllerFactory(new AopMvcFactory());
                System.Web.Http.GlobalConfiguration.Configuration.Services.Replace(typeof(System.Web.Http.Dispatcher.IHttpControllerActivator), new AopWebApiFactory());
            }
        }

        public static void InitGeneric(string nameSpaceService, string nameSpaceModel, WebType webType, Type aopType = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (string.IsNullOrEmpty(nameSpaceService))
                return;

            Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
            {
                if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                    try { Assembly.Load(a.Name); } catch (Exception) { }
            });

            var list = InitModelType(nameSpaceModel);

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;
                try
                {
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                    {
                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (!b.IsGenericType)
                        {
                            Init(b.Namespace, webType, aopType);
                            return;
                        }

                        if (b.BaseType == typeof(FastAopAttribute))
                            return;

                        if (b.BaseType == typeof(Attribute))
                            return;

                        if (b.IsGenericType)
                        {
                            list.ForEach(m =>
                            {
                                b.GetConstructors().ToList().ForEach(c =>
                                {
                                    Constructor.Constructor.depth = 0;
                                    c.GetParameters().ToList().ForEach(p =>
                                    {
                                        Constructor.Constructor.Param(p.ParameterType, aopType);
                                    });
                                });

                                if (!b.IsInterface && b.GetInterfaces().Any())
                                {
                                    var sType = b.MakeGenericType(new Type[1] { m });

                                    foreach (var iface in sType.GetInterfaces())
                                    {
                                        ServiceInstance.SetValue(iface, FastAop.Instance(sType, iface, aopType));
                                        ServiceAopType.SetValue(iface, aopType);
                                        ServiceType.SetValue(iface, sType);
                                        ServiceTime.SetValue(iface, lifetime);
                                    }
                                }
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (ex is AopException)
                        throw ex;
                }
            });

            InitAutowiredGeneric(nameSpaceModel, webType, lifetime, aopType);

            if (webType == WebType.WebForm)
                return;

            if (webType == WebType.Mvc)
                System.Web.Mvc.ControllerBuilder.Current.SetControllerFactory(new AopMvcFactory());

            if (webType == WebType.WebApi)
                System.Web.Http.GlobalConfiguration.Configuration.Services.Replace(typeof(System.Web.Http.Dispatcher.IHttpControllerActivator), new AopWebApiFactory());

            if (webType == WebType.MvcAndWebApi)
            {
                System.Web.Mvc.ControllerBuilder.Current.SetControllerFactory(new AopMvcFactory());
                System.Web.Http.GlobalConfiguration.Configuration.Services.Replace(typeof(System.Web.Http.Dispatcher.IHttpControllerActivator), new AopWebApiFactory());
            }
        }

        private static void InitAutowired(string nameSpaceService, Type aopType = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            Type interfaceType;
            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;
                try
                {
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                    {
                        if (b.IsGenericType)
                            return;

                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (b.IsInterface)
                            return;

                        interfaceType = b;
                        if (b.GetInterfaces().Any())
                        {
                            foreach (var iface in b.GetInterfaces())
                            {
                                var obj = Instance(b, iface, lifetime);

                                if (obj == null)
                                    return;

                                ServiceAopType.SetValue(iface, aopType);
                                ServiceType.SetValue(iface, b);
                                ServiceInstance.SetValue(iface, obj);
                                ServiceTime.SetValue(iface, lifetime);
                            }
                        }
                        else
                        {
                            var obj = Instance(b, null, lifetime);

                            if (obj == null)
                                return;

                            Dic.SetValueDyn(b, obj);
                            ServiceAopType.SetValue(interfaceType, aopType);
                            ServiceType.SetValue(interfaceType, b);
                            ServiceTime.SetValue(interfaceType, lifetime);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (ex is AopException)
                        throw ex;
                }
            });
        }

        internal static object Instance(Type b, Type iface, ServiceLifetime lifetime = ServiceLifetime.Scoped, bool isReInstance = false)
        {
            object obj = null, temp = null;
            foreach (var item in b.GetRuntimeFields())
            {
                if (item.GetCustomAttribute<Autowired>() == null)
                    continue;

                if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                    throw new AopException($"{b.Name} field {item} attribute must readonly");

                if (item.FieldType.isSysType())
                    throw new AopException($"{b.Name} field {item} is system type not support");

                obj = Dic.GetValueDyn(b);

                if (b.IsInterface && obj == null)
                    obj = ServiceInstance.GetValue(b);

                if (b.GetInterfaces().Any() && obj == null)
                    obj = ServiceInstance.GetValue(iface);

                if (obj == null && !isReInstance)
                    continue;

                if (obj == null && isReInstance)
                    obj = Instance(b, iface, FastAop.ServiceAopType.GetValue(iface));

                if (temp == null)
                {
                    var model = Constructor.Constructor.Get(b, null);
                    temp = Activator.CreateInstance(b, model.dynParam.ToArray());
                }

                foreach (var param in temp.GetType().GetRuntimeFields())
                {
                    if (param.GetCustomAttribute<Autowired>() == null)
                        continue;

                    if (!param.Attributes.HasFlag(FieldAttributes.InitOnly))
                        throw new AopException($"{b.Name} field {item} attribute must readonly");

                    if (item.FieldType.isSysType())
                        throw new AopException($"{b.Name} field {item} is system type not support");

                    if (param.FieldType.IsInterface)
                    {
                        var fieldType = isReInstance ? ServiceType.GetValue(param.FieldType) : ServiceInstance.GetValue(param.FieldType)?.GetType();
                        if (fieldType != null)
                            Instance(fieldType, param.FieldType, lifetime, isReInstance);
                    }
                    else if (param.FieldType.GetInterfaces().Any())
                    {
                        foreach (var iType in param.FieldType.GetInterfaces())
                        {
                            var fieldType = isReInstance ? ServiceType.GetValue(iType) : ServiceInstance.GetValue(iType).GetType();
                            Instance(fieldType, iType, lifetime, isReInstance);
                        }
                    }
                    else
                    {
                        var fieldType = isReInstance ? ServiceType.GetValue(param.FieldType) : Dic.GetValueDyn(param.FieldType).GetType();
                        Instance(fieldType, iface, lifetime, isReInstance);
                    }
                }

                if (item.FieldType.IsInterface)
                    item.SetValueDirect(__makeref(temp), ServiceInstance.GetValue(item.FieldType));
                else if (item.FieldType.GetInterfaces().Any())
                    item.SetValueDirect(__makeref(temp), ServiceInstance.GetValue(iface));
                else
                    item.SetValue(temp, Dic.GetValueDyn(item.FieldType));

                var objFildType = obj.GetType().GetRuntimeFields().First().FieldType;
                if (!objFildType.IsInterface && !objFildType.GetInterfaces().Any())
                    obj.GetType().GetRuntimeFields().First().SetValue(obj, temp);
                else if (obj.GetType().GetRuntimeFields().First().FieldType == temp.GetType())
                    obj.GetType().GetRuntimeFields().First().SetValueDirect(__makeref(obj), temp);

                if (obj.GetType().FullName.EndsWith(".dynamic"))
                    objFildType.GetRuntimeFields().First().SetValue(obj, Dic.GetValueDyn(item.FieldType));
            }

            if (iface != null)
                return obj;
            else
                return temp;
        }

        private static void InitAutowiredGeneric(string nameSpaceModel, WebType webType, ServiceLifetime lifetime, Type aopType = null)
        {
            var list = InitModelType(nameSpaceModel);

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                try
                {
                    assembly.ExportedTypes.ToList().ForEach(b =>
                    {
                        object obj = null, temp = null;

                        foreach (var item in b.GetRuntimeFields())
                        {
                            if (item.GetCustomAttribute<Autowired>() == null)
                                continue;

                            if (!b.IsGenericType)
                                continue;

                            if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                                throw new AopException($"{b.Name} field {item} attribute must readonly");

                            if (item.FieldType.isSysType())
                                throw new AopException($"{b.Name} field {item} is system type not support");

                            if (ServiceInstance.GetValue(item.FieldType) == null)
                                FastAop.Init(b.Namespace, webType, aopType);

                            if (!b.IsInterface && !b.GetInterfaces().Any())
                                continue;

                            list.ForEach(m =>
                            {
                                var type = b.MakeGenericType(new Type[1] { m });
                                if (b.IsInterface)
                                    obj = ServiceInstance.GetValue(type);

                                if (b.GetInterfaces().Any())
                                    obj = ServiceInstance.GetValue(type.GetInterfaces().First());

                                if (obj == null)
                                    return;

                                if (temp == null)
                                {
                                    var tempType = obj.GetType().GetRuntimeFields().First().FieldType;
                                    var model = Constructor.Constructor.Get(tempType, null);
                                    temp = Activator.CreateInstance(tempType, model.dynParam.ToArray());
                                }
                                var newItem = temp.GetType().GetRuntimeFields().ToList().Find(a => a.FieldType == item.FieldType && a.Name == item.Name);

                                if (item.FieldType.IsInterface)
                                    newItem.SetValueDirect(__makeref(temp), ServiceInstance.GetValue(item.FieldType));
                                else
                                    newItem.SetValue(temp, Dic.GetValueDyn(item.FieldType));

                                obj.GetType().GetRuntimeFields().First().SetValueDirect(__makeref(obj), temp);
                            });
                        }

                        if (obj == null)
                            return;

                        list.ForEach(m =>
                        {
                            var sType = b.MakeGenericType(new Type[1] { m });
                            var interfaceType = sType;
                            if (b.GetInterfaces().Any())
                            {
                                foreach (var iface in sType.GetInterfaces())
                                {
                                    ServiceInstance.SetValue(iface, obj);
                                    ServiceAopType.SetValue(iface, aopType);
                                    ServiceType.SetValue(iface, sType);
                                    ServiceTime.SetValue(iface, lifetime);
                                }
                            }
                            else
                            {
                                Dic.SetValueDyn(interfaceType, obj);
                                ServiceAopType.SetValue(interfaceType, aopType);
                                ServiceType.SetValue(interfaceType, b);
                                ServiceTime.SetValue(interfaceType, lifetime);
                            }
                        });
                    });
                }
                catch (Exception ex)
                {
                    if (ex is AopException)
                        throw ex;
                }
            });
        }

        private static object InstanceGeneric(List<Type> list, Type b, WebType webType, ServiceLifetime lifetime)
        {
            object obj = null, temp = null;
            foreach (var item in b.GetRuntimeFields())
            {
                if (item.GetCustomAttribute<Autowired>() == null)
                    continue;

                if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                    throw new AopException($"{b.Name} field {item} attribute must readonly");

                if (item.FieldType.isSysType())
                    throw new AopException($"{b.Name} field {item} is system type not support");

                if (ServiceInstance.GetValue(item.FieldType) == null)
                    Init(b.Namespace, webType);

                if (!b.IsInterface && !b.GetInterfaces().Any())
                    continue;

                list.ForEach(m =>
                {
                    if (obj == null)
                    {
                        var typeGeneric = b.MakeGenericType(new Type[1] { m });
                        if (b.IsInterface)
                            obj = ServiceInstance.GetValue(typeGeneric);

                        if (b.GetInterfaces().Any())
                            obj = ServiceInstance.GetValue(typeGeneric.GetInterfaces().First());
                    }

                    if (obj == null)
                        return;

                    var type = obj.GetType().GetRuntimeFields().First().FieldType;
                    if (temp == null)
                    {
                        var model = Constructor.Constructor.Get(type, null);
                        temp = Activator.CreateInstance(type, model.dynParam.ToArray());
                    }

                    foreach (var param in temp.GetType().GetRuntimeFields())
                    {
                        if (param.GetCustomAttribute<Autowired>() == null)
                            continue;

                        if (!param.Attributes.HasFlag(FieldAttributes.InitOnly))
                            throw new AopException($"{type.Name} field {item} attribute must readonly");

                        if (param.FieldType.isSysType())
                            throw new AopException($"{type.Name} field {item} is system type not support");

                        if (param.FieldType.IsInterface)
                            InstanceGeneric(list, ServiceInstance.GetValue(param.FieldType).GetType(), webType, lifetime);
                        else if (param.FieldType.GetInterfaces().Any())
                            InstanceGeneric(list, ServiceInstance.GetValue(param.FieldType.GetInterfaces().First()).GetType(), webType, lifetime);
                    }

                    var newItem = temp.GetType().GetRuntimeFields().ToList().Find(a => a.FieldType == item.FieldType && a.Name == item.Name);
                    if (item.FieldType.IsInterface)
                        newItem.SetValueDirect(__makeref(temp), ServiceInstance.GetValue(item.FieldType));
                    else if (item.FieldType.GetInterfaces().Any())
                        newItem.SetValueDirect(__makeref(temp), ServiceInstance.GetValue(item.FieldType.GetInterfaces().First()));
                    else
                        newItem.SetValue(temp, Dic.GetValueDyn(item.FieldType));

                    obj.GetType().GetRuntimeFields().First().SetValueDirect(__makeref(obj), temp);
                });
            }

            return obj;
        }

        public static void AddScoped<S, I>(Type aopType = null)
        {
            var serviceType = typeof(S);
            var interfaceType = typeof(I);
            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (!serviceType.GetInterfaces().ToList().Exists(a => a == interfaceType))
                throw new Exception($"serviceType:{serviceType.Name}, getInterfaces class not have Interfaces class:{interfaceType.Name}");

            ServiceAopType.SetValue(interfaceType, aopType);
            ServiceTime.SetValue(interfaceType, ServiceLifetime.Scoped);
            ServiceInstance.SetValue(interfaceType, Instance(serviceType, interfaceType, aopType));
            ServiceType.SetValue(interfaceType, serviceType);
        }

        public static void AddScoped(Type interfaceType, object obj)
        {
            var serviceType = obj.GetType();

            if (!serviceType.GetInterfaces().ToList().Exists(a => a == interfaceType))
                throw new Exception($"serviceType:{serviceType.Name}, getInterfaces class not have Interfaces class:{interfaceType.Name}");

            ServiceAopType.SetValue(interfaceType, null);
            ServiceTime.SetValue(interfaceType, ServiceLifetime.Scoped);
            ServiceInstance.SetValue(interfaceType, obj);
            ServiceType.SetValue(interfaceType, serviceType);
            ServiceNoAop.SetValue(interfaceType, true);
        }

        public static void AddSingleton<S, I>(Type aopType = null)
        {
            var serviceType = typeof(S);
            var interfaceType = typeof(I);
            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (!serviceType.GetInterfaces().ToList().Exists(a => a == interfaceType))
                throw new Exception($"serviceType:{serviceType.Name}, getInterfaces class not have Interfaces class:{interfaceType.Name}");

            ServiceAopType.SetValue(interfaceType, aopType);
            ServiceTime.SetValue(interfaceType, ServiceLifetime.Singleton);
            ServiceInstance.SetValue(interfaceType, Instance(serviceType, interfaceType, aopType));
            ServiceType.SetValue(interfaceType, serviceType);
        }

        public static void AddSingleton(Type interfaceType, object obj)
        {
            var serviceType = obj.GetType();

            if (!serviceType.GetInterfaces().ToList().Exists(a => a == interfaceType))
                throw new Exception($"serviceType:{serviceType.Name}, getInterfaces class not have Interfaces class:{interfaceType.Name}");

            ServiceAopType.SetValue(interfaceType, null);
            ServiceTime.SetValue(interfaceType, ServiceLifetime.Singleton);
            ServiceInstance.SetValue(interfaceType, obj);
            ServiceType.SetValue(interfaceType, serviceType);
            ServiceNoAop.SetValue(interfaceType, true);
        }

        public static void AddTransient<S, I>(Type aopType = null)
        {
            var serviceType = typeof(S);
            var interfaceType = typeof(I);
            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (!serviceType.GetInterfaces().ToList().Exists(a => a == interfaceType))
                throw new Exception($"serviceType:{serviceType.Name}, getInterfaces class not have Interfaces class:{interfaceType.Name}");

            ServiceAopType.SetValue(interfaceType, aopType);
            ServiceTime.SetValue(interfaceType, ServiceLifetime.Transient);
            ServiceInstance.SetValue(interfaceType, Instance(serviceType, interfaceType, aopType));
            ServiceType.SetValue(interfaceType, serviceType);
        }

        public static void AddTransient(Type interfaceType, object obj)
        {
            var serviceType = obj.GetType();

            if (!serviceType.GetInterfaces().ToList().Exists(a => a == interfaceType))
                throw new Exception($"serviceType:{serviceType.Name}, getInterfaces class not have Interfaces class:{interfaceType.Name}");

            ServiceAopType.SetValue(interfaceType, null);
            ServiceTime.SetValue(interfaceType, ServiceLifetime.Transient);
            ServiceInstance.SetValue(interfaceType, obj);
            ServiceType.SetValue(interfaceType, serviceType);
            ServiceNoAop.SetValue(interfaceType, true);
        }

        public static I Resolve<I>()
        {
            return (I)ServiceInstance.GetValue(typeof(I));
        }
    }
}
