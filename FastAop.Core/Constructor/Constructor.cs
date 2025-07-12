using FastAop.Core.Model;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace FastAop.Core.Constructor
{
    internal static class Constructor
    {
        private static ConcurrentDictionary<string, ConstructorModel> cache = new ConcurrentDictionary<string, ConstructorModel>();
        internal static int depth = 0;

        internal static void Param(IServiceCollection serviceCollection, Type paramType, Type aopType, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            if (paramType.isSysType())
                return;

            object obj = null;
            var serverType = paramType;
            if (!paramType.IsInterface && paramType.GetInterfaces().Any())
            {
                serverType = paramType.GetInterfaces().First();
                obj = FastAop.Instance(paramType, paramType.GetInterfaces().First(), aopType);
            }
            else if (!paramType.IsInterface && !paramType.GetInterfaces().Any())
            {
                var model = Constructor.Get(paramType, null);
                obj = Activator.CreateInstance(paramType, model.dynParam.ToArray());
            }

            serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == serverType));
            if (serviceLifetime == ServiceLifetime.Scoped && obj != null)
                serviceCollection.AddScoped(serverType, s => { return obj; });

            if (serviceLifetime == ServiceLifetime.Transient && obj != null)
                serviceCollection.AddTransient(serverType, s => { return obj; });

            if (serviceLifetime == ServiceLifetime.Singleton && obj != null)
                serviceCollection.AddSingleton(serverType, s => { return obj; });

            if (obj != null)
                return;

            if (paramType.IsInterface && serviceCollection.BuildServiceProvider().GetService(paramType) == null)
            {
                var param = paramType.Assembly.GetTypes().ToList().Find(t => t.GetInterfaces().Any() && !t.IsAbstract && !t.IsSealed && t.GetInterfaces().ToList().Exists(e => e == paramType));
                if (param == null)
                    return;

                if (param.GetConstructors().Length > 0)
                {
                    depth++;
                    param.GetConstructors().ToList().ForEach(c =>
                    {
                        c.GetParameters().ToList().ForEach(p =>
                        {
                            if (depth > 10)
                                throw new Exception($"Type Name:{param.FullName},Parameters Type:{string.Join(",", c.GetParameters().Select(g => g.Name))} repeat using");

                            Param(serviceCollection, p.ParameterType, aopType, serviceLifetime);
                        });
                    });
                }

                serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == serverType));
                obj = FastAop.Instance(param, serverType);

                if (serviceLifetime == ServiceLifetime.Scoped)
                    serviceCollection.AddScoped(serverType, s => { return obj; });

                if (serviceLifetime == ServiceLifetime.Transient)
                    serviceCollection.AddTransient(serverType, s => { return obj; });

                if (serviceLifetime == ServiceLifetime.Singleton)
                    serviceCollection.AddSingleton(serverType, s => { return obj; });

                FastAopExtension.serviceProvider = serviceCollection.BuildServiceProvider();
                return;
            }

            return;
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
                            model.dynParam.Add(null);
                        else if (p.ParameterType.isSysType() && p.ParameterType.IsValueType)
                            model.dynParam.Add(Activator.CreateInstance(p.ParameterType));
                        else if (!p.ParameterType.IsAbstract && !p.ParameterType.IsInterface && FastAopExtension.serviceProvider.GetService(p.ParameterType) != null)
                            model.dynParam.Add(FastAopExtension.serviceProvider.GetService(p.ParameterType));
                        else if (!p.ParameterType.IsAbstract && !p.ParameterType.IsInterface)
                        {
                            var temp = Constructor.Get(p.ParameterType, null);
                            model.dynParam.Add(Activator.CreateInstance(p.ParameterType, temp.dynParam.ToArray()));
                        }
                        else if (FastAopExtension.serviceProvider.GetService(p.ParameterType) == null && p.ParameterType.IsInterface && p.ParameterType.IsGenericType)
                            throw new Exception($"AddFastAopGeneric Method，{serviceType.FullName} Constructor have Parameter Generic Type");
                        else if (FastAopExtension.serviceProvider.GetService(p.ParameterType) == null && p.ParameterType.IsInterface)
                            throw new Exception($"can't find {p.ParameterType.Name} Instance class or {p.ParameterType.Name} add ioc in AddFastAop before");
                        else if (p.ParameterType.IsInterface)
                            model.dynParam.Add(FastAopExtension.serviceProvider.GetService(p.ParameterType));
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

        internal static bool isSysType(this Type type)
        {
            if (type.IsPrimitive || type.Equals(typeof(string)) || type.Equals(typeof(decimal)) || type.Equals(typeof(DateTime)))
                return true;
            else
                return false;
        }
    }
}