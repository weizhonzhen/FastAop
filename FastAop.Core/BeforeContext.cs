using System;
using System.Reflection;
using System.Threading.Tasks;

namespace FastAop.Core
{
    public class BeforeContext
    {
        private object _Result =new object();
        public object[] Paramter { get; set; }

        public string ServiceType { get; set; }

        public string MethodName { get; set; }

        public MethodInfo Method
        {
            get
            {
                return string.IsNullOrEmpty(ServiceType) ? null : FastAopCache.GetType(ServiceType).GetMethod(MethodName);
            }
            internal set { }
        }

        public bool IsReturn { get; set; }

        public object Result
        {
            set
            {
                if (!IsTaskResult && value is Task)
                    value = FastAop.GetTaskResult(value);

                if (!isValueTaskResult && FastAop.IsValueTask(value.GetType()))
                    value = FastAop.GetTaskResult(value);

                if (!IsTaskResult && !isValueTaskResult)
                    _Result = Convert.ChangeType(value, ResultType);
                else
                    _Result = value;
            }
            get
            {
                if (IsTaskResult && !(_Result is Task) && !isValueTaskResult)
                    throw new Exception($"serviceName class name:{ServiceType},method name:{Method.Name}, return type is Task, but aop retrun type is {_Result.GetType().Name}");
                else if (!IsTaskResult && !FastAop.IsValueTask(_Result.GetType()) && isValueTaskResult)
                    throw new Exception($"serviceName class name:{ServiceType},method name:{Method.Name}, return type is ValueTask, but aop retrun type is {_Result.GetType().Name}");
                else if (IsTaskResult && !isValueTaskResult && ResultType.GenericTypeArguments.Length > 0 && _Result.GetType().GenericTypeArguments.Length > 0 && ResultType.GenericTypeArguments[0] != _Result.GetType().GenericTypeArguments[0])
                    throw new Exception($"serviceName class name:{ServiceType},method name:{Method.Name}, retrun type is Task<{ResultType.GenericTypeArguments[0].Name}>, but aop retrun type is Task<{_Result.GetType().GenericTypeArguments[0].Name}>");
                else if (!IsTaskResult && isValueTaskResult && ResultType.GenericTypeArguments.Length > 0 && _Result.GetType().GenericTypeArguments.Length > 0 && ResultType.GenericTypeArguments[0] != _Result.GetType().GenericTypeArguments[0])
                    throw new Exception($"serviceName class name:{ServiceType},method name:{Method.Name}, retrun type is ValueTask<{ResultType.GenericTypeArguments[0].Name}>, but aop retrun type is ValueTask<{_Result.GetType().GenericTypeArguments[0].Name}>");
                else
                    return _Result;
            }
        }

        public object TaskResult
        {
            get
            {
                return FastAop.GetTaskResult(Result);
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
                return FastAop.IsValueTask(Method.ReturnType);
            }
            internal set { }
        }


        public string[] AttributeName { get; set; }

        public Type ResultType { get { return Method.ReturnType; } internal set { } }
    }
}