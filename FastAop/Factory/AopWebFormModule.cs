using FastAop.Constructor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
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

            var type = page.GetType().BaseType;
            cache.TryGetValue(type, out list);

            if (list == null)
            {
                list = type.GetRuntimeFields().ToList();
                cache.TryAdd(type, list);
            }

            list.ForEach(item =>
            {
                if (item.GetCustomAttribute<Autowired>() == null)
                    return;

                if (item.FieldType.isSysType())
                    throw new Exception($"{page.GetType().Name} field {item.Name} is system type not support");

                if (item.FieldType.IsInterface && FastAop._types.GetValue(item.FieldType) == null)
                    throw new Exception($"{item.FieldType.Name} not in ServiceCollection");

                if (!item.FieldType.IsInterface && item.FieldType.GetInterfaces().Any() && FastAop._types.GetValue(item.FieldType.GetInterfaces().First()) == null)
                    throw new Exception($"{item.FieldType.GetInterfaces().First().FullName} not in ServiceCollection");

                if (!item.FieldType.IsInterface && Dic.GetValueDyn(item.FieldType) == null)
                    throw new Exception($"{item.FieldType.FullName} not in ServiceCollection");

                if (item.FieldType.IsInterface)
                    item.SetValue(page, FastAop._types.GetValue(item.FieldType));
                else if (item.FieldType.GetInterfaces().Any()) 
                    item.SetValue(page, FastAop._types.GetValue(item.FieldType.GetInterfaces().First()));
                else
                    item.SetValue(page, Dic.GetValueDyn(item.FieldType));
            });
        }
    }
}