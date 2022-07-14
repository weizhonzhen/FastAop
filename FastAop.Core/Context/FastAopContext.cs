using Microsoft.Extensions.DependencyInjection;

namespace FastAop.Core.Context
{
    public class FastAopContext
    {
        public static dynamic ResolveDyn<T>()
        {
            return FastAopExtension.serviceProvider.GetService(typeof(T));
        }

        public static T Resolve<T>()
        {
            if (typeof(T).IsInterface)
                return FastAopExtension.serviceProvider.GetService<T>();
            else
                return default;
        }
    }
}
