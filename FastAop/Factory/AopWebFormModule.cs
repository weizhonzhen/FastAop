using FastAop.Constructor;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using System.Web.UI;

namespace FastAop.Factory
{
    public class AopWebFormModule : IHttpModule
    {
        private HttpApplication application;

        public void Dispose()
        {  }

        public void Init(HttpApplication _application)
        {
            if (_application == null)
                throw new ArgumentNullException(nameof(_application));

            application = _application;
            application.PreRequestHandlerExecute += PreRequestHandlerExecute;
        }

        private void PreRequestHandlerExecute(object sender, EventArgs e)
        {
            var page = application.Context.CurrentHandler as Page;

            foreach (var item in page.GetType().BaseType.GetRuntimeFields())
            {
                if (item.GetCustomAttribute<Autowired>() == null)
                    continue;

                if (item.FieldType.isSysType())
                    throw new Exception($"{item.Name} is system type not support");

                if (FastAop._types.GetValue(item.FieldType) == null)
                    throw new Exception($"{item.FieldType.FullName} not in ServiceCollection");

                item.SetValue(page, FastAop._types.GetValue(item.FieldType));
            }
        }
    }
}
