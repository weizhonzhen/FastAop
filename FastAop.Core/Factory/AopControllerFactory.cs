using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using System;
using FastAop.Core.Constructor;
using System.Reflection;
using System.Collections.Concurrent;
using System.Linq;

namespace FastAop.Core.Factory
{
    public class AopControllerFactory : IControllerActivator
    {
        private ConcurrentDictionary<Type, object> cache = new ConcurrentDictionary<Type, object>();
        public object Create(ControllerContext controllerContext)
        {
            if (controllerContext == null)
                throw new ArgumentNullException(nameof(controllerContext));

            if (controllerContext.ActionDescriptor == null)
                throw new ArgumentException(nameof(ControllerContext.ActionDescriptor));

            var controllerTypeInfo = controllerContext.ActionDescriptor.ControllerTypeInfo;
            if (controllerTypeInfo == null)
                throw new ArgumentException(nameof(controllerContext.ActionDescriptor.ControllerTypeInfo));

            var type = controllerTypeInfo.AsType();
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
                        throw new AopException($"{type.Name} field {item} attribute must Attribute readonly");

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

            return instance;
        }

        public void Release(ControllerContext context, object controller)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (controller == null)
                throw new ArgumentNullException(nameof(controller));

            (controller as IDisposable)?.Dispose();
        }
    }
}
