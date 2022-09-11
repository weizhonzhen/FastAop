using FastAop.Constructor;
using FastAop.Result;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.UI;

namespace FastAop.Factory
{
    public class AopWebFormModule : IHttpModule
    {
        private ConcurrentDictionary<Type, List<FieldInfo>> cache = new ConcurrentDictionary<Type, List<FieldInfo>>();
        private HttpApplication application;

        public void Dispose() { }

        public void Init(HttpApplication _application)
        {
            if (_application == null)
                throw new ArgumentNullException(nameof(_application));

            application = _application;
            application.PreRequestHandlerExecute += PreRequestHandlerExecute;
        }

        private void PreRequestHandlerExecute(object sender, EventArgs e)
        {
            var list = new List<FieldInfo>();
            var page = application.Context.CurrentHandler as Page;

            if (page == null)
                return;

            var type = page.GetType().BaseType;
            cache.TryGetValue(type, out list);

            if (list == null)
            {
                list = type.GetRuntimeFields().ToList();
                cache.TryAdd(type, list);
            }

            foreach(var item in  list)
            {
                if (item.GetCustomAttribute<Autowired>() == null)
                    continue;

                if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                    throw new AopException($"{page.GetType().Name} field {item} attribute must readonly");

                if (item.FieldType.isSysType())
                    throw new Exception($"{page.GetType().Name} field {item} is system type not support");

                if (item.FieldType.IsInterface)
                    item.SetValueDirect(__makeref(page), FastAop.ServiceInstance.GetValue(item.FieldType));
                else if (item.FieldType.GetInterfaces().Any()) 
                    item.SetValueDirect(__makeref(page), FastAop.ServiceInstance.GetValue(item.FieldType.GetInterfaces().First()));
                else
                    item.SetValue(page, Dic.GetValueDyn(item.FieldType));
            }
        }
    }
}