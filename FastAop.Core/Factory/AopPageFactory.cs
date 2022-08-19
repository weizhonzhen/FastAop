﻿using FastAop.Core.Constructor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FastAop.Core.Factory
{
    public class AopPageFactory : IPageModelActivatorProvider
    {
        private Dictionary<Type, object> cache = new Dictionary<Type, object>();
        public Func<PageContext, object> CreateActivator(CompiledPageActionDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            var type = descriptor.ModelTypeInfo.AsType();
            object instance = null;

            cache.TryGetValue(type, out instance);

            if (instance == null)
            {
                var model = Constructor.Constructor.Get(type, null);
                instance = Activator.CreateInstance(type, model.dynParam.ToArray());

                foreach (var item in type.GetRuntimeFields())
                {
                    if (item.GetCustomAttribute<Autowired>() == null)
                        continue;

                    if (item.FieldType.isSysType())
                        throw new Exception($"{item.Name} is system type not support");

                    if (FastAopExtension.serviceProvider.GetService(item.FieldType) == null)
                        throw new Exception($"{item.FieldType.FullName} not in ServiceCollection");

                    item.SetValue(instance, FastAopExtension.serviceProvider.GetService(item.FieldType));
                }

                cache.Add(type, instance);
            }

            return _ => instance;
        }

        public Action<PageContext, object> CreateReleaser(CompiledPageActionDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            return (context, model) => { };
        }
    }
}
