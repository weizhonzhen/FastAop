﻿using System;
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

                //Generic Method
                if (currentMthod.IsGenericMethod)
                {
                    method.DefineGenericParameters(currentMthod.GetGenericArguments().ToList().Select(a => a.Name).ToArray());

                    if (currentMthod.GetGenericArguments()[0].GenericParameterAttributes.ToString() != GenericParameterAttributes.None.ToString())
                        throw new Exception($"Interface class name:{interfaceType.Name},method name:{currentMthod.Name}, not support Generic Method constraint");
                }

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

                //AttributeName
                var attList = serviceType.GetMethod(currentMthod.Name, mTypes).GetCustomAttributes().ToList().Select(a => a.GetType().Name).ToArray();
                var AttributeName = mIL.DeclareLocal(typeof(string[]));
                mIL.Emit(OpCodes.Ldc_I4, attList.Length);
                mIL.Emit(OpCodes.Newarr, typeof(string));
                mIL.Emit(OpCodes.Stloc, AttributeName);

                for (int t = 0; t < attList.Length; t++)
                {
                    mIL.Emit(OpCodes.Ldloc, AttributeName);
                    mIL.Emit(OpCodes.Ldc_I4, t);
                    mIL.Emit(OpCodes.Ldstr, attList[t]);
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

                //BeforeContext ServerName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldstr, serviceType.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_ServiceName"), new[] { typeof(string) });

                //BeforeContext ArgumentName
                if (currentMthod.IsGenericMethod && currentMthod.DeclaringType.GenericTypeArguments.Length > 0)
                {
                    mIL.Emit(OpCodes.Ldloc, beforeContext);
                    mIL.Emit(OpCodes.Ldstr, currentMthod.DeclaringType.GenericTypeArguments[0].Name);
                    mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_ServiceArgumentName"), new[] { typeof(string) });
                }

                //Before BeforeContext MethodName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //BeforeContext AttributeName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

                //BeforeContext ResultType
                mIL.Emit(OpCodes.Ldloc, beforeContext); 
                mIL.Emit(OpCodes.Ldstr, currentMthod.ReturnType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_ResultType"), new[] { typeof(string) });

                //Declare AfterContext
                var afterContext = mIL.DeclareLocal(typeof(AfterContext));
                mIL.Emit(OpCodes.Newobj, typeof(AfterContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, afterContext);

                //AfterContext Paramter
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldloc, local);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_Paramter"), new[] { typeof(object[]) });

                //AfterContext ServerName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldstr, serviceType.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_ServiceName"), new[] { typeof(string) });

                //AfterContext ArgumentName
                if (currentMthod.IsGenericMethod && currentMthod.DeclaringType.GenericTypeArguments.Length > 0)
                {
                    mIL.Emit(OpCodes.Ldloc, afterContext);
                    mIL.Emit(OpCodes.Ldstr, currentMthod.DeclaringType.GenericTypeArguments[0].Name);
                    mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_ServiceArgumentName"), new[] { typeof(string) });
                }

                //AfterContext MethodName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //AfterContext AttributeName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

                //AfterContext ResultType
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.ReturnType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_ResultType"), new[] { typeof(string) });

                //Declare ExceptionContext
                var exceptionContext = mIL.DeclareLocal(typeof(ExceptionContext));
                mIL.Emit(OpCodes.Newobj, typeof(ExceptionContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, exceptionContext);
                
                //ExceptionContext AttributeName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

                //ExceptionContext ResultType
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.ReturnType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_ResultType"), new[] { typeof(string) });

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

                //Declare exception
                var exception = mIL.DeclareLocal(typeof(Exception));
                mIL.Emit(OpCodes.Stloc, exception);

                //ExceptionContext exception
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, exception);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_Exception"), new[] { typeof(Exception) });

                //Exception Context ServerName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldstr, serviceType.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_ServiceName"), new[] { typeof(string) });

                //Exception Context ArgumentName
                if (currentMthod.IsGenericMethod && currentMthod.DeclaringType.GenericTypeArguments.Length > 0)
                {
                    mIL.Emit(OpCodes.Ldloc, exceptionContext);
                    mIL.Emit(OpCodes.Ldstr, currentMthod.DeclaringType.GenericTypeArguments[0].Name);
                    mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_ServiceArgumentName"), new[] { typeof(string) });
                }

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

                if (method.ReturnType.IsValueType)
                    mIL.Emit(OpCodes.Unbox_Any, method.ReturnType);
                else
                    mIL.Emit(OpCodes.Castclass, method.ReturnType);

                //check update return data BeforeContext 
                var before_False = mIL.DefineLabel();
                var before_Ret = mIL.DefineLabel();
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("get_IsReturn"), null);
                mIL.Emit(OpCodes.Brfalse, before_False);

                //beforeContext IsReturn true 
                var beforeResult = mIL.DeclareLocal(currentMthod.ReturnType);
                mIL.Emit(OpCodes.Stloc, beforeResult);
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("get_Result"), null);
                mIL.Emit(OpCodes.Br, before_Ret);

                //beforeContext IsReturn false
                mIL.MarkLabel(before_False);
                var afterResult = mIL.DeclareLocal(currentMthod.ReturnType);
                mIL.Emit(OpCodes.Stloc, afterResult);
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("get_Result"), null);
                
                mIL.MarkLabel(before_Ret);

                //check update return data ExceptionContext
                var ex_False = mIL.DefineLabel();
                var ex_Ret = mIL.DefineLabel();
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("get_IsReturn"), null);
                mIL.Emit(OpCodes.Brfalse, ex_False);

                //exceptionContext IsReturn true 
                var exceptionResult = mIL.DeclareLocal(currentMthod.ReturnType);
                mIL.Emit(OpCodes.Stloc, exceptionResult);
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("get_Result"), null);
                mIL.Emit(OpCodes.Br, ex_Ret);

                //exceptionContext IsReturn false 
                mIL.MarkLabel(ex_False);
                //...

                mIL.MarkLabel(ex_Ret);
                
                mIL.Emit(OpCodes.Ret);
            }

            var dynMethod = new DynamicMethod("Instance", interfaceType, null);

            var dynIL = dynMethod.GetILGenerator();
            dynIL.Emit(OpCodes.Newobj, builder.CreateTypeInfo().AsType().GetConstructor(Type.EmptyTypes));
            dynIL.Emit(OpCodes.Ret);

            return dynMethod;
        }

        internal static object Instance(Type serviceType, Type interfaceType, Type attrType)
        {
            return Proxy(serviceType, interfaceType, attrType).CreateDelegate(Expression.GetFuncType(new Type[] { interfaceType })).DynamicInvoke();
        }

        public static dynamic InstanceDyn(Type serviceType, Type attrType = null)
        {
            return ProxyDyn(serviceType,attrType).CreateDelegate(Expression.GetFuncType(new Type[] { serviceType })).DynamicInvoke();
        }

        private static DynamicMethod ProxyDyn(Type serviceType, Type attrType = null)
        {
#if NETFRAMEWORK
                 throw new Exception("FastAop.Core not support net framwork");
#endif

            if (serviceType.IsInterface)
                throw new Exception($"serviceType not Interface class,class name:{serviceType.Name}");

            var assemblyName = new AssemblyName("FastAop.ILGrator.Core");
            var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule(assemblyName.Name);
            var builder = module.DefineType($"Aop_{assemblyName}", TypeAttributes.Public, null);

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

            var listMethod = serviceType.GetMethods(BindingFlags.SuppressChangeType | BindingFlags.Instance | BindingFlags.Public);

            //method list
            for (int m = 0; m < listMethod.Length; m++)
            {
                var currentMthod = listMethod[m];
                if (currentMthod.Name == "Equals" || currentMthod.Name == "GetHashCode" || currentMthod.Name == "ToString" || currentMthod.Name == "GetType")
                    continue;

                var mTypes = currentMthod.GetParameters().Select(d => d.ParameterType).ToArray();
                var method = builder.DefineMethod(currentMthod.Name, MethodAttributes.Public | MethodAttributes.NewSlot | MethodAttributes.Virtual, currentMthod.ReturnType, mTypes);

                //Generic Method
                if (currentMthod.IsGenericMethod)
                {
                    method.DefineGenericParameters(currentMthod.GetGenericArguments().ToList().Select(a => a.Name).ToArray());

                    if (currentMthod.GetGenericArguments()[0].GenericParameterAttributes.ToString() != GenericParameterAttributes.None.ToString())
                        throw new Exception($"serviceName class name:{serviceType.Name},method name:{currentMthod.Name}, not support Generic Method constraint");
                }

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

                //AttributeName
                var attList = serviceType.GetMethod(currentMthod.Name, mTypes).GetCustomAttributes().ToList().Select(a => a.GetType().Name).ToArray();
                var AttributeName = mIL.DeclareLocal(typeof(string[]));
                mIL.Emit(OpCodes.Ldc_I4, attList.Length);
                mIL.Emit(OpCodes.Newarr, typeof(string));
                mIL.Emit(OpCodes.Stloc, AttributeName);

                for (int t = 0; t < attList.Length; t++)
                {
                    mIL.Emit(OpCodes.Ldloc, AttributeName);
                    mIL.Emit(OpCodes.Ldc_I4, t);
                    mIL.Emit(OpCodes.Ldstr, attList[t]);
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

                //BeforeContext ServerName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldstr, serviceType.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_ServiceName"), new[] { typeof(string) });

                //BeforeContext ArgumentName
                if (currentMthod.IsGenericMethod && currentMthod.DeclaringType.GenericTypeArguments.Length > 0)
                {
                    mIL.Emit(OpCodes.Ldloc, beforeContext);
                    mIL.Emit(OpCodes.Ldstr, currentMthod.DeclaringType.GenericTypeArguments[0].Name);
                    mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_ServiceArgumentName"), new[] { typeof(string) });
                }

                //Before BeforeContext MethodName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //BeforeContext AttributeName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

                //BeforeContext ResultType
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.ReturnType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_ResultType"), new[] { typeof(string) });

                //Declare AfterContext
                var afterContext = mIL.DeclareLocal(typeof(AfterContext));
                mIL.Emit(OpCodes.Newobj, typeof(AfterContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, afterContext);

                //AfterContext Paramter
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldloc, local);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_Paramter"), new[] { typeof(object[]) });

                //AfterContext ServerName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldstr, serviceType.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_ServiceName"), new[] { typeof(string) });

                //AfterContext ArgumentName
                if (currentMthod.IsGenericMethod && currentMthod.DeclaringType.GenericTypeArguments.Length > 0)
                {
                    mIL.Emit(OpCodes.Ldloc, afterContext);
                    mIL.Emit(OpCodes.Ldstr, currentMthod.DeclaringType.GenericTypeArguments[0].Name);
                    mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_ServiceArgumentName"), new[] { typeof(string) });
                }

                //AfterContext MethodName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //AfterContext AttributeName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

                //AfterContext ResultType
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.ReturnType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_ResultType"), new[] { typeof(string) });

                //Declare ExceptionContext
                var exceptionContext = mIL.DeclareLocal(typeof(ExceptionContext));
                mIL.Emit(OpCodes.Newobj, typeof(ExceptionContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, exceptionContext);

                //ExceptionContext AttributeName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

                //ExceptionContext ResultType
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.ReturnType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_ResultType"), new[] { typeof(string) });

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

                //Declare exception
                var exception = mIL.DeclareLocal(typeof(Exception));
                mIL.Emit(OpCodes.Stloc, exception);

                //ExceptionContext exception
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, exception);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_Exception"), new[] { typeof(Exception) });

                //Exception Context ServerName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldstr, serviceType.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_ServiceName"), new[] { typeof(string) });

                //Exception Context ArgumentName
                if (currentMthod.IsGenericMethod && currentMthod.DeclaringType.GenericTypeArguments.Length > 0)
                {
                    mIL.Emit(OpCodes.Ldloc, exceptionContext);
                    mIL.Emit(OpCodes.Ldstr, currentMthod.DeclaringType.GenericTypeArguments[0].Name);
                    mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_ServiceArgumentName"), new[] { typeof(string) });
                }

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

                if (method.ReturnType.IsValueType)
                    mIL.Emit(OpCodes.Unbox_Any, method.ReturnType);
                else
                    mIL.Emit(OpCodes.Castclass, method.ReturnType);

                //check update return data BeforeContext 
                var before_False = mIL.DefineLabel();
                var before_Ret = mIL.DefineLabel();
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("get_IsReturn"), null);
                mIL.Emit(OpCodes.Brfalse, before_False);

                //beforeContext IsReturn true 
                var beforeResult = mIL.DeclareLocal(currentMthod.ReturnType);
                mIL.Emit(OpCodes.Stloc, beforeResult);
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("get_Result"), null);
                mIL.Emit(OpCodes.Br, before_Ret);

                //beforeContext IsReturn false
                mIL.MarkLabel(before_False);
                var afterResult = mIL.DeclareLocal(currentMthod.ReturnType);
                mIL.Emit(OpCodes.Stloc, afterResult);
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("get_Result"), null);

                mIL.MarkLabel(before_Ret);

                //check update return data ExceptionContext
                var ex_False = mIL.DefineLabel();
                var ex_Ret = mIL.DefineLabel();
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("get_IsReturn"), null);
                mIL.Emit(OpCodes.Brfalse, ex_False);

                //exceptionContext IsReturn true 
                var exceptionResult = mIL.DeclareLocal(currentMthod.ReturnType);
                mIL.Emit(OpCodes.Stloc, exceptionResult);
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("get_Result"), null);
                mIL.Emit(OpCodes.Br, ex_Ret);

                //exceptionContext IsReturn false 
                mIL.MarkLabel(ex_False);
                //...

                mIL.MarkLabel(ex_Ret);

                mIL.Emit(OpCodes.Ret);
            }

            var dynMethod = new DynamicMethod("Instance", serviceType, null);

            var dynIL = dynMethod.GetILGenerator();
            dynIL.Emit(OpCodes.Newobj, builder.CreateTypeInfo().AsType().GetConstructor(Type.EmptyTypes));
            dynIL.Emit(OpCodes.Ret);

            return dynMethod;
        }
    }
}