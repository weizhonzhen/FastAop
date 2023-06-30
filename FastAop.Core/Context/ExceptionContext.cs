using FastAop.Core.Cache;
using FastAop.Core.Result;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace FastAop.Core.Context
{
    public class ExceptionContext
    {
        public string Id { get; set; }

        private object _Result;
        public object[] Paramter { get; set; }

        public string ServiceType { get; set; }

        public MethodInfo Method
        {
            get
            {
                return FastAopCache.Get(Id);
            }
            internal set { }
        }

        public Exception Exception { get; set; }

        public bool IsReturn { get; set; }

        public object Result
        {
            set
            {
                if (ResultType == typeof(void))
                    return;

                _Result = BaseResult.SetResult(this, value);
            }
            get
            {
                return BaseResult.GetResult(this, _Result);
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

        public bool isValueTaskResult
        {
            get
            {
                return BaseResult.IsValueTask(Method.ReturnType);
            }
            internal set { }
        }

        public string[] AttributeName { get; set; }

        public Type ResultType
        {
            get
            {
                return Method.ReturnType;
            }
            internal set { }
        }
    }
}