using System;

namespace FastAop.Core
{
    public class ExceptionContext
    {
        public object[] Paramter { get; set; }

        public string MethodName { get; set; }

        public Exception Exception { get; set; }
    }
}
