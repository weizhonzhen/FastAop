using System;
using System.Reflection;

namespace FastAop.Core
{
    public class BeforeContext
    {
        public object[] Paramter { get; set; }

        public string ServiceType { get; set; }

        public string MethodName { get; set; }

        public MethodInfo Method { get { return string.IsNullOrEmpty(ServiceType) ? null : Type.GetType(ServiceType).GetMethod(MethodName); } }

        public bool IsReturn { get; set; }

        public object Result { get; set; }

        public string[] AttributeName { get; set; }

        public Type ResultType { get { return Method.ReturnType; } }
    }
}
