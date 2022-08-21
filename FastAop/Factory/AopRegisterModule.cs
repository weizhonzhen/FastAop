[assembly: WebActivatorEx.PreApplicationStartMethod(typeof(FastAop.Factory.AopRegisterModule), "Start")]

namespace FastAop.Factory
{
    using Microsoft.Web.Infrastructure.DynamicModuleHelper;
    public static class AopRegisterModule
    {
        public static void Start()
        {
            DynamicModuleUtility.RegisterModule(typeof(AopWebFormModule));
        }
    }
}
