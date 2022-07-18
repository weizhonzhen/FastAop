using FastAop.Core.Cache;
using FastAop.Core.Result;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace FastAop.Core.Context
{
    public class ExceptionContext
    {
        private object _Result;
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

        public Exception Exception { get; set; }

        public bool IsReturn { get; set; }

        public object Result
        {
            set
            {
                if (ResultType == typeof(void))
                    return;

                if (!IsTaskResult && value is Task)
                    value = BaseResult.GetTaskResult(value);

                if (!isValueTaskResult && BaseResult.IsValueTask(value.GetType()))
                    value = BaseResult.GetTaskResult(value);

                if (value.GetType() != ResultType && IsReturn)
                    throw new Exception($"ServiceName:{(Method.DeclaringType != null ? Method.DeclaringType.Name : MethodName)},Method Name:{MethodName},return Type:{ResultType.Name},but aop set result type :{value.GetType().Name}");

                if (!IsTaskResult && !isValueTaskResult)
                    _Result = Convert.ChangeType(value, ResultType);
                else
                    _Result = value;
            }
            get
            {
                if (IsTaskResult && !(_Result is Task) && !isValueTaskResult)
                    throw new Exception($"serviceName class name:{ServiceType},method name:{Method.Name}, return type is Task, but aop retrun type is {_Result.GetType().Name}");
                else if (!IsTaskResult && !BaseResult.IsValueTask(_Result.GetType()) && isValueTaskResult)
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