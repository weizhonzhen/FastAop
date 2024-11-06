﻿using FastAop.Core.Result;
using System.Reflection;
using System.Threading.Tasks;

namespace FastAop.Core.Context
{
    public class AfterContext
    {
        internal object _Result;

        public object[] Paramter { get; set; }

        public string ServiceType { get; set; }

        public MethodInfo Method { get; set; }

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

        public bool isValueTaskResult
        {
            get
            {
                return BaseResult.IsValueTask(Method.ReturnType);
            }
            internal set { }
        }

        public string[] AttributeName { get; set; }
    }
}