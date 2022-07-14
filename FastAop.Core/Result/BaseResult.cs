using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace FastAop.Core.Result
{
    internal class BaseResult
    {
        private delegate object EmitInvoke(object target, object[] paramters);

        internal static object Invoke(object model, MethodInfo methodInfo, object[] param)
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
            else if (IsValueTask(result.GetType()))
            {
                var method = result.GetType().GetMethods().ToList().Find(a => a.Name == "get_Result");
                return method.Invoke(result, null);
            }
            else
                return result;
        }

        internal static bool IsValueTask(Type type)
        {
            if (type.GetGenericArguments().Length > 0)
                return typeof(ValueTask<>).MakeGenericType(type.GetGenericArguments()[0]) == type;
            return false;
        }
    }
}
