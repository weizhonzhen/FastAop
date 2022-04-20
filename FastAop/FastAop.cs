using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace FastAop
{
    public static class FastAop
    {
        internal static Dictionary<Type,object> _types = new Dictionary<Type, object>();
        internal static Dictionary<Type, dynamic> _typesDyn = new Dictionary<Type, dynamic>();

        public static void Init(string nameSpaceService, Type aopType)
        {
            if (aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (!string.IsNullOrEmpty(nameSpaceService))
            {
                Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
                {
                    if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                        try { Assembly.Load(a.Name); } catch (Exception) { }
                });

                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
                {
                    if (assembly.IsDynamic)
                        return;
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace == nameSpaceService).ToList().ForEach(b =>
                    {
                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (!b.IsInterface && b.GetInterfaces().Any())
                            _types.SetValue(b.GetInterfaces().First(), FastAop.Instance(b, b.GetInterfaces().First(), aopType));
                        else if (!b.IsInterface && !b.GetInterfaces().Any())
                            Dic.SetValueDyn(_typesDyn, b, FastAop.InstanceDyn(b, aopType));
                    });
                });
            }
        }

        public static void Init(string nameSpaceService)
        {
            if (!string.IsNullOrEmpty(nameSpaceService))
            {
                Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
                {
                    if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                        try { Assembly.Load(a.Name); } catch (Exception) { }
                });

                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
                {
                    if (assembly.IsDynamic)
                        return;
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace == nameSpaceService).ToList().ForEach(b =>
                    {
                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (!b.IsInterface && b.GetInterfaces().Any())
                            _types.SetValue(b.GetInterfaces().First(), FastAop.Instance(b, b.GetInterfaces().First()));
                        else if (!b.IsInterface && !b.GetInterfaces().Any())
                            Dic.SetValueDyn(_typesDyn, b, FastAop.InstanceDyn(b));
                    });
                });
            }
        }

        public static object Instance(Type serviceType, Type interfaceType)
        {
            return Proxy(serviceType, interfaceType).CreateDelegate(Expression.GetFuncType(new Type[] { interfaceType })).DynamicInvoke();
        }

        public static object Instance(Type serviceType, Type interfaceType, Type attrType)
        {
            if (attrType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"attrType baseType not is FastAopAttribute,class name:{attrType.Name}");

            return Proxy(serviceType, interfaceType, attrType).CreateDelegate(Expression.GetFuncType(new Type[] { interfaceType })).DynamicInvoke();
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

                return Proxy(serviceType, interfaceType).CreateDelegate(typeof(Func<I>)) as Func<I>;
            }
        }

        private static DynamicMethod Proxy(Type serviceType, Type interfaceType, Type attrType = null)
        {
            if (!interfaceType.IsPublic)
                throw new Exception($"interfaceType is not public class,class name:{interfaceType.Name}");

            if (!interfaceType.IsInterface)
                throw new Exception($"interfaceType only Interface class,class name:{interfaceType.Name}");

            if (serviceType.IsInterface)
                throw new Exception($"serviceType not Interface class,class name:{serviceType.Name}");

            if (serviceType.IsAbstract && serviceType.IsSealed)
                throw new Exception($"serviceType class is static class not support,class name:{serviceType.Name}");

            if (serviceType.GetConstructor(Type.EmptyTypes) == null)
                throw new Exception($"serviceType class have Constructor Paramtes not support,class name:{serviceType.Name}");

            var assemblyName = new AssemblyName("FastAop.ILGrator");
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
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
                var attList = (serviceType.GetMethod(currentMthod.Name, mTypes)?.GetCustomAttributes().ToList().Select(a => a.GetType().Name).ToList()?? new List<string>()).ToArray() ;
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
                mIL.Emit(OpCodes.Ldstr, serviceType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_ServiceType"), new[] { typeof(string) });

                //BeforeContext MethodName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //BeforeContext AttributeName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

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
                mIL.Emit(OpCodes.Ldstr, serviceType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_ServiceType"), new[] { typeof(string) });

                //AfterContext MethodName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //AfterContext AttributeName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

                //Declare ExceptionContext
                var exceptionContext = mIL.DeclareLocal(typeof(ExceptionContext));
                mIL.Emit(OpCodes.Newobj, typeof(ExceptionContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, exceptionContext);

                //ExceptionContext AttributeName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

                //aop attr
                var aopAttrType = typeof(FastAopAttribute);
                var beforeMethod = aopAttrType.GetMethod("Before");
                var afterMethod = aopAttrType.GetMethod("After");
                var exceptionMethod = aopAttrType.GetMethod("Exception");
                var aopAttribute = new List<FastAopAttribute>();

                aopAttribute = serviceType.GetMethod(currentMthod.Name, mTypes)?.GetCustomAttributes().Where(d => aopAttrType.IsAssignableFrom(d.GetType())).Cast<FastAopAttribute>().OrderBy(d => d.Sort).ToList() ?? new List<FastAopAttribute>();
                if (attrType != null)
                {
                    //auto add FastAopAttribute
                    var classCtorInfo = attrType.GetConstructor(Type.EmptyTypes);
                    var attributeBuilder = new CustomAttributeBuilder(classCtorInfo, new object[] { });
                    method.SetCustomAttribute(attributeBuilder);
                    aopAttribute.Add(Activator.CreateInstance(attrType) as FastAopAttribute);
                }

                var attrList = new List<LocalBuilder>();

                //attr
                for (int attr = 0; attr < aopAttribute.Count; attr++)
                {
                    //DeclareContext attr
                    var attr_Type = (aopAttribute[attr]).GetType();
                    var attrLocal = mIL.DeclareLocal(attr_Type);
                    mIL.Emit(OpCodes.Newobj, attr_Type.GetConstructor(Type.EmptyTypes));

                    //BeforeContext
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
                    returnData = mIL.DeclareLocal(typeof(object));
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

                //Exception Context exception
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, exception);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_Exception"), new[] { typeof(Exception) });

                //Exception Context ServerName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldstr, serviceType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_ServiceType"), new[] { typeof(string) });

                //Exception Context MethodName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //Exception Context Paramter
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
            dynIL.Emit(OpCodes.Newobj, builder.CreateType().GetConstructor(Type.EmptyTypes));
            dynIL.Emit(OpCodes.Ret);

            return dynMethod;
        }

        public static dynamic InstanceDyn(Type serviceType, Type attrType = null)
        {
            return ProxyDyn(serviceType, attrType).CreateDelegate(Expression.GetFuncType(new Type[] { serviceType })).DynamicInvoke();
        }

        public static dynamic InstanceDyn<T>(Type attrType = null)
        {
            var serviceType = typeof(T);
            return ProxyDyn(serviceType, attrType).CreateDelegate(Expression.GetFuncType(new Type[] { serviceType })).DynamicInvoke();
        }

        private static DynamicMethod ProxyDyn(Type serviceType, Type attrType = null)
        {
            if (serviceType.IsInterface)
                throw new Exception($"serviceType not Interface class,class name:{serviceType.Name}");

            if (serviceType.IsAbstract && serviceType.IsSealed)
                throw new Exception($"serviceType class is static class not support,class name:{serviceType.Name}");

            if (serviceType.GetConstructor(Type.EmptyTypes) == null)
                throw new Exception($"serviceType class have Constructor Paramtes not support,class name:{serviceType.Name}");

            var assemblyName = new AssemblyName("FastAop.ILGrator.Core");
            var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule(assemblyName.Name);
            var builder = module.DefineType($"Aop_{assemblyName}", TypeAttributes.Public, serviceType, new Type[0]);

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

            var listMethod = serviceType.GetMethods(BindingFlags.SuppressChangeType | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static );

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
                mIL.Emit(OpCodes.Ldstr, serviceType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_ServiceType"), new[] { typeof(string) });

                //Before BeforeContext MethodName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //BeforeContext AttributeName
                mIL.Emit(OpCodes.Ldloc, beforeContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

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
                mIL.Emit(OpCodes.Ldstr, serviceType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_ServiceType"), new[] { typeof(string) });

                //AfterContext MethodName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldstr, currentMthod.Name);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_MethodName"), new[] { typeof(string) });

                //AfterContext AttributeName
                mIL.Emit(OpCodes.Ldloc, afterContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(AfterContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

                //Declare ExceptionContext
                var exceptionContext = mIL.DeclareLocal(typeof(ExceptionContext));
                mIL.Emit(OpCodes.Newobj, typeof(ExceptionContext).GetConstructor(Type.EmptyTypes));
                mIL.Emit(OpCodes.Stloc, exceptionContext);

                //ExceptionContext AttributeName
                mIL.Emit(OpCodes.Ldloc, exceptionContext);
                mIL.Emit(OpCodes.Ldloc, AttributeName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_AttributeName"), new[] { typeof(string[]) });

                //aop attr
                var aopAttribute = new List<FastAopAttribute>();
                var attrList = new List<LocalBuilder>();
                var aopAttrType = typeof(FastAopAttribute);
                var beforeMethod = aopAttrType.GetMethod("Before");
                var afterMethod = aopAttrType.GetMethod("After");
                var exceptionMethod = aopAttrType.GetMethod("Exception");

                aopAttribute = serviceType.GetMethod(currentMthod.Name, mTypes).GetCustomAttributes().Where(d => aopAttrType.IsAssignableFrom(d.GetType())).Cast<FastAopAttribute>().OrderBy(d => d.Sort).ToList();
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

                if (currentMthod.IsStatic)
                {
                    mIL.Emit(OpCodes.Ldftn, currentMthod);
                    mIL.EmitCalli(OpCodes.Calli, CallingConventions.Standard, currentMthod.ReturnType, mTypes, null);
                }
                else
                    mIL.Emit(OpCodes.Callvirt, currentMthod);

                //method ReturnData
                LocalBuilder returnData = null;
                if (currentMthod.ReturnType != typeof(void))
                {
                    if (currentMthod.ReturnType.IsValueType)
                        mIL.Emit(OpCodes.Box, currentMthod.ReturnType);

                    //Declare Method ReturnData
                    returnData = mIL.DeclareLocal(typeof(object));
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
                mIL.Emit(OpCodes.Ldstr, serviceType.AssemblyQualifiedName);
                mIL.EmitCall(OpCodes.Callvirt, typeof(ExceptionContext).GetMethod("set_ServiceType"), new[] { typeof(string) });

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

            var method = result.GetType().GetMethods().ToList().Find(a => a.Name == "GetAwaiter");
            var data = Invoke(result, method, null);
            method = data.GetType().GetMethods().ToList().Find(a => a.Name == "GetResult");
            return method.Invoke(data, null);
        }
    }

    public class FastAopContext
    {
        public static T Resolve<T>()
        {
            if (typeof(T).IsInterface)
                return (T)FastAop._types.GetValue(typeof(T));
            else
                return (T)Activator.CreateInstance(typeof(T));
        }

        public static dynamic ResolveDyn<T>()
        {
            if (!typeof(T).GetInterfaces().Any())
                return FastAop._typesDyn.GetValue(typeof(T));
            else
                return null;
        }
    }

    internal class FastAopCache
    {
        private static Dictionary<string, Type> cache = new Dictionary<string, Type>();

        public static Type GetType(string key)
        {
            if (cache.ContainsKey(key))
                return cache[key];
            else
            {
                cache.Add(key, Type.GetType(key));
                return cache[key];
            }
        }
    }
}


namespace System.Collections.Generic
{
    internal static class Dic
    {
        internal static Object GetValue(this Dictionary<Type, object> item, Type key)
        {
            if (key == null)
                return null;

            if (item == null)
                return null;

            key = item.Keys.ToList().Find(a => a == key);

            if (item.Keys.ToList().Exists(a => a == key))
                return item[key];
            else
                return null;
        }

        internal static Dictionary<Type, object> SetValue(this Dictionary<Type, object> item, Type key, object value)
        {
            if (key == null)
                return null;

            if (item == null)
                return null;

            if (item.Keys.ToList().Exists(a => a == key))
                item[key] = value;
            else
                item.Add(key, value);

            return item;
        }

        internal static Object GetValueDyn(this Dictionary<Type, dynamic> item, Type key)
        {
            if (key == null)
                return null;

            if (item == null)
                return null;

            key = item.Keys.ToList().Find(a => a == key);

            if (item.Keys.ToList().Exists(a => a == key))
                return item[key];
            else
                return null;
        }

        internal static Dictionary<Type, dynamic> SetValueDyn(Dictionary<Type, dynamic> item, Type key, dynamic value)
        {
            if (key == null)
                return null;

            if (item == null)
                return null;

            if (item.Keys.ToList().Exists(a => a == key))
                item[key] = value;
            else
                item.Add(key, value);

            return item;
        }
    }
}