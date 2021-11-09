using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace FastAop.Core
{
    public static class FastAopExtension
    {
        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string NamespaceService)
        {
            if (!string.IsNullOrEmpty(NamespaceService))
            {
                Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
                {
                    if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                        try { Assembly.Load(a.Name); } catch (Exception ex) { }
                });

                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
                {
                    try
                    {
                        assembly.GetTypes().Where(a => a.Namespace != null && a.Namespace == NamespaceService).ToList().ForEach(b =>
                        {
                            var isServiceAttr = false;
                            b.GetMethods().ToList().ForEach(m =>
                            {
                                if (m.GetCustomAttributes().ToList().Exists(a => a.GetType().BaseType == typeof(FastAopAttribute)))
                                    isServiceAttr = true;
                            });

                            if (!b.IsInterface && b.GetInterfaces().Any() && isServiceAttr)
                                serviceCollection.AddSingleton(b.GetInterfaces().First(), FastAop.Instance(b, b.GetInterfaces().First()).GetType());
                            else if (!b.IsInterface && b.GetInterfaces().Any())
                                serviceCollection.AddTransient(b.GetInterfaces().First(),b);

                        });
                    }
                    catch (Exception ex) { }
                });
            }
            
            return serviceCollection;
        }
    }
}