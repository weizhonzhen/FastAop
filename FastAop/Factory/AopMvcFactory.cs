using System;
using System.Web.Mvc;
using System.Web.Routing;
using System.Reflection;
using FastAop.Constructor;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;

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

            if (!ModelValidatorProviders.Providers.ToList().Exists(a => a.GetType().Name == typeof(FastAopMvcModelValidatorProvider).Name))
            {
                ModelValidatorProviders.Providers.Clear();
                ModelValidatorProviders.Providers.Add(new FastAopMvcModelValidatorProvider());
                ModelValidatorProviders.Providers.Add(new DataErrorInfoModelValidatorProvider());
                ModelValidatorProviders.Providers.Add(new ClientDataTypeModelValidatorProvider());
            }

            return (IController)instance;
        }
    }

    internal class FastAopMvcModelValidatorProvider : DataAnnotationsModelValidatorProvider
    {
        protected override IEnumerable<ModelValidator> GetValidators(ModelMetadata metadata, ControllerContext context, IEnumerable<Attribute> attributes)
        {
            attributes.ToList().ForEach(a =>
            {
                foreach (var item in a.GetType().GetRuntimeFields())
                {
                    if (item.GetCustomAttribute<Autowired>() == null)
                        continue;

                    if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                        throw new AopException($"{a.GetType().Name} field {item} attribute must readonly");

                    if (item.FieldType.isSysType())
                        throw new Exception($"{a.GetType().Name} field {item} is system type not support");

                    if (item.FieldType.IsInterface)
                        item.SetValueDirect(__makeref(a), FastAop.ServiceInstance.GetValue(item.FieldType));
                    else if (item.FieldType.GetInterfaces().Any())
                        item.SetValueDirect(__makeref(a), FastAop.ServiceInstance.GetValue(item.FieldType.GetInterfaces().First()));
                    else
                        item.SetValue(a, Dic.GetValueDyn(item.FieldType));
                }
            });
            return base.GetValidators(metadata, context, attributes);
        }
    }
}