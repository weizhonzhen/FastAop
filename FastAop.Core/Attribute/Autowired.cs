using System;

namespace FastAop.Core
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class Autowired : Attribute
    {
    }
}
