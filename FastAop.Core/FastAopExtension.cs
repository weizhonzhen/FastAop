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
            if (!string.IsNullOrEmpty(nameSpaceService))
            {
                Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
                {
                    if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                        try { Assembly.Load(a.Name); } catch (Exception ex) { }
                });

                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
                {
                    if (assembly.IsDynamic)
                        return;

                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace == nameSpaceService).ToList().ForEach(b =>
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
                            serviceCollection.AddScoped(b, s => { return FastAop.Core.FastAopDyn.Instance(b); });
                        else if (!b.IsInterface && !b.GetInterfaces().Any() && b.GetConstructors().Length == 0)
                            serviceCollection.AddScoped(b, s => { return Activator.CreateInstance(b); });
                        else if (!b.IsInterface && !b.GetInterfaces().Any() && b.GetConstructors().Length > 0)
                        {
                            var model = Constructor.Get(b, null);
                            serviceCollection.AddScoped(b, s => { return Activator.CreateInstance(b, model.dynParam.ToArray()); });
                        }
                    });
                });
            }

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string nameSpaceService, Type aopType)
        {
            if (aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (!string.IsNullOrEmpty(nameSpaceService))
            {
                Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
                {
                    if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                        try { Assembly.Load(a.Name); } catch (Exception) { }
                });

                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
                {
                    if (assembly.IsDynamic)
                        return;

                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace == nameSpaceService).ToList().ForEach(b =>
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
                            serviceCollection.AddScoped(b, s => { return FastAop.Core.FastAopDyn.Instance(b, aopType); });
                    });
                });
            }

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        public static IServiceCollection AddFastAopGeneric(this IServiceCollection serviceCollection, string nameSpaceService,string nameSpaceModel)
        {
            if (!string.IsNullOrEmpty(nameSpaceService))
            {
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

                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace == nameSpaceService).ToList().ForEach(b =>
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
            }

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        public static IServiceCollection AddFastAopGeneric(this IServiceCollection serviceCollection, string nameSpaceService, string nameSpaceModel, Type aopType)
        {
            if (!string.IsNullOrEmpty(nameSpaceService))
            {
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

                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace == nameSpaceService).ToList().ForEach(b =>
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
                                Constructor.Param(serviceCollection, p.ParameterType,aopType);
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
            }

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        private static List<Type> InitModelType(string nameSpaceModel)
        {
            var list = new List<Type>();
            if (!string.IsNullOrEmpty(nameSpaceModel))
            {
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
            }
            return list;
        }
    }
}