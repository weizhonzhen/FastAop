using FastAop.Context;
using FastAop.Model;
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
        internal static Dictionary<Type, object> _types = new Dictionary<Type, object>();

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
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                    {
                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (b.IsGenericType && b.GetGenericArguments().ToList().Select(a => a.FullName).ToList().Exists(n => n == null))
                            return;

                        if (b.BaseType == typeof(FastAopAttribute))
                            return;

                        if (b.BaseType == typeof(Attribute))
                            return;

                        b.GetConstructors().ToList().ForEach(c =>
                        {
                            Constructor.Constructor.depth = 0;
                            c.GetParameters().ToList().ForEach(p =>
                            {
                                Constructor.Constructor.Param(p.ParameterType, aopType);
                            });
                        });

                        if (!b.IsInterface && b.GetInterfaces().Any())
                            _types.SetValue(b.GetInterfaces().First(), FastAop.Instance(b, b.GetInterfaces().First(), aopType));
                        else if (!b.IsInterface && !b.GetInterfaces().Any())
                            Dic.SetValueDyn(b, FastAopDyn.Instance(b, aopType));
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
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                    {
                        var isServiceAttr = false;
                        b.GetMethods().ToList().ForEach(m =>
                        {
                            if (m.GetCustomAttributes().ToList().Exists(a => a.GetType().BaseType == typeof(FastAopAttribute)))
                                isServiceAttr = true;
                        });

                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (b.IsGenericType && b.GetGenericArguments().ToList().Select(a => a.FullName).ToList().Exists(n => n == null))
                            return;

                        if (b.BaseType == typeof(FastAopAttribute))
                            return;

                        if (b.BaseType == typeof(Attribute))
                            return;

                        b.GetConstructors().ToList().ForEach(c =>
                        {
                            Constructor.Constructor.depth = 0;
                            c.GetParameters().ToList().ForEach(p =>
                            {
                                Constructor.Constructor.Param(p.ParameterType, isServiceAttr);
                            });
                        });

                        if (!b.IsInterface && b.GetInterfaces().Any())
                            _types.SetValue(b.GetInterfaces().First(), FastAop.Instance(b, b.GetInterfaces().First()));
                        else if (!b.IsInterface && !b.GetInterfaces().Any())
                            Dic.SetValueDyn(b, FastAopDyn.Instance(b));
                    });
                });
            }
        }

        public static void InitGeneric(string nameSpaceService, string nameSpaceModel)
        {
            if (!string.IsNullOrEmpty(nameSpaceService))
            {
                Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
                {
                    if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                        try { Assembly.Load(a.Name); } catch (Exception) { }
                });

                var list = InitModelType(nameSpaceModel);

                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
                {
                    if (assembly.IsDynamic)
                        return;

                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                    {
                        var isServiceAttr = false;
                        b.GetMethods().ToList().ForEach(m =>
                        {
                            if (m.GetCustomAttributes().ToList().Exists(a => a.GetType().BaseType == typeof(FastAopAttribute)))
                                isServiceAttr = true;
                        });

                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (!b.IsGenericType)
                            return;

                        if (b.BaseType == typeof(FastAopAttribute))
                            return;

                        if (b.BaseType == typeof(Attribute))
                            return;

                        if (b.IsGenericType)
                        {
                            list.ForEach(m =>
                            {
                                b.GetConstructors().ToList().ForEach(c =>
                                {
                                    Constructor.Constructor.depth = 0;
                                    c.GetParameters().ToList().ForEach(p =>
                                    {
                                        Constructor.Constructor.Param(p.ParameterType, isServiceAttr);
                                    });
                                });

                                if (!b.IsInterface && b.GetInterfaces().Any())
                                {
                                    var serviceType = b.MakeGenericType(new Type[1] { m });
                                    _types.SetValue(serviceType.GetInterfaces().First(), FastAop.Instance(serviceType, serviceType.GetInterfaces().First()));
                                }
                            });
                        }
                    });
                });
            }
        }

        public static void InitGeneric(string nameSpaceService, string nameSpaceModel, Type aopType)
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

                var list = InitModelType(nameSpaceModel);

                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
                {
                    if (assembly.IsDynamic)
                        return;

                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                    {
                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (!b.IsGenericType)
                            return;

                        if (b.BaseType == typeof(FastAopAttribute))
                            return;

                        if (b.BaseType == typeof(Attribute))
                            return;

                        if (b.IsGenericType)
                        {
                            list.ForEach(m =>
                            {
                                b.GetConstructors().ToList().ForEach(c =>
                                {
                                    Constructor.Constructor.depth = 0;
                                    c.GetParameters().ToList().ForEach(p =>
                                    {
                                        Constructor.Constructor.Param(p.ParameterType, aopType);
                                    });
                                });

                                if (!b.IsInterface && b.GetInterfaces().Any())
                                {
                                    var serviceType = b.MakeGenericType(new Type[1] { m });
                                    _types.SetValue(serviceType.GetInterfaces().First(), FastAop.Instance(serviceType, serviceType.GetInterfaces().First(), aopType));
                                }
                            });
                        }
                    });
                });
            }
        }

        private static List<Type> InitModelType(string nameSpaceModel)
        {
            var list = new List<Type>();
            if (!string.IsNullOrEmpty(nameSpaceModel))
            {
                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
                {
                    if (assembly.IsDynamic)
                        return;

                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceModel)).ToList().ForEach(b =>
                    {
                        if (b.IsPublic && b.IsClass && !b.IsAbstract && !b.IsGenericType)
                            list.Add(b);
                    });
                });
            }
            return list;
        }

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
                throw new Exception($"Type: {serviceType.FullName},Constructor Paramter: {string.Join(",", model.constructorType.Select(a => a.Name))}");
            }
        }

        public static object Instance(Type serviceType, Type interfaceType, Type attrType)
        {
            if (attrType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"attrType baseType not is FastAopAttribute,class name:{attrType.Name}");

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

            if (model.serviceType.IsAbstract && model.serviceType.IsSealed)
                throw new Exception($"serviceType class is static class not support,class name:{model.serviceType.Name}");

            if (model.serviceType.GetConstructor(arryType) == null)
                throw new Exception($"serviceType class have Constructor Paramtes not support,class name:{model.serviceType.Name}");

            var assemblyName = new AssemblyName($"FastAop.ILGrator");
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule(assemblyName.Name);
            var builder = module.DefineType($"Aop_{assemblyName}", TypeAttributes.Public, null, new Type[] { model.interfaceType });

            //Constructor method
            var field = builder.DefineField($"Aop_{model.serviceType.Name}_Field", model.serviceType, FieldAttributes.Private);
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

            var listMethod = model.interfaceType.GetMethods(BindingFlags.SuppressChangeType | BindingFlags.Instance | BindingFlags.Public);

            //method list
            for (int m = 0; m < listMethod.Length; m++)
            {
                var currentMthod = listMethod[m];
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
                    mIL.Emit(OpCodes.Stelem_Ref);
                }

                //AttributeName
                var attList = (model.serviceType.GetMethods().ToList().Find(ms => ms == currentMthod)?.GetCustomAttributes().ToList().Select(a => a.GetType().Name).ToList() ?? new List<string>()).ToArray();
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
                mIL.Emit(OpCodes.Ldstr, model.serviceType.AssemblyQualifiedName);
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
                mIL.Emit(OpCodes.Ldstr, model.serviceType.AssemblyQualifiedName);
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

                aopAttribute = model.serviceType.GetMethods().ToList().Find(ms => ms == currentMthod)?.GetCustomAttributes().Where(d => aopAttrType.IsAssignableFrom(d.GetType())).Cast<FastAopAttribute>().OrderBy(d => d.Sort).ToList() ?? new List<FastAopAttribute>();
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
                mIL.Emit(OpCodes.Ldstr, model.serviceType.AssemblyQualifiedName);
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
                {
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
                    var beforeResult = mIL.DeclareLocal(typeof(object));
                    mIL.Emit(OpCodes.Stloc, beforeResult);
                    mIL.Emit(OpCodes.Ldloc, beforeContext);
                    mIL.EmitCall(OpCodes.Callvirt, typeof(BeforeContext).GetMethod("get_Result"), null);
                    mIL.Emit(OpCodes.Br, before_Ret);

                    //beforeContext IsReturn false
                    mIL.MarkLabel(before_False);
                    var afterResult = mIL.DeclareLocal(typeof(object));
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
                    var exceptionResult = mIL.DeclareLocal(typeof(object));
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
            dynIL.Emit(OpCodes.Newobj, builder.CreateType().GetConstructor(arryType));
            dynIL.Emit(OpCodes.Ret);
            return dynMethod;
        }
    }
}