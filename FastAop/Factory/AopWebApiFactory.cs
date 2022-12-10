using FastAop.Constructor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using System.Web.Http.Metadata;
using System.Web.Http.Validation;
using System.Web.Http.Validation.Providers;

namespace FastAop.Factory
{
    public class AopWebApiFactory : IHttpControllerActivator
    {
        public IHttpController Create(HttpRequestMessage request, HttpControllerDescriptor controllerDescriptor, Type controllerType)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (controllerDescriptor == null)
                throw new ArgumentNullException(nameof(controllerDescriptor));

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

            if (!controllerDescriptor.Configuration.Services.GetModelValidatorProviders().ToList().Exists(a => a.GetType().Name == typeof(FastAopWebApiModelValidatorProvider).Name))            
                controllerDescriptor.Configuration.Services.Replace(typeof(ModelValidatorProvider), new FastAopWebApiModelValidatorProvider());
            
            return (IHttpController)instance;
        }
    }

    public class FastAopWebApiModelValidatorProvider : DataAnnotationsModelValidatorProvider
    {
        protected override IEnumerable<ModelValidator> GetValidators(ModelMetadata metadata, IEnumerable<ModelValidatorProvider> validatorProviders, IEnumerable<Attribute> attributes)
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
            return base.GetValidators(metadata, validatorProviders, attributes);
        }
    }
}