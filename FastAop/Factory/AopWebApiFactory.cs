using FastAop.Constructor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using System.Web.UI;

namespace FastAop.Factory
{
    public class AopWebApiFactory : IHttpControllerActivator
    {
        private ConcurrentDictionary<Type, object> cache = new ConcurrentDictionary<Type, object>();
        public IHttpController Create(HttpRequestMessage request, HttpControllerDescriptor controllerDescriptor, Type controllerType)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (controllerDescriptor == null)
                throw new ArgumentNullException(nameof(controllerDescriptor));

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

                    if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                        throw new AopException($"{controllerType.Name} field {item} attribute must readonly");

                    if (item.FieldType.isSysType())
                        throw new Exception($"{controllerType.Name} field {item} is system type not support");

                    if (item.FieldType.IsInterface && FastAop._types.GetValue(item.FieldType) == null)
                        throw new Exception($"{controllerType.Name} field {item} not in ServiceCollection");

                    if (!item.FieldType.IsInterface && item.FieldType.GetInterfaces().Any() && FastAop._types.GetValue(item.FieldType.GetInterfaces().First()) == null)
                        throw new Exception($"{controllerType.Name} field {item} not in ServiceCollection");

                    if (!item.FieldType.IsInterface && Dic.GetValueDyn(item.FieldType) == null)
                        throw new Exception($"{controllerType.Name} field {item} not in ServiceCollection");

                    if (item.FieldType.IsInterface)
                        item.SetValueDirect(__makeref(instance), FastAop._types.GetValue(item.FieldType));
                    else if (item.FieldType.GetInterfaces().Any())
                        item.SetValueDirect(__makeref(instance), FastAop._types.GetValue(item.FieldType.GetInterfaces().First()));
                    else
                        item.SetValue(instance, Dic.GetValueDyn(item.FieldType));
                }

                cache.TryAdd(controllerType, instance);
            }

            return (IHttpController)instance;

        }
    }
}
