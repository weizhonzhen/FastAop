using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace FastAop.Core
{
    public static class FastAop
    {
        public static object Instance(Type serviceType, Type interfaceType)
        {
            return Proxy(serviceType, interfaceType).CreateDelegate(Expression.GetFuncType(new Type[] { interfaceType })).DynamicInvoke();
        }

        public static I Instance<S, I>() where S : class, new() where I : class
        {
            return FuncAop<S, I>.Instance();
        }

        private class FuncAop<S, I>
        {
            public static Func<I> Instance = CreateInstance();

            private static Func<I> CreateInstance()
            {
                var serviceType = typeof(S);
                var interfaceType = typeof(I);

                return Proxy(serviceType,interfaceType).CreateDelegate(typeof(Func<I>)) as Func<I>;
            }
        }

        private static DynamicMethod Proxy(Type serviceType, Type interfaceType, Type attrType = null)
        {
            if (!interfaceType.IsInterface)
                throw new Exception($"interfaceType only Interface class,class name:{interfaceType.Name}");

            if (serviceType.IsInterface)
                throw new Exception($"serviceType not Interface class,class name:{serviceType.Name}");

            var assemblyName = new AssemblyName("FastAop.ILGrator.Core");
            var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule(assemblyName.Name);
            var builder = module.DefineType($"Aop_{assemblyName}", TypeAttributes.Public, null, new Type[] { interfaceType });

            //Constructor method
            var field = builder.DefineField($"Aop_{serviceType.Name}_Field", serviceType, FieldAttributes.Private);
            var constructor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);

            var cIL = constructor.GetILGenerator();
            cIL.Emit(OpCodes.Ldarg_0);
            cIL.Emit(OpCodes.Callvirt, typeof(object).GetConstructor(Type.EmptyTypes));
            cIL.Emit(OpCodes.Ldarg_0);
            cIL.Emit(OpCodes.Newobj, serviceType.GetConstructor(Type.EmptyTypes));
            cIL.Emit(OpCodes.Stfld, field);
            cIL.Emit(OpCodes.Ret);

            var listMethod = interfaceType.GetMethods(BindingFlags.SuppressChangeType | BindingFlags.Instance | BindingFlags.Public);

            //method list
            for (int m = 0; m < listMethod.Length; m++)
            {
                var currentMthod = listMethod[m];
                var mTypes = currentMthod.GetParameters().Select(d => d.ParameterType).ToArray();
                var method = builder.DefineMethod(currentMthod.Name, MethodAttributes.Public | MethodAttributes.NewSlot | MethodAttributes.Virtual, currentMthod.ReturnType, mTypes);

                var mIL = method.GetILGenerator();

                //Declare Paramter
                var local = mIL.DeclareLocal(typeof(object[]));
                mIL.Emit(OpCodes.Ldc_I4, mTypes.Length);
                mIL.Emit(OpCodes.Newarr, typeof(object));
                mIL.Emit(OpCodes.Stloc, local);

                //method param type
                for (int t = 0; t < mTypes.Length; t++)
                {
                    mIL.Emit(OpCodes.Ldloc, local);
                    mIL.Emit(OpCodes.Ldc_I4, t);
                    mIL.Emit(OpCodes.Ldarg, t + 1);
                    if (mTypes[t].IsValueType)
                        mIL.Emit(OpCodes.Box, mTypes[t]);
                    mIL.Emit(OpCodes.Stelem_Ref);
                }

                //DeclareContext Paramter
                var beforeContext = mIL.DeclareLocal(typeof(BeforeContext));
                mIL.Emit(OpCodes.Newobj, typeof(BeforeContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, beforeContext);

                //BeforeContext Paramter
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldloc, local);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_Paramter"), new[] { typeof(object[]) });

                //Before BeforeContext MethodName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //Declare AfterContext
                var afterContext = mIL.DeclareLocal(typeof(AfterContext));
                mIL.Emit(OpCodes.Newobj, typeof(AfterContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, afterContext);

                //AfterContext Paramter
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldloc, local);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_Paramter"), new[] { typeof(object[]) });

                //AfterContext MethodName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //aop attr
                var aopAttribute = new List<FastAopAttribute>();
                var attrList = new List<LocalBuilder>();
                var aopAttrType = typeof(FastAopAttribute);
                var beforeMethod = aopAttrType.GetMethod("Before");
                var afterMethod = aopAttrType.GetMethod("After");
                var exceptionMethod = aopAttrType.GetMethod("Exception");

                if (attrType == null)
                    aopAttribute = serviceType.GetMethod(currentMthod.Name, mTypes).GetCustomAttributes().Where(d => aopAttrType.IsAssignableFrom(d.GetType())).Cast<FastAopAttribute>().OrderBy(d => d.Sort).ToList();
                else
                {
                    //auto add FastAopAttribute
                    var classCtorInfo = attrType.GetConstructor(Type.EmptyTypes);
                    var attributeBuilder = new CustomAttributeBuilder(classCtorInfo, new object[] { });
                    method.SetCustomAttribute(attributeBuilder);
                    aopAttribute.Add(Activator.CreateInstance(attrType) as FastAopAttribute);
                }

                //before attr
                for (int attr = 0; attr < aopAttribute.Count; attr++)
                {
                    //Declare attr type
                    var attr_Type = (aopAttribute[attr]).GetType();
                    var attrLocal = mIL.DeclareLocal(attr_Type);

                    //BeforeContext
                    mIL.Emit(OpCodes.Newobj, attr_Type.GetConstructor(Type.EmptyTypes));
                    mIL.Emit(OpCodes.Stloc, attrLocal);
                    mIL.Emit(OpCodes.Ldloc, attrLocal);
                    mIL.Emit(OpCodes.Ldloc, beforeContext);
                    mIL.Emit(OpCodes.Callvirt, beforeMethod);
                    attrList.Add(attrLocal);
                }

                mIL.BeginExceptionBlock();
                mIL.Emit(OpCodes.Ldarg_0);
                mIL.Emit(OpCodes.Ldfld, field);
                for (int t = 0; t < mTypes.Length; t++)
                {
                    mIL.Emit(OpCodes.Ldarg, t + 1);
                }
                mIL.Emit(OpCodes.Callvirt, currentMthod);

                //method ReturnData
                LocalBuilder returnData = null;
                if (currentMthod.ReturnType != typeof(void))
                {
                    if (currentMthod.ReturnType.IsValueType)
                        mIL.Emit(OpCodes.Box, currentMthod.ReturnType);

                    //Declare Method ReturnData
                    returnData = mIL.DeclareLocal(currentMthod.ReturnType);
                    mIL.Emit(OpCodes.Stloc, returnData);

                    //Method ReturnData
                    mIL.Emit(OpCodes.Ldloc, afterContext);
                    mIL.Emit(OpCodes.Ldloc, returnData);
                    mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_Result"), new[] { typeof(object) });
                }

                mIL.BeginCatchBlock(typeof(Exception));

                //Declare ExceptionContext
                var exceptionContext = mIL.DeclareLocal(typeof(ExceptionContext));
                mIL.Emit(OpCodes.Newobj, typeof(ExceptionContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, exceptionContext);

                //Declare exception
                var exception = mIL.DeclareLocal(typeof(Exception));
                mIL.Emit(OpCodes.Stloc, exception);

                //ExceptionContext exception
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, exception);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_Exception"), new[] { typeof(Exception) });

                //ExceptionContext MethodName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //ExceptionContext Paramter;
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, local);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_Paramter"), new[] { typeof(object[]) });

                //attr
                for (int ex = 0; ex < aopAttribute.Count; ex++)
                {
                    //ExceptionContext
                    mIL.Emit(OpCodes.Ldloc, attrList[ex]);
                    mIL.Emit(OpCodes.Ldloc, exceptionContext);
                    mIL.Emit(OpCodes.Callvirt, exceptionMethod);
                }

                mIL.EndExceptionBlock();

                //attr
                for (int attr = 0; attr < aopAttribute.Count; attr++)
                {
                    //AfterContext
                    mIL.Emit(OpCodes.Ldloc, attrList[attr]);
                    mIL.Emit(OpCodes.Ldloc, afterContext);
                    mIL.Emit(OpCodes.Callvirt, afterMethod);
                }

                if (currentMthod.ReturnType != typeof(void))
                    mIL.Emit(OpCodes.Ldloc, returnData);

                mIL.Emit(OpCodes.Ret);
            }

            var dynMethod = new DynamicMethod("Instance", interfaceType, null);

            var dynIL = dynMethod.GetILGenerator();
            dynIL.Emit(OpCodes.Newobj, builder.CreateTypeInfo().AsType().GetConstructor(Type.EmptyTypes));
            dynIL.Emit(OpCodes.Ret);

            return dynMethod;
        }

        internal static object Instance(Type attrType, Type serviceType, Type interfaceType)
        {
            return Proxy(serviceType, interfaceType, attrType).CreateDelegate(Expression.GetFuncType(new Type[] { interfaceType })).DynamicInvoke();
        }
    }
}