using FastAop.Context;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace FastAop.Result
{
    internal static class BaseResult
    {
        private delegate object EmitInvoke(object target, object[] paramters);

        private static object Invoke(object model, MethodInfo methodInfo, object[] param)
        {
            if (methodInfo == null || model == null)
                return null;
            try
            {
                var dynamicMethod = new DynamicMethod("InvokeEmit", typeof(object), new Type[] { typeof(object), typeof(object[]) }, typeof(EmitInvoke).Module);
                var iL = dynamicMethod.GetILGenerator();
                var info = methodInfo.GetParameters();
                var type = new Type[info.Length];
                var local = new LocalBuilder[type.Length];

                for (int i = 0; i < info.Length; i++)
                {
                    if (info[i].ParameterType.IsByRef)
                        type[i] = info[i].ParameterType.GetElementType();
                    else
                        type[i] = info[i].ParameterType;
                }

                for (int i = 0; i < type.Length; i++)
                {
                    local[i] = iL.DeclareLocal(type[i], true);
                }

                for (int i = 0; i < type.Length; i++)
                {
                    iL.Emit(OpCodes.Ldarg_1);
                    iL.Emit(OpCodes.Ldc_I4, i);
                    iL.Emit(OpCodes.Ldelem_Ref);
                    if (type[i].IsValueType)
                        iL.Emit(OpCodes.Unbox_Any, type[i]);
                    else
                        iL.Emit(OpCodes.Castclass, type[i]);
                    iL.Emit(OpCodes.Stloc, local[i]);
                }

                if (!methodInfo.IsStatic)
                    iL.Emit(OpCodes.Ldarg_0);

                for (int i = 0; i < type.Length; i++)
                {
                    if (info[i].ParameterType.IsByRef)
                        iL.Emit(OpCodes.Ldloca_S, local[i]);
                    else
                        iL.Emit(OpCodes.Ldloc, local[i]);
                }

                if (methodInfo.IsStatic)
                    iL.EmitCall(OpCodes.Call, methodInfo, null);
                else
                    iL.EmitCall(OpCodes.Callvirt, methodInfo, null);

                if (methodInfo.ReturnType == typeof(void))
                    iL.Emit(OpCodes.Ldnull);
                else if (methodInfo.ReturnType.IsValueType)
                    iL.Emit(OpCodes.Box, methodInfo.ReturnType);
                else
                    iL.Emit(OpCodes.Castclass, methodInfo.ReturnType);


                for (int i = 0; i < type.Length; i++)
                {
                    if (info[i].ParameterType.IsByRef)
                    {
                        iL.Emit(OpCodes.Ldarg_1);
                        iL.Emit(OpCodes.Ldc_I4, i);
                        iL.Emit(OpCodes.Ldloc, local[i]);
                        if (local[i].LocalType.IsValueType)
                            iL.Emit(OpCodes.Box, local[i].LocalType);
                        iL.Emit(OpCodes.Stelem_Ref);
                    }
                }

                iL.Emit(OpCodes.Ret);
                var dyn = dynamicMethod.CreateDelegate(typeof(EmitInvoke)) as EmitInvoke;
                return dyn(model, param);
            }
            catch (Exception ex)
            {
                return methodInfo.Invoke(model, param);
            }
        }

        internal static object GetTaskResult(object result)
        {
            if (result == null)
                return result;

            if (result is Task)
            {
                var method = result.GetType().GetMethods().ToList().Find(a => a.Name == "get_Result");
                return Invoke(result, method, null);
            }
            else
                return result;
        }

        internal static object SetResult(ExceptionContext context, object value)
        {
            return SetResult(context.IsTaskResult, value, context.Method, context.ResultType, context.IsReturn, context.Method.Name);
        }

        internal static object GetResult(ExceptionContext context, object _Result)
        {
            return GetResult(context.IsTaskResult, _Result, context.ServiceType, context.Method, context.ResultType);
        }

        internal static object SetResult(AfterContext context, object value)
        {
            if (!context.IsTaskResult && value is Task)
                value = BaseResult.GetTaskResult(value);

            if (value != null && !context.Method.IsGenericMethod && value.GetType() != context.ResultType)
                throw new Exception($"ServiceName:{(context.Method.DeclaringType != null ? context.Method.DeclaringType.Name : context.Method.Name)},Method Name:{context.Method.Name},return Type:{context.ResultType.Name},but aop set result type :{value.GetType().Name}");

            if (!context.IsTaskResult && !context.Method.IsGenericMethod)
                return Convert.ChangeType(value, context.ResultType);
            else
                return value;
        }

        internal static object SetResult(BeforeContext context, object value)
        {
            return SetResult(context.IsTaskResult, value, context.Method, context.ResultType, context.IsReturn, context.Method.Name);
        }

        internal static object GetResult(BeforeContext context, object _Result)
        {
            return GetResult(context.IsTaskResult, _Result, context.ServiceType, context.Method, context.ResultType);
        }

        private static object SetResult(bool IsTaskResult, object value, MethodInfo Method, Type ResultType, bool IsReturn, string MethodName)
        {
            if (!IsTaskResult && value is Task)
                value = GetTaskResult(value);

            if (value != null && !Method.IsGenericMethod && value.GetType() != ResultType && IsReturn)
                throw new Exception($"ServiceName:{(Method.DeclaringType != null ? Method.DeclaringType.Name : MethodName)},Method Name:{MethodName},return Type:{ResultType.Name},but aop set result type :{value.GetType().Name}");

            if (!IsTaskResult && !Method.IsGenericMethod)
                return Convert.ChangeType(value, ResultType);
            else
                return value;
        }

        private static object GetResult(bool IsTaskResult, object _Result, string ServiceType, MethodInfo Method, Type ResultType)
        {
            if (IsTaskResult && !(_Result is Task))
                throw new Exception($"serviceName class name:{ServiceType},method name:{Method.Name}, return type is Task, but aop retrun type is {_Result.GetType().Name}");
            else if (IsTaskResult && ResultType.GenericTypeArguments.Length > 0 && _Result.GetType().GenericTypeArguments.Length > 0 && ResultType.GenericTypeArguments[0] != _Result.GetType().GenericTypeArguments[0])
                throw new Exception($"serviceName class name:{ServiceType},method name:{Method.Name}, retrun type is Task<{ResultType.GenericTypeArguments[0].Name}>, but aop retrun type is Task<{_Result.GetType().GenericTypeArguments[0].Name}>");
            else
                return _Result;
        }
    }
}
