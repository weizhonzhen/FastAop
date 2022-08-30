using FastAop.Core.Model;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace FastAop.Core.Constructor
{
    internal static class Constructor
    {
        internal static int depth = 0;

        internal static void Param(IServiceCollection serviceCollection, Type paramType, Type aopType)
        {
            if (paramType.isSysType())
                return;

            if (!paramType.IsInterface && paramType.GetInterfaces().Any())
            {
                serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == paramType.GetInterfaces().First()));
                serviceCollection.AddScoped(paramType.GetInterfaces().First(), FastAop.Instance(paramType, paramType.GetInterfaces().First(), aopType).GetType());
                return;
            }
            else if (!paramType.IsInterface && !paramType.GetInterfaces().Any())
            {
                serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == paramType));
                serviceCollection.AddScoped(paramType, s => { return FastAopDyn.Instance(paramType, aopType); });
                return;
            }

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

                            Param(serviceCollection, p.ParameterType, aopType);
                        });
                    });
                }

                serviceCollection.Remove(serviceCollection.FirstOrDefault(a => a.ServiceType == paramType));
                serviceCollection.AddScoped(paramType, FastAop.Instance(param, paramType).GetType());
                FastAopExtension.serviceProvider = serviceCollection.BuildServiceProvider();
                return;
            }

            return;
        }

        internal static ConstructorModel Get(Type serviceType, Type interfaceType)
        {
            var model = new ConstructorModel();
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
                        model.dynParam.Add(Activator.CreateInstance(p.ParameterType));
                    else if (FastAopExtension.serviceProvider.GetService(p.ParameterType) == null && p.ParameterType.IsInterface && p.ParameterType.IsGenericType)
                        throw new Exception($"AddFastAopGeneric Method，{serviceType.FullName} Constructor have Parameter Generic Type");
                    else if (FastAopExtension.serviceProvider.GetService(p.ParameterType) == null && p.ParameterType.IsInterface)
                        throw new Exception($"can't find {p.ParameterType.Name} Instance class");
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