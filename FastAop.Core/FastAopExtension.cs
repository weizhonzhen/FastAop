﻿using FastAop.Core;
using FastAop.Core.Constructor;
using FastAop.Core.Factory;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class FastAopExtension
    {
        internal static IServiceProvider serviceProvider;

        private static void InitAssembly()
        {
            Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
            {
                if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                    try { Assembly.Load(a.Name); } catch (Exception ex) { }
            });

        }

        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string nameSpaceService, Type aopType = null, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            if (string.IsNullOrEmpty(nameSpaceService))
                return serviceCollection;

            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            InitAssembly();

            serviceProvider = serviceCollection.BuildServiceProvider();

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                try
                {
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                    {
                        if (b.IsGenericType && b.GetGenericArguments().ToList().Select(a => a.FullName).ToList().Exists(n => n == null))
                            return;

                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (b.BaseType == typeof(FastAopAttribute))
                            return;

                        if (b.BaseType == typeof(Attribute))
                            return;

                        b.GetConstructors().ToList().ForEach(c =>
                        {
                            Constructor.depth = 0;
                            c.GetParameters().ToList().ForEach(p =>
                            {
                                Constructor.Param(serviceCollection, p.ParameterType, aopType, serviceLifetime);
                            });
                        });

                        if (!b.IsInterface && b.GetInterfaces().Any())
                        {
                            foreach (var iface in b.GetInterfaces())
                            {
                                var obj = FastAop.Core.FastAop.Instance(b, iface, aopType);
                                if (obj == null)
                                    return;
                                serviceCollection.AddService(serviceLifetime, iface, obj);
                            }
                        }
                        else if (!b.IsInterface && !b.GetInterfaces().Any())
                        {
                            var model = Constructor.Get(b, null);
                            var obj = Activator.CreateInstance(b, model.dynParam.ToArray());
                            if (obj == null)
                                return;
                            AddService(serviceCollection, serviceLifetime, b, obj);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (ex is AopException)
                        throw ex;
                }
            });

            serviceProvider = serviceCollection.BuildServiceProvider();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IControllerActivator)));
            serviceCollection.AddSingleton<IControllerActivator, AopControllerFactory>();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IPageModelActivatorProvider)));
            serviceCollection.AddSingleton<IPageModelActivatorProvider, AopPageFactory>();

            serviceCollection.AddFastAopAutowired(nameSpaceService);

            return serviceCollection;
        }

        public static IServiceCollection AddFastAopGeneric(this IServiceCollection serviceCollection, string nameSpaceService, string nameSpaceModel, Type aopType = null, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            if (string.IsNullOrEmpty(nameSpaceService))
                return serviceCollection;

            InitAssembly();

            serviceProvider = serviceCollection.BuildServiceProvider();
            var list = InitModelType(nameSpaceModel);

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                try
                {
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                    {
                        if (!b.IsGenericType)
                            return;

                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (b.BaseType == typeof(FastAopAttribute))
                            return;

                        if (b.BaseType == typeof(Attribute))
                            return;

                        b.GetConstructors().ToList().ForEach(c =>
                        {
                            Constructor.depth = 0;
                            c.GetParameters().ToList().ForEach(p =>
                            {
                                Constructor.Param(serviceCollection, p.ParameterType, aopType, serviceLifetime);
                            });
                        });


                        if (b.IsGenericType)
                        {
                            list.ForEach(m =>
                            {
                                b.GetConstructors().ToList().ForEach(c =>
                                {
                                    Constructor.depth = 0;
                                    c.GetParameters().ToList().ForEach(p =>
                                    {
                                        Constructor.Param(serviceCollection, p.ParameterType, aopType, serviceLifetime);
                                    });
                                });

                                if (!b.IsInterface && b.GetInterfaces().Any())
                                {
                                    var serviceType = b.MakeGenericType(new Type[1] { m });
                                    foreach (var iface in serviceType.GetInterfaces())
                                    {
                                        var obj = FastAop.Core.FastAop.Instance(serviceType, iface, aopType);
                                        serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == iface));
                                        serviceCollection.AddService(serviceLifetime, iface, obj);
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

            serviceProvider = serviceCollection.BuildServiceProvider();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IControllerActivator)));
            serviceCollection.AddSingleton<IControllerActivator, AopControllerFactory>();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IPageModelActivatorProvider)));
            serviceCollection.AddSingleton<IPageModelActivatorProvider, AopPageFactory>();

            serviceCollection.AddFastAopAutowiredGeneric(nameSpaceService, nameSpaceModel, aopType);

            return serviceCollection;
        }

        public static IServiceCollection AddFastAopAutowired(this IServiceCollection serviceCollection, string nameSpaceService, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            var isFastAopCall = true;
            if (serviceProvider == null)
            {
                isFastAopCall = false;
                serviceProvider = serviceCollection.BuildServiceProvider();
                InitAssembly();
            }

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

                        var type = b;
                        if (b.GetInterfaces().Any())
                        {
                            foreach (var iface in type.GetInterfaces())
                            {
                                var obj = Instance(b, iface, isFastAopCall);
                                if (obj == null)
                                    return;
                                serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == iface));
                                serviceCollection.AddService(serviceLifetime, iface, obj);
                            }
                        }
                        else
                        {
                            var obj = Instance(b, null, isFastAopCall);
                            if (obj == null)
                                return;
                            serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == type));
                            serviceCollection.AddService(serviceLifetime, type, obj);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (ex is AopException)
                        throw ex;
                }
            });

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IControllerActivator)));
            serviceCollection.AddSingleton<IControllerActivator, AopControllerFactory>();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IPageModelActivatorProvider)));
            serviceCollection.AddSingleton<IPageModelActivatorProvider, AopPageFactory>();

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        public static IServiceCollection AddFastAopAutowiredGeneric(this IServiceCollection serviceCollection, string nameSpaceService, string nameSpaceModel, Type aopType = null, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            var isFastAopCall = true;
            if (serviceProvider == null)
            {
                isFastAopCall = false;
                serviceProvider = serviceCollection.BuildServiceProvider();
                InitAssembly();
            }

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
                            AddFastAop(serviceCollection, b.Namespace, aopType);
                            return;
                        }

                        var obj = InstanceGeneric(serviceCollection, list, b, isFastAopCall);

                        if (obj == null)
                            return;

                        list.ForEach(m =>
                        {
                            var type = b.MakeGenericType(new Type[1] { m });
                            if (b.GetInterfaces().Any())
                            {
                                foreach (var iface in type.GetInterfaces())
                                {
                                    serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == iface));
                                    serviceCollection.AddService(serviceLifetime, iface, obj);
                                }
                            }
                            else
                            {
                                serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == type));
                                serviceCollection.AddService(serviceLifetime, type, obj);
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

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IControllerActivator)));
            serviceCollection.AddSingleton<IControllerActivator, AopControllerFactory>();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IPageModelActivatorProvider)));
            serviceCollection.AddSingleton<IPageModelActivatorProvider, AopPageFactory>();

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        public static IServiceCollection AddFastAopScoped<S, I>(this IServiceCollection serviceCollection, Type aopType = null)
        {
            var serviceType = typeof(S);
            var interfaceType = typeof(I);
            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (!serviceType.GetInterfaces().ToList().Exists(a => a == interfaceType))
                throw new Exception($"serviceType:{serviceType.Name}, getInterfaces class not have Interfaces class:{interfaceType.Name}");

            var obj = FastAop.Core.FastAop.Instance(serviceType, interfaceType, aopType);
            serviceCollection.AddScoped(interfaceType, s => { return obj; });

            return serviceCollection;
        }

        public static IServiceCollection AddFastAopSingleton<S, I>(this IServiceCollection serviceCollection, Type aopType = null)
        {
            var serviceType = typeof(S);
            var interfaceType = typeof(I);
            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (!serviceType.GetInterfaces().ToList().Exists(a => a == interfaceType))
                throw new Exception($"serviceType:{serviceType.Name}, getInterfaces class not have Interfaces class:{interfaceType.Name}");

            var obj = FastAop.Core.FastAop.Instance(serviceType, interfaceType, aopType);
            serviceCollection.AddScoped(interfaceType, s => { return obj; });

            return serviceCollection;
        }

        public static IServiceCollection AddFastAopTransient<S, I>(this IServiceCollection serviceCollection, Type aopType = null)
        {
            var serviceType = typeof(S);
            var interfaceType = typeof(I);
            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (!serviceType.GetInterfaces().ToList().Exists(a => a == interfaceType))
                throw new Exception($"serviceType:{serviceType.Name}, getInterfaces class not have Interfaces class:{interfaceType.Name}");

            var obj = FastAop.Core.FastAop.Instance(serviceType, interfaceType, aopType);
            serviceCollection.AddScoped(interfaceType, s => { return obj; });

            return serviceCollection;
        }

        private static object Instance(Type b, Type iface, bool isFastAopCall)
        {
            object obj = null, temp = null;
            foreach (var item in b.GetRuntimeFields())
            {
                if (item.GetCustomAttribute<Autowired>() == null)
                    continue;

                if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                    throw new AopException($"{b.Name} field {item} attribute must readonly");

                if (item.FieldType.isSysType())
                    throw new Exception($"{b.Name} field {item} is system type not support");

                if (serviceProvider.GetService(item.FieldType) == null && isFastAopCall && item.FieldType.IsGenericType)
                    throw new Exception($"AddFastAopGeneric Method first，{item.FieldType.FullName} is Generic Type");

                if (serviceProvider.GetService(item.FieldType) == null && isFastAopCall)
                    throw new Exception($"AddFastAop Method，{item.FieldType.FullName} not in ServiceCollection");

                if (serviceProvider.GetService(item.FieldType) == null && !isFastAopCall)
                    throw new Exception($"{item.FieldType.FullName} not in ServiceCollection");

                obj = serviceProvider.GetService(b);

                if (b.IsInterface && obj == null)
                    obj = serviceProvider.GetService(b);

                if (b.GetInterfaces().Any() && obj == null)
                    obj = serviceProvider.GetService(iface);

                if (obj == null)
                    continue;

                if (temp == null)
                {
                    var model = Constructor.Get(b, null);
                    temp = Activator.CreateInstance(b, model.dynParam.ToArray());
                }

                foreach (var param in temp.GetType().GetRuntimeFields())
                {
                    if (param.GetCustomAttribute<Autowired>() == null)
                        continue;

                    if (!param.Attributes.HasFlag(FieldAttributes.InitOnly))
                        throw new AopException($"{b.Name} field {item} attribute must readonly");

                    if (param.FieldType.isSysType())
                        throw new Exception($"{b.Name} field {item} is system type not support");

                    if (param.FieldType.IsInterface)
                        Instance(serviceProvider.GetService(param.FieldType).GetType(), param.FieldType, isFastAopCall);
                    else if (param.FieldType.GetInterfaces().Any())
                    {
                        foreach (var iType in param.FieldType.GetInterfaces())
                        {
                            Instance(serviceProvider.GetService(iType).GetType(), iType, isFastAopCall);
                        }
                    }
                }

                if (!item.FieldType.IsInterface && !item.FieldType.GetInterfaces().Any())
                    item.SetValue(temp, serviceProvider.GetService(item.FieldType));
                else
                    item.SetValueDirect(__makeref(temp), serviceProvider.GetService(item.FieldType));

                var objFildType = obj.GetType().GetRuntimeFields().First().FieldType;
                if (!objFildType.IsInterface && !objFildType.GetInterfaces().Any())
                    obj.GetType().GetRuntimeFields().First().SetValue(obj, temp);
                else if (obj.GetType().GetRuntimeFields().First().FieldType == temp.GetType())
                    obj.GetType().GetRuntimeFields().First().SetValueDirect(__makeref(obj), temp);

                if (obj.GetType().FullName.EndsWith(".dynamic"))
                    objFildType.GetRuntimeFields().First().SetValue(obj, serviceProvider.GetService(item.FieldType));
            }

            if (iface != null)
                return obj;
            else
                return temp;
        }

        private static object InstanceGeneric(IServiceCollection serviceCollection, List<Type> list, Type b, bool isFastAopCall)
        {
            object obj = null, temp = null;
            foreach (var item in b.GetRuntimeFields())
            {
                if (item.GetCustomAttribute<Autowired>() == null)
                    continue;

                if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                    throw new AopException($"{b.Name} field {item} attribute must readonly");

                if (item.FieldType.isSysType())
                    throw new Exception($"{item.Name} is system type not support");

                if (serviceProvider.GetService(item.FieldType) == null)
                    serviceCollection.AddFastAop(b.Namespace);

                if (serviceProvider.GetService(item.FieldType) == null && isFastAopCall && item.FieldType.IsGenericType)
                    throw new Exception($"AddFastAopGeneric Method，{b.FullName} Field {item.FieldType.FullName} is Generic Type");

                if (serviceProvider.GetService(item.FieldType) == null && isFastAopCall)
                    throw new Exception($"AddFastAop Method，{b.FullName} Field {item.FieldType.FullName} not in ServiceCollection");

                if (serviceProvider.GetService(item.FieldType) == null && !isFastAopCall)
                    throw new Exception($"{b.FullName} Field {item.FieldType.FullName} not in ServiceCollection");

                if (!b.IsInterface && !b.GetInterfaces().Any())
                    continue;

                list.ForEach(m =>
                {
                    if (obj == null)
                    {
                        var typeGeneric = b.MakeGenericType(new Type[1] { m });
                        if (b.IsInterface)
                            obj = serviceProvider.GetService(typeGeneric);

                        if (b.GetInterfaces().Any())
                            obj = serviceProvider.GetService(typeGeneric.GetInterfaces().First());
                    }

                    if (obj == null)
                        return;

                    var type = obj.GetType().GetRuntimeFields().First().FieldType;
                    if (temp == null)
                    {
                        var model = Constructor.Get(type, null);
                        temp = Activator.CreateInstance(type, model.dynParam.ToArray());
                    }

                    foreach (var param in temp.GetType().GetRuntimeFields())
                    {
                        if (param.GetCustomAttribute<Autowired>() == null)
                            continue;

                        if (!param.Attributes.HasFlag(FieldAttributes.InitOnly))
                            throw new AopException($"{type.Name} field {item} attribute must readonly");

                        if (param.FieldType.isSysType())
                            throw new Exception($"{type.Name} field {item} is system type not support");

                        if (param.FieldType.GetInterfaces().Any())
                            InstanceGeneric(serviceCollection, list, serviceProvider.GetService(param.FieldType.GetInterfaces().First()).GetType(), isFastAopCall);
                        else
                            InstanceGeneric(serviceCollection, list, serviceProvider.GetService(param.FieldType).GetType(), isFastAopCall);
                    }

                    var newItem = temp.GetType().GetRuntimeFields().ToList().Find(a => a.FieldType == item.FieldType && a.Name == item.Name);

                    if (!item.FieldType.IsInterface && !item.FieldType.GetInterfaces().Any())
                        newItem.SetValue(temp, serviceProvider.GetService(item.FieldType));
                    else
                        newItem.SetValueDirect(__makeref(temp), serviceProvider.GetService(item.FieldType));

                    var objFildType = obj.GetType().GetRuntimeFields().First().FieldType;
                    if (!objFildType.IsInterface && !objFildType.GetInterfaces().Any())
                        obj.GetType().GetRuntimeFields().First().SetValue(obj, temp);
                    else
                        obj.GetType().GetRuntimeFields().First().SetValueDirect(__makeref(obj), temp);
                });
            }

            return obj;
        }

        private static List<Type> InitModelType(string nameSpaceModel)
        {
            var list = new List<Type>();
            if (string.IsNullOrEmpty(nameSpaceModel))
                return list;

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                try
                {
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceModel)).ToList().ForEach(b =>
                    {
                        if (b.IsPublic && b.IsClass && !b.IsAbstract && !b.IsGenericType)
                            list.Add(b);
                    });
                }
                catch { }
            });

            return list;
        }

        private static void AddService(this IServiceCollection serviceCollection, ServiceLifetime serviceLifetime, Type iface, object obj)
        {
            if (serviceLifetime == ServiceLifetime.Scoped)
                serviceCollection.AddScoped(iface, s => { return obj; });

            if (serviceLifetime == ServiceLifetime.Transient)
                serviceCollection.AddTransient(iface, s => { return obj; });

            if (serviceLifetime == ServiceLifetime.Singleton)
                serviceCollection.AddSingleton(iface, s => { return obj; });
        }
    }
}