using System;
using System.Reflection;

namespace FastAop.Core
{
    public class AfterContext
    {
        public object[] Paramter { get; set; }

        public string ServiceType { get; set; }

        public string MethodName { get; set; }

        public MethodInfo Method { get { return string.IsNullOrEmpty(ServiceType) ? null : Type.GetType(ServiceType).GetMethod(MethodName); } }

        public object Result { get; set; }

        public string[] AttributeName { get; set; }

        public object ResultType { get { return Method.ReturnType; } }
    }
}
