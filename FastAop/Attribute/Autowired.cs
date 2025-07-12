using System;

namespace FastAop
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class Autowired : Attribute
    {
    }
}
