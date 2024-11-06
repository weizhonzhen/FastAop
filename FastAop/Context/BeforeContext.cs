using FastAop.Result;
using System.Reflection;
using System.Threading.Tasks;

namespace FastAop.Context
{
    public class BeforeContext
    {
        internal object _Result;

        public string Id { get; set; }

        public object[] Paramter { get; set; }

        public string ServiceType { get; set; }

        public MethodInfo Method { get; set; }

        public bool IsReturn { get; set; }

        public object Result
        {
            get
            {
                return _Result;
            }
            set
            {
                if (Method.ReturnType == typeof(void))
                    return;

                _Result = BaseResult.SetResult(this, value);
            }
        }

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