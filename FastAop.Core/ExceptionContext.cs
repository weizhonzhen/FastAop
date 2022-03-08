using System;
using System.Reflection;

namespace FastAop.Core
{
    public class ExceptionContext
    {
        public object[] Paramter { get; set; }

        public string ServiceType { get; set; }

        public string MethodName { get; set; }

        public MethodInfo Method { get { return string.IsNullOrEmpty(ServiceType) ? null : FastAopCache.GetType(ServiceType).GetMethod(MethodName); } }

        public Exception Exception { get; set; }

        public bool IsReturn { get; set; }

        public object Result { get; set; }

        public string[] AttributeName { get; set; }

        public Type ResultType { get { return Method.ReturnType; } }
    }
}
