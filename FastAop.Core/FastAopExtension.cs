using FastAop.Core;
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

        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string nameSpaceService)
        {
            if (string.IsNullOrEmpty(nameSpaceService))
                return serviceCollection;

            InitAssembly();

            serviceProvider = serviceCollection.BuildServiceProvider();

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
                            Constructor.Param(serviceCollection, p.ParameterType);
                        });
                    });

                    if (!b.IsInterface && b.GetInterfaces().Any() && isServiceAttr)
                    {
                        serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == b.GetInterfaces().First()));
                        serviceCollection.AddScoped(b.GetInterfaces().First(), FastAop.Core.FastAop.Instance(b, b.GetInterfaces().First()).GetType());
                    }
                    else if (!b.IsInterface && b.GetInterfaces().Any())
                    {
                        serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == b.GetInterfaces().First()));
                        serviceCollection.AddScoped(b.GetInterfaces().First(), FastAop.Core.FastAop.Instance(b, b.GetInterfaces().First()).GetType());
                    }
                    else if (!b.IsInterface && !b.GetInterfaces().Any() && isServiceAttr)
                    {
                        serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == b));
                        serviceCollection.AddScoped(b, s => { return FastAopDyn.Instance(b); });
                    }
                    else if (!b.IsInterface && !b.GetInterfaces().Any() && b.GetConstructors().Length == 0)
                    {
                        serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == b));
                        serviceCollection.AddScoped(b, s => { return Activator.CreateInstance(b); });
                    }
                    else if (!b.IsInterface && !b.GetInterfaces().Any() && b.GetConstructors().Length > 0)
                    {
                        var model = Constructor.Get(b, null);
                        serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == b));
                        serviceCollection.AddScoped(b, s => { return Activator.CreateInstance(b, model.dynParam.ToArray()); });
                    }
                });
            });

            serviceProvider = serviceCollection.BuildServiceProvider();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IControllerActivator)));
            serviceCollection.AddSingleton<IControllerActivator, AopControllerFactory>();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IPageModelActivatorProvider)));
            serviceCollection.AddSingleton<IPageModelActivatorProvider, AopPageFactory>();

            serviceCollection.AddFastAopAutowired(nameSpaceService);

            return serviceCollection;
        }

        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string nameSpaceService, Type aopType)
        {
            if (string.IsNullOrEmpty(nameSpaceService))
                return serviceCollection;

            if (aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            InitAssembly();

            serviceProvider = serviceCollection.BuildServiceProvider();

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
                        });
                    });

                    if (!b.IsInterface && b.GetInterfaces().Any())
                    {
                        serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == b.GetInterfaces().First()));
                        serviceCollection.AddScoped(b.GetInterfaces().First(), FastAop.Core.FastAop.Instance(b, b.GetInterfaces().First(), aopType).GetType());
                    }
                    else if (!b.IsInterface && !b.GetInterfaces().Any())
                    {
                        serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == b));
                        serviceCollection.AddScoped(b, s => { return FastAopDyn.Instance(b, aopType); });
                    }
                });
            });

            serviceProvider = serviceCollection.BuildServiceProvider();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IControllerActivator)));
            serviceCollection.AddSingleton<IControllerActivator, AopControllerFactory>();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IPageModelActivatorProvider)));
            serviceCollection.AddSingleton<IPageModelActivatorProvider, AopPageFactory>();

            serviceCollection.AddFastAopAutowired(nameSpaceService);

            return serviceCollection;
        }

        public static IServiceCollection AddFastAopGeneric(this IServiceCollection serviceCollection, string nameSpaceService, string nameSpaceModel)
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
                            Constructor.Param(serviceCollection, p.ParameterType);
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
                                    Constructor.Param(serviceCollection, p.ParameterType);
                                });
                            });

                            if (!b.IsInterface && b.GetInterfaces().Any())
                            {
                                var serviceType = b.MakeGenericType(new Type[1] { m });
                                serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == serviceType.GetInterfaces().First()));
                                serviceCollection.AddScoped(serviceType.GetInterfaces().First(), FastAop.Core.FastAop.Instance(serviceType, serviceType.GetInterfaces().First()).GetType());
                            }
                        });
                    }
                });
            });

            serviceProvider = serviceCollection.BuildServiceProvider();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IControllerActivator)));
            serviceCollection.AddSingleton<IControllerActivator, AopControllerFactory>();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IPageModelActivatorProvider)));
            serviceCollection.AddSingleton<IPageModelActivatorProvider, AopPageFactory>();

            serviceCollection.AddFastAopAutowiredGeneric(nameSpaceService,nameSpaceModel);

            return serviceCollection;
        }

        public static IServiceCollection AddFastAopGeneric(this IServiceCollection serviceCollection, string nameSpaceService, string nameSpaceModel, Type aopType)
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
                                serviceCollection.Remove(serviceCollection.FirstOrDefault(a=>a.ServiceType== serviceType.GetInterfaces().First()));
                                serviceCollection.AddScoped(serviceType.GetInterfaces().First(), FastAop.Core.FastAop.Instance(serviceType, serviceType.GetInterfaces().First(), aopType).GetType());
                            }
                        });
                    }
                });
            });

            serviceProvider = serviceCollection.BuildServiceProvider();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IControllerActivator)));
            serviceCollection.AddSingleton<IControllerActivator, AopControllerFactory>();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IPageModelActivatorProvider)));
            serviceCollection.AddSingleton<IPageModelActivatorProvider, AopPageFactory>();

            serviceCollection.AddFastAopAutowiredGeneric(nameSpaceService,nameSpaceModel);

            return serviceCollection;
        }

        public static IServiceCollection AddFastAopAutowired(this IServiceCollection serviceCollection, string nameSpaceService)
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

                assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                {
                    if (b.IsGenericType)
                        return;

                    if (b.IsAbstract && b.IsSealed)
                        return;

                    if (b.IsInterface)
                        return;

                   var obj = Instance(b, isFastAopCall);

                    if (obj == null)
                        return;

                    if (b.GetInterfaces().Any())
                    {
                        serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == b.GetInterfaces().First()));
                        serviceCollection.AddScoped(b.GetInterfaces().First(), s => { return obj; });
                    }
                    else
                    {
                        serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == b));
                        serviceCollection.AddScoped(b, s => { return obj; });
                    }
                });
            });

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IControllerActivator)));
            serviceCollection.AddSingleton<IControllerActivator, AopControllerFactory>();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IPageModelActivatorProvider)));
            serviceCollection.AddSingleton<IPageModelActivatorProvider, AopPageFactory>();

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        private static object Instance(Type b, bool isFastAopCall)
        {
            object obj = null, temp = null;
            foreach (var item in b.GetRuntimeFields())
            {
                if (item.GetCustomAttribute<Autowired>() == null)
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
                    obj = serviceProvider.GetService(b.GetInterfaces().First());
                else
                    continue;

                if (obj == null)
                    continue;

                if (obj.GetType().FullName == "Aop_FastAop.ILGrator.Core")
                {
                    if (temp == null)
                    {
                        var type = obj.GetType().GetRuntimeFields().First().FieldType;
                        var model = Constructor.Get(type, null);
                        temp = Activator.CreateInstance(type, model.dynParam.ToArray());

                        foreach (var param in temp.GetType().GetRuntimeFields())
                        {
                            if (item.GetCustomAttribute<Autowired>() == null)
                                continue;

                            if (item.FieldType.isSysType())
                                throw new Exception($"{item.Name} is system type not support");

                            if (param.FieldType.IsInterface)
                                Instance(serviceProvider.GetService(param.FieldType).GetType(), isFastAopCall);
                            else if (param.FieldType.GetInterfaces().Any())
                                Instance(serviceProvider.GetService(param.FieldType.GetInterfaces().First()).GetType(), isFastAopCall);
                        }
                    }

                    if (item.FieldType.IsInterface)
                        item.SetValue(temp, serviceProvider.GetService(item.FieldType));
                    else
                        item.SetValue(temp, serviceProvider.GetService(item.FieldType));

                    obj.GetType().GetRuntimeFields().First().SetValue(obj, temp);
                }
            }

            return obj;
        }

        public static IServiceCollection AddFastAopAutowiredGeneric(this IServiceCollection serviceCollection, string nameSpaceService, string nameSpaceModel)
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

                assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                {                   
                    if (!b.IsGenericType)
                        return;

                    if (b.IsAbstract && b.IsSealed)
                        return;

                    var obj = InstanceGeneric(serviceCollection, list, b, isFastAopCall);

                    if (obj == null)
                        return;

                    list.ForEach(m =>
                    {
                        var type = b.MakeGenericType(new Type[1] { m });
                        if (b.GetInterfaces().Any())
                        {
                            serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == type.GetInterfaces().First()));
                            serviceCollection.AddScoped(type.GetInterfaces().First(), s => { return obj; });
                        }
                        else
                        {
                            serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == type));
                            serviceCollection.AddScoped(type, s => { return obj; });
                        }
                    });
                });
            });

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IControllerActivator)));
            serviceCollection.AddSingleton<IControllerActivator, AopControllerFactory>();

            serviceCollection.Remove(serviceCollection.FirstOrDefault(c => c.ServiceType == typeof(IPageModelActivatorProvider)));
            serviceCollection.AddSingleton<IPageModelActivatorProvider, AopPageFactory>();

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        private static object InstanceGeneric(IServiceCollection serviceCollection,List<Type> list,Type b,bool isFastAopCall)
        {
            object obj=null, temp=null;
            foreach (var item in b.GetRuntimeFields())
            {
                if (item.GetCustomAttribute<Autowired>() == null)
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

                    if (obj.GetType().FullName == "Aop_FastAop.ILGrator.Core")
                    {
                        if (temp == null)
                        {
                            var type = obj.GetType().GetRuntimeFields().First().FieldType;
                            var model = Constructor.Get(type, null);
                            temp = Activator.CreateInstance(type, model.dynParam.ToArray());

                            foreach (var param in temp.GetType().GetRuntimeFields())
                            {
                                if (item.GetCustomAttribute<Autowired>() == null)
                                    continue;

                                if (item.FieldType.isSysType())
                                    throw new Exception($"{item.Name} is system type not support");

                                if (param.FieldType.IsInterface)
                                    InstanceGeneric(serviceCollection, list, serviceProvider.GetService(param.FieldType).GetType(), isFastAopCall);
                                else if (param.FieldType.GetInterfaces().Any())
                                    InstanceGeneric(serviceCollection, list, serviceProvider.GetService(param.FieldType.GetInterfaces().First()).GetType(), isFastAopCall);
                            }
                        }

                        var newItem = temp.GetType().GetRuntimeFields().ToList().Find(a => a.FieldType == item.FieldType && a.Name == item.Name);
                        newItem.SetValue(temp, serviceProvider.GetService(item.FieldType));
                        obj.GetType().GetRuntimeFields().First().SetValue(obj, temp);
                    }
                    else
                    {
                        var newItem = obj.GetType().GetRuntimeFields().ToList().Find(a => a.FieldType == item.FieldType && a.Name == item.Name);
                        newItem.SetValue(obj, serviceProvider.GetService(item.FieldType));
                    }
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