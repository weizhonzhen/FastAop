using FastAop.Core.Constructor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace FastAop.Core.Factory
{
    public class AopPageFactory : IPageModelActivatorProvider
    {
        private ConcurrentDictionary<Type, object> cache = new ConcurrentDictionary<Type, object>();
        public Func<PageContext, object> CreateActivator(CompiledPageActionDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            var type = descriptor.ModelTypeInfo.AsType();
            object instance = null;

            cache.TryGetValue(type, out instance);

            if (instance == null)
            {
                object value;
                var model = Constructor.Constructor.Get(type, null);
                instance = Activator.CreateInstance(type, model.dynParam.ToArray());

                foreach (var item in type.GetRuntimeFields())
                {
                    if (item.GetCustomAttribute<Autowired>() == null)
                        continue;

                    if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                        throw new AopException($"{type.Name} field {item} attribute must readonly");

                    if (item.FieldType.isSysType())
                        throw new Exception($"{type.Name} field {item} is system type not support");

                    value = FastAopExtension.serviceProvider.GetService(item.FieldType);

                    if (value == null)
                        throw new Exception($"{type.Name} field {item} not in ServiceCollection");

                    if (!item.FieldType.IsInterface && !item.FieldType.GetInterfaces().Any())
                        item.SetValue(instance, value);
                    else
                        item.SetValueDirect(__makeref(instance), value);
                }

                cache.TryAdd(type, instance);
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
