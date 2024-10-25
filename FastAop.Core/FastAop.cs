using FastAop.Core.Context;
using FastAop.Core.Model;
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
            var model = Constructor.Constructor.Get(serviceType, interfaceType);
            var funcMethod = Proxy(model).CreateDelegate(Expression.GetFuncType(model.dynType.ToArray()));

            try
            {
                return funcMethod.DynamicInvoke(model.dynParam.ToArray());
            }
            catch
            {
                throw new Exception($"Type: {serviceType.FullName},Constructor Paramter: {string.Join(",", model.dynType.Select(a => a.GetType().Name))}");
            }
        }

        public static I Instance<S, I>() where S : class where I : class
        {
            var model = Constructor.Constructor.Get(typeof(S), typeof(I));
            var funcMethod = Proxy(model).CreateDelegate(Expression.GetFuncType(model.dynType.ToArray()));

            try
            {
                return (I)funcMethod.DynamicInvoke(model.dynParam.ToArray());
            }
            catch
            {
                throw new Exception($"Type: {typeof(S).FullName},Constructor Paramter: {string.Join(",", model.constructorType.Select(a => a.Name))}");
            }
        }

        internal static object Instance(Type serviceType, Type interfaceType, Type attrType)
        {
            var model = Constructor.Constructor.Get(serviceType, interfaceType);
            var funcMethod = Proxy(model, attrType).CreateDelegate(Expression.GetFuncType(model.dynType.ToArray()));

            try
            {
                return funcMethod.DynamicInvoke(model.dynParam.ToArray());
            }
            catch
            {
                throw new Exception($"Type: {serviceType.FullName},Constructor Paramter: {string.Join(",", model.constructorType.Select(a => a.Name))}");
            }
        }

        private static DynamicMethod Proxy(ConstructorModel model, Type attrType = null)
        {
            var arryType = model.constructorType.Count > 0 ? model.constructorType.ToArray() : Type.EmptyTypes;

            if (!model.interfaceType.IsPublic)
                throw new Exception($"interfaceType is not public class,class name:{model.interfaceType.Name}");

            if (!model.interfaceType.IsInterface)
                throw new Exception($"interfaceType only Interface class,class name:{model.interfaceType.Name}");

            if (model.serviceType.IsInterface)
                throw new Exception($"serviceType is Interface class,class name:{model.serviceType.Name}");

            if (!model.serviceType.GetInterfaces().ToList().Exists(a => a == model.interfaceType))
                throw new Exception($"serviceType  getInterfaces class not have Interfaces class:{model.interfaceType.Name}");

            if (model.serviceType.GetConstructor(arryType) == null)
                throw new Exception($"serviceType class have Constructor Paramtes not support,class name:{model.serviceType.Name}");

            AssemblyName assemblyName;

            if (!model.interfaceType.IsGenericType)
                assemblyName = new AssemblyName($"{model.interfaceType.FullName}.Aop");
            else
            {
                var modelName = model.interfaceType.GetGenericArguments().Length > 0 ? model.interfaceType.GetGenericArguments()[0].FullName : "";
                assemblyName = new AssemblyName($"{model.interfaceType.Namespace}.{model.interfaceType.Name.Replace("`1", "")}<{modelName}>.Aop");
            }

            var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule(assemblyName.Name);
            var builder = module.DefineType(assemblyName.Name, TypeAttributes.Public, null, new Type[] { model.interfaceType });

            //Constructor method
            FieldBuilder field;
            if (!model.serviceType.IsGenericType)
                field = builder.DefineField(model.serviceType.FullName, model.serviceType, FieldAttributes.Private);
            else
            {
                var modelName = model.serviceType.GetGenericArguments().Length > 0 ? model.serviceType.GetGenericArguments()[0].FullName : "";
                field = builder.DefineField($"{model.serviceType.Namespace}.{model.serviceType.Name.Replace("`1", "")}<{modelName}>", model.serviceType, FieldAttributes.Private);
            }

            var constructor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, arryType);

            var cIL = constructor.GetILGenerator();
            cIL.Emit(OpCodes.Ldarg_0);

            //constructor param
            if (model.constructorType.Count > 0)
            {
                for (int i = 1; i <= model.dynParam.Count; i++)
                {
                    cIL.Emit(OpCodes.Ldarg, i);
                }
            }

            cIL.Emit(OpCodes.Newobj, model.serviceType.GetConstructor(arryType));
            cIL.Emit(OpCodes.Stfld, field);
            cIL.Emit(OpCodes.Ret);

            var methodList = model.interfaceType.GetMethods(BindingFlags.SuppressChangeType | BindingFlags.Instance | BindingFlags.Public);
            var serviceMethodList = model.serviceType.GetMethods().ToList();

            //method list
            for (int m = 0; m < methodList.Length; m++)
            {
                var currentMthod = methodList[m];              
                var mTypes = currentMthod.GetParameters().Select(d => d.ParameterType).ToArray();
                var method = builder.DefineMethod(currentMthod.Name, MethodAttributes.Public | MethodAttributes.NewSlot | MethodAttributes.Virtual, currentMthod.ReturnType, mTypes);

                //Generic Method
                if (currentMthod.IsGenericMethod)
                {
                    var param = method.DefineGenericParameters(currentMthod.GetGenericMethodDefinition().GetGenericArguments().ToList().Select(a => a.Name).ToArray());

                    for (var g = 0; g < param.Length; g++)
                    {
                        var constraint = currentMthod.GetGenericArguments()[g];
                        param[g].SetBaseTypeConstraint(constraint.BaseType);
                        param[g].SetInterfaceConstraints(constraint.GetInterfaces());
                        param[g].SetGenericParameterAttributes(constraint.GenericParameterAttributes);
                    }
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
                    else
                        mIL.Emit(OpCodes.Castclass, mTypes[t]);
                    mIL.Emit(OpCodes.Stelem_Ref);
                }

                //AttributeName
                var attList = (FastAopContext.GetMethod(serviceMethodList, currentMthod, mTypes)?.GetCustomAttributes().ToList().Select(a => a.GetType().Name).ToList() ?? new List<string>()).ToArray();
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
                mIL.Emit(OpCodes.Callvirt, typeof(BeforeContext).GetProperty("Paramter").GetSetMethod(true));

                //BeforeContext Service_Type
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldstr, model.serviceType.AssemblyQualifiedName);
                mIL.Emit(OpCodes.Callvirt, typeof(BeforeContext).GetProperty("ServiceType").GetSetMethod(true));

                //BeforeContext methodinfo
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldtoken, currentMthod);
                if (model.interfaceType.IsGenericType || currentMthod.IsGenericMethod)
                {
                    mIL.Emit(OpCodes.Ldtoken, model.interfaceType);                    
                    mIL.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) }));
                }
                else
                    mIL.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }));
                mIL.Emit(OpCodes.Callvirt, typeof(BeforeContext).GetProperty("Method").GetSetMethod(true));

                //BeforeContext AttributeName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.Emit(OpCodes.Callvirt, typeof(BeforeContext).GetProperty("AttributeName").GetSetMethod(true));

                //Declare AfterContext
                var afterContext = mIL.DeclareLocal(typeof(AfterContext));
                mIL.Emit(OpCodes.Newobj, typeof(AfterContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, afterContext);

                //AfterContext Paramter
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldloc, local);
                mIL.Emit(OpCodes.Callvirt, typeof(AfterContext).GetProperty("Paramter").GetSetMethod(true));

                //AfterContext ServerName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldstr, model.serviceType.AssemblyQualifiedName);
                mIL.Emit(OpCodes.Callvirt, typeof(AfterContext).GetProperty("ServiceType").GetSetMethod(true));

                //AfterContext methodinfo
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldtoken, currentMthod);
                if (model.interfaceType.IsGenericType || currentMthod.IsGenericMethod)
                {
                    mIL.Emit(OpCodes.Ldtoken, model.interfaceType);
                    mIL.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) }));
                }
                else
                    mIL.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }));
                mIL.Emit(OpCodes.Callvirt, typeof(AfterContext).GetProperty("Method").GetSetMethod(true));

                //AfterContext AttributeName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.Emit(OpCodes.Callvirt, typeof(AfterContext).GetProperty("AttributeName").GetSetMethod(true));

                //Declare ExceptionContext
                var exceptionContext = mIL.DeclareLocal(typeof(ExceptionContext));
                mIL.Emit(OpCodes.Newobj, typeof(ExceptionContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, exceptionContext);

                //ExceptionContext AttributeName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.Emit(OpCodes.Callvirt, typeof(ExceptionContext).GetProperty("AttributeName").GetSetMethod(true));

                //aop attr
                var aopAttribute = new List<FastAopAttribute>();
                var attrList = new List<LocalBuilder>();
                var aopAttrType = typeof(FastAopAttribute);
                var beforeMethod = aopAttrType.GetMethod("Before");
                var afterMethod = aopAttrType.GetMethod("After");
                var exceptionMethod = aopAttrType.GetMethod("Exception");

                aopAttribute = FastAopContext.GetMethod(serviceMethodList, currentMthod, mTypes)?.GetCustomAttributes().Where(d => aopAttrType.IsAssignableFrom(d.GetType())).Cast<FastAopAttribute>().OrderBy(d => d.Sort).ToList() ?? new List<FastAopAttribute>();
                if (attrType != null)
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
                    else
                        mIL.Emit(OpCodes.Castclass, currentMthod.ReturnType);

                    //Declare Method ReturnData
                    returnData = mIL.DeclareLocal(typeof(object));
                    mIL.Emit(OpCodes.Stloc, returnData);

                    //Method ReturnData
                    mIL.Emit(OpCodes.Ldloc, afterContext);
                    mIL.Emit(OpCodes.Ldloc, returnData);

                    mIL.Emit(OpCodes.Callvirt, typeof(AfterContext).GetProperty("Result").GetSetMethod(true));
                }

                mIL.BeginCatchBlock(typeof(Exception));

                //Declare exception
                var exception = mIL.DeclareLocal(typeof(Exception));
                mIL.Emit(OpCodes.Stloc, exception);

                //ExceptionContext exception
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, exception);
                mIL.Emit(OpCodes.Callvirt, typeof(ExceptionContext).GetProperty("Exception").GetSetMethod(true));

                //Exception Context ServerName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldstr, model.serviceType.AssemblyQualifiedName);
                mIL.Emit(OpCodes.Callvirt, typeof(ExceptionContext).GetProperty("ServiceType").GetSetMethod(true));

                //ExceptionContext methodinfo
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldtoken, currentMthod);
                if (model.interfaceType.IsGenericType || currentMthod.IsGenericMethod)
                {
                    mIL.Emit(OpCodes.Ldtoken, model.interfaceType);
                    mIL.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) }));
                }
                else
                    mIL.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }));
                mIL.Emit(OpCodes.Callvirt, typeof(ExceptionContext).GetProperty("Method").GetSetMethod(true));

                //ExceptionContext Paramter;
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, local);
                mIL.Emit(OpCodes.Callvirt, typeof(ExceptionContext).GetProperty("Paramter").GetSetMethod(true));

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
                {
                    mIL.Emit(OpCodes.Ldloc, returnData);

                    if (currentMthod.ReturnType.IsValueType)
                        mIL.Emit(OpCodes.Unbox_Any, currentMthod.ReturnType);
                    else
                        mIL.Emit(OpCodes.Castclass, currentMthod.ReturnType);

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

                    if (currentMthod.ReturnType.IsValueType)
                        mIL.Emit(OpCodes.Unbox_Any, currentMthod.ReturnType);
                    else
                        mIL.Emit(OpCodes.Castclass, currentMthod.ReturnType);
                }
                mIL.Emit(OpCodes.Ret);
            }

            var dynMethod = new DynamicMethod("Instance", model.interfaceType, arryType);

            var dynIL = dynMethod.GetILGenerator();

            //constructor param type
            for (int i = 0; i < model.constructorType.Count; i++)
                dynIL.Emit(OpCodes.Ldarg, i);

            dynIL.Emit(OpCodes.Newobj, builder.CreateTypeInfo().AsType().GetConstructor(arryType));
            dynIL.Emit(OpCodes.Ret);
            return dynMethod;
        }
    }
}