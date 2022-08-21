using System;
using System.Web.Mvc;
using System.Web.Routing;
using System.Reflection;
using FastAop.Constructor;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace FastAop.Factory
{
    public class AopMvcFactory : DefaultControllerFactory
    {
        private ConcurrentDictionary<Type, object> cache = new ConcurrentDictionary<Type, object>();
        protected override IController GetControllerInstance(RequestContext requestContext, Type controllerType)
        {
            if (requestContext == null)
                throw new ArgumentNullException(nameof(requestContext));

            if (controllerType == null)
                throw new ArgumentException(nameof(controllerType));

            object instance = null;
            cache.TryGetValue(controllerType, out instance);

            if (instance == null)
            {
                var info = Constructor.Constructor.Get(controllerType, null);
                instance = Activator.CreateInstance(controllerType, info.dynParam.ToArray());

                foreach (var item in ((TypeInfo)controllerType).DeclaredFields)
                {
                    if (item.GetCustomAttribute<Autowired>() == null)
                        continue;

                    if (item.FieldType.isSysType())
                        throw new Exception($"{item.Name} is system type not support");

                    if (FastAop._types.GetValue(item.FieldType) == null)
                        throw new Exception($"{item.FieldType.FullName} not in ServiceCollection");

                    item.SetValue(instance, FastAop._types.GetValue(item.FieldType));
                }

                cache.TryAdd(controllerType, instance);
            }

            return (IController)instance;
        }
    }
}