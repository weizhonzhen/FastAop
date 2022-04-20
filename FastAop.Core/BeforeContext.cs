using System;
using System.Reflection;
using System.Threading.Tasks;

namespace FastAop.Core
{
    public class BeforeContext
    {
        public object[] Paramter { get; set; }

        public string ServiceType { get; set; }

        public string MethodName { get; set; }

        public MethodInfo Method { get { return string.IsNullOrEmpty(ServiceType) ? null : FastAopCache.GetType(ServiceType).GetMethod(MethodName); } internal set { } }

        public bool IsReturn { get; set; }

        public object Result { get { return FastAop.GetTaskResult(TaskResult); } set { } }

        public object TaskResult { get; set; }


        public bool IsTaskResult { get { return Method.ReturnType.BaseType == typeof(Task) || Method.ReturnType == typeof(Task); } internal set { } }

        public string[] AttributeName { get; set; }

        public Type ResultType { get { return Method.ReturnType; } internal set { } }
    }
}