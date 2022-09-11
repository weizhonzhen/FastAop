using System;
using System.Web.Mvc;
using System.Web.Routing;
using System.Reflection;
using FastAop.Constructor;
using System.Collections.Concurrent;
using System.Linq;

namespace FastAop.Factory
{
    public class AopMvcFactory : DefaultControllerFactory
    {
        protected override IController GetControllerInstance(RequestContext requestContext, Type controllerType)
        {
            if (requestContext == null)
                throw new ArgumentNullException(nameof(requestContext));

            if (controllerType == null)
                throw new ArgumentException(nameof(controllerType));

            var info = Constructor.Constructor.Get(controllerType, null);
            var instance = Activator.CreateInstance(controllerType, info.dynParam.ToArray());

            foreach (var item in ((TypeInfo)controllerType).DeclaredFields)
            {
                if (item.GetCustomAttribute<Autowired>() == null)
                    continue;

                if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                    throw new AopException($"{controllerType.Name} field {item} attribute must readonly");

                if (item.FieldType.isSysType())
                    throw new Exception($"{controllerType.Name} field {item} is system type not support");

                if (item.FieldType.IsInterface)
                    item.SetValueDirect(__makeref(instance), FastAop.ServiceInstance.GetValue(item.FieldType));
                else if (item.FieldType.GetInterfaces().Any())
                    item.SetValueDirect(__makeref(instance), FastAop.ServiceInstance.GetValue(item.FieldType.GetInterfaces().First()));
                else
                    item.SetValue(instance, Dic.GetValueDyn(item.FieldType));
            }

            return (IController)instance;
        }
    }
}