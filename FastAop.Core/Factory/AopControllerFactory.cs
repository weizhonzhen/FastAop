using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using System;
using FastAop.Core.Constructor;
using System.Reflection;
using System.Collections.Concurrent;

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
