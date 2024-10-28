using FastAop.Result;
using System.Reflection;
using System.Threading.Tasks;

namespace FastAop.Context
{
    public class BeforeContext
    {
        public string Id { get; set; }

        public object[] Paramter { get; set; }

        public string ServiceType { get; set; }

        public MethodInfo Method { get; set; }

        public bool IsReturn { get; set; }

        public object Result { get; set; }

        public object TaskResult
        {
            get
            {
                return BaseResult.GetTaskResult(Result);
            }
            internal set { }
        }

        public bool IsTaskResult
        {
            get
            {
                return Method.ReturnType.BaseType == typeof(Task) || Method.ReturnType == typeof(Task);
            }
            internal set { }
        }

        public string[] AttributeName { get; set; }
}
}