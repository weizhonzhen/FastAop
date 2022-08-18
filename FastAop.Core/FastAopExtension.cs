using FastAop.Core;
using FastAop.Core.Constructor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class FastAopExtension
    {
        internal static IServiceProvider serviceProvider;

        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string nameSpaceService)
        {
            if (string.IsNullOrEmpty(nameSpaceService))
                return serviceCollection;

            Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
            {
                if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                    try { Assembly.Load(a.Name); } catch (Exception ex) { }
            });

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                {
                    var isServiceAttr = false;
                    b.GetMethods().ToList().ForEach(m =>
                    {
                        if (m.GetCustomAttributes().ToList().Exists(a => a.GetType().BaseType == typeof(FastAopAttribute)))
                            isServiceAttr = true;
                    });

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
                            Constructor.Param(serviceCollection, p.ParameterType, isServiceAttr);
                            serviceProvider = serviceCollection.BuildServiceProvider();
                        });
                    });

                    if (!b.IsInterface && b.GetInterfaces().Any() && isServiceAttr)
                        serviceCollection.AddScoped(b.GetInterfaces().First(), FastAop.Core.FastAop.Instance(b, b.GetInterfaces().First()).GetType());
                    else if (!b.IsInterface && b.GetInterfaces().Any())
                        serviceCollection.AddScoped(b.GetInterfaces().First(), b);
                    else if (!b.IsInterface && !b.GetInterfaces().Any() && isServiceAttr)
                        serviceCollection.AddScoped(b, s => { return FastAopDyn.Instance(b); });
                    else if (!b.IsInterface && !b.GetInterfaces().Any() && b.GetConstructors().Length == 0)
                        serviceCollection.AddScoped(b, s => { return Activator.CreateInstance(b); });
                    else if (!b.IsInterface && !b.GetInterfaces().Any() && b.GetConstructors().Length > 0)
                    {
                        var model = Constructor.Get(b, null);
                        serviceCollection.AddScoped(b, s => { return Activator.CreateInstance(b, model.dynParam.ToArray()); });
                    }
                });
            });

            serviceProvider = serviceCollection.BuildServiceProvider();

            serviceCollection.AddFastAopAutowired();

            return serviceCollection;
        }

        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string nameSpaceService, Type aopType)
        {
            if (string.IsNullOrEmpty(nameSpaceService))
                return serviceCollection;

            if (aopType.BaseType != typeof(FastAopAttribute))
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
                            Constructor.Param(serviceCollection, p.ParameterType, aopType);
                            serviceProvider = serviceCollection.BuildServiceProvider();
                        });
                    });

                    if (!b.IsInterface && b.GetInterfaces().Any())
                        serviceCollection.AddScoped(b.GetInterfaces().First(), FastAop.Core.FastAop.Instance(b, b.GetInterfaces().First(), aopType).GetType());
                    else if (!b.IsInterface && !b.GetInterfaces().Any())
                        serviceCollection.AddScoped(b, s => { return FastAopDyn.Instance(b, aopType); });
                });
            });

            serviceProvider = serviceCollection.BuildServiceProvider();

            serviceCollection.AddFastAopAutowired();

            return serviceCollection;
        }

        public static IServiceCollection AddFastAopGeneric(this IServiceCollection serviceCollection, string nameSpaceService, string nameSpaceModel)
        {
            if (string.IsNullOrEmpty(nameSpaceService))
                return serviceCollection;

            Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
            {
                if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                    try { Assembly.Load(a.Name); } catch (Exception ex) { }
            });

            var list = InitModelType(nameSpaceModel);

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                {
                    var isServiceAttr = false;
                    b.GetMethods().ToList().ForEach(m =>
                    {
                        if (m.GetCustomAttributes().ToList().Exists(a => a.GetType().BaseType == typeof(FastAopAttribute)))
                            isServiceAttr = true;
                    });

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
                            Constructor.Param(serviceCollection, p.ParameterType, isServiceAttr);
                            serviceProvider = serviceCollection.BuildServiceProvider();
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
                                    Constructor.Param(serviceCollection, p.ParameterType, isServiceAttr);
                                });
                            });

                            if (!b.IsInterface && b.GetInterfaces().Any())
                            {
                                var serviceType = b.MakeGenericType(new Type[1] { m });
                                serviceCollection.AddScoped(serviceType.GetInterfaces().First(), FastAop.Core.FastAop.Instance(serviceType, serviceType.GetInterfaces().First()).GetType());
                            }
                        });
                    }
                });
            });

            serviceProvider = serviceCollection.BuildServiceProvider();

            serviceCollection.AddFastAopAutowiredGeneric(nameSpaceModel);

            return serviceCollection;
        }

        public static IServiceCollection AddFastAopGeneric(this IServiceCollection serviceCollection, string nameSpaceService, string nameSpaceModel, Type aopType)
        {
            if (string.IsNullOrEmpty(nameSpaceService))
                return serviceCollection;

            Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
            {
                if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                    try { Assembly.Load(a.Name); } catch (Exception ex) { }
            });

            var list = InitModelType(nameSpaceModel);

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

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
                            Constructor.Param(serviceCollection, p.ParameterType, aopType);
                            serviceProvider = serviceCollection.BuildServiceProvider();
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
                                    Constructor.Param(serviceCollection, p.ParameterType, aopType);
                                });
                            });

                            if (!b.IsInterface && b.GetInterfaces().Any())
                            {
                                var serviceType = b.MakeGenericType(new Type[1] { m });
                                serviceCollection.AddScoped(serviceType.GetInterfaces().First(), FastAop.Core.FastAop.Instance(serviceType, serviceType.GetInterfaces().First(), aopType).GetType());
                            }
                        });
                    }
                });
            });

            serviceProvider = serviceCollection.BuildServiceProvider();

            serviceCollection.AddFastAopAutowiredGeneric(nameSpaceModel);

            return serviceCollection;
        }

        public static IServiceCollection AddFastAopAutowired(this IServiceCollection serviceCollection)
        {
            var isFastAopCall = true;
            if (serviceProvider == null)
            {
                isFastAopCall = false;
                serviceProvider = serviceCollection.BuildServiceProvider();
                Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
                {
                    if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                        try { Assembly.Load(a.Name); } catch (Exception ex) { }
                });
            }

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                assembly.ExportedTypes.ToList().ForEach(b =>
                {
                    object obj = null,temp = null;

                    foreach (var item in b.GetRuntimeFields())
                    {
                        if (item.GetCustomAttribute<Autowired>() == null)
                            continue;

                        if (b.IsGenericType)
                            continue;

                        if (item.FieldType.isSysType())
                            throw new Exception($"{item.Name} is system type not support");

                        if (serviceProvider.GetService(item.FieldType) == null && isFastAopCall && item.FieldType.IsGenericType)
                            throw new Exception($"AddFastAopGeneric Method first，{item.FieldType.FullName} is Generic Type");

                        if (serviceProvider.GetService(item.FieldType) == null && isFastAopCall)
                            throw new Exception($"AddFastAop Method，{item.FieldType.FullName} not in ServiceCollection");

                        if (serviceProvider.GetService(item.FieldType) == null && !isFastAopCall)
                            throw new Exception($"{item.FieldType.FullName} not in ServiceCollection");

                        if (b.IsInterface)
                            obj = serviceProvider.GetService(b);
                        else if (b.GetInterfaces().Any())
                            obj = serviceProvider.GetService(b.GetInterfaces()[0]);
                        else
                            continue;

                        if (obj == null)
                            continue;

                        if (obj.GetType().FullName == "Aop_FastAop.ILGrator.Core")
                        {
                            if (obj.GetType().GetRuntimeFields().ToList()[0].FieldType.GetConstructors().ToList().Exists(a => a.GetParameters().Length > 0))
                                throw new Exception($"{item.Name}[Autowired]  not support Constructors {b.FullName}");
                            temp = temp ?? Activator.CreateInstance(obj.GetType().GetRuntimeFields().ToList()[0].FieldType);

                            if (item.FieldType.IsInterface)
                                item.SetValue(temp, serviceProvider.GetService(item.FieldType));
                            else
                                item.SetValue(temp, serviceProvider.GetService(item.FieldType));

                            obj.GetType().GetRuntimeFields().ToList()[0].SetValue(obj, temp);
                        }
                        else
                            item.SetValue(obj, serviceProvider.GetService(item.FieldType));
                    }

                    if (obj == null)
                        return;

                    if (b.GetInterfaces().Any())
                        serviceCollection.AddScoped(b.GetInterfaces()[0], s => { return obj; });
                    else
                        serviceCollection.AddScoped(b, s => { return obj; });
                });
            });

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        public static IServiceCollection AddFastAopAutowiredGeneric(this IServiceCollection serviceCollection,string nameSpaceModel)
        {
            var isFastAopCall = true;
            if (serviceProvider == null)
            {
                isFastAopCall = false;
                serviceProvider = serviceCollection.BuildServiceProvider();
                Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
                {
                    if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                        try { Assembly.Load(a.Name); } catch (Exception ex) { }
                });
            }

            var list = InitModelType(nameSpaceModel);

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                assembly.ExportedTypes.ToList().ForEach(b =>
                {
                    object obj = null, temp = null;

                    foreach (var item in b.GetRuntimeFields())
                    {
                        if (item.GetCustomAttribute<Autowired>() == null)
                            continue;

                        if (!b.IsGenericType)
                            continue;

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
                            var type = b.MakeGenericType(new Type[1] { m });
                            if (b.IsInterface)
                                obj = serviceProvider.GetService(type);
                            
                           if (b.GetInterfaces().Any())
                                obj = serviceProvider.GetService(type.GetInterfaces().First());

                            if (obj == null)
                                return;

                            if (obj.GetType().FullName == "Aop_FastAop.ILGrator.Core")
                            {
                                if (obj.GetType().GetRuntimeFields().ToList()[0].FieldType.GetConstructors().ToList().Exists(a => a.GetParameters().Length > 0))
                                    throw new Exception($"{item.Name}[Autowired]  not support Constructors {b.FullName}");
                                temp = temp ?? Activator.CreateInstance(obj.GetType().GetRuntimeFields().ToList()[0].FieldType);
                                var newItem = temp.GetType().GetRuntimeFields().ToList().Find(a => a.FieldType == item.FieldType && a.Name == item.Name);

                                if (item.FieldType.IsInterface)
                                    newItem.SetValue(temp, serviceProvider.GetService(item.FieldType));
                                else
                                    newItem.SetValue(temp, serviceProvider.GetService(item.FieldType));

                                obj.GetType().GetRuntimeFields().ToList()[0].SetValue(obj, temp);
                            }
                            else
                            {
                                var newItem = obj.GetType().GetRuntimeFields().ToList().Find(a => a.FieldType == item.FieldType && a.Name == item.Name);
                                newItem.SetValue(obj, serviceProvider.GetService(item.FieldType));
                            }
                        });
                    }

                    if (obj == null)
                        return;

                    list.ForEach(m =>
                    {
                        var type = b.MakeGenericType(new Type[1] { m });
                        if (b.GetInterfaces().Any())
                            serviceCollection.AddScoped(type.GetInterfaces()[0], s => { return obj; });
                        else
                            serviceCollection.AddScoped(type, s => { return obj; });
                    });
                });
            });

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
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

                assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceModel)).ToList().ForEach(b =>
                {
                    if (b.IsPublic && b.IsClass && !b.IsAbstract && !b.IsGenericType)
                        list.Add(b);
                });
            });

            return list;
        }
    }
}