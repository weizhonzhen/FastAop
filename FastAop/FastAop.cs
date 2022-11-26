using FastAop.Constructor;
using FastAop.Context;
using FastAop.Factory;
using FastAop.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using ExceptionContext = FastAop.Context.ExceptionContext;

namespace FastAop
{
    public enum WebType
    {
        Mvc = 0,
        WebApi = 1,
        MvcAndWebApi = 2,
        WebForm = 3
    }

    public static class FastAop
    {
        internal static ConcurrentDictionary<Type, object> ServiceInstance = new ConcurrentDictionary<Type, object>();
        internal static ConcurrentDictionary<Type, Type> ServiceAopType = new ConcurrentDictionary<Type, Type>();
        internal static ConcurrentDictionary<Type, Type> ServiceType = new ConcurrentDictionary<Type, Type>();
        internal static ConcurrentDictionary<Type, ServiceLifetime> ServiceTime = new ConcurrentDictionary<Type, ServiceLifetime>();

        public static void Init(string nameSpaceService, WebType webType, Type aopType = null, ServiceLifetime lifetime=ServiceLifetime.Scoped)
        {
            if (string.IsNullOrEmpty(nameSpaceService))
                return;

            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
            {
                if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                    try { Assembly.Load(a.Name); } catch (Exception) { }
            });

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                Type interfaceType;
                try
                {
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

                        interfaceType = b;
                        if (!b.IsInterface && b.GetInterfaces().Any())
                        {
                            foreach (var iface in b.GetInterfaces())
                            {
                                ServiceAopType.SetValue(iface, aopType);
                                ServiceTime.SetValue(iface, lifetime);
                                ServiceInstance.SetValue(iface, Instance(b, iface, aopType));
                                ServiceType.SetValue(iface, b);
                            }
                        }
                        else if (!b.IsInterface && !b.GetInterfaces().Any())
                        {
                            Dic.SetValueDyn(b, FastAopDyn.Instance(b, aopType));
                            ServiceTime.SetValue(b, lifetime);
                            ServiceAopType.SetValue(interfaceType, aopType);
                            ServiceType.SetValue(interfaceType, b);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (ex is AopException)
                        throw ex;
                }
            });

            InitAutowired(nameSpaceService, aopType, lifetime);

            if (webType == WebType.WebForm)
                return;

            if (webType == WebType.Mvc)
                System.Web.Mvc.ControllerBuilder.Current.SetControllerFactory(new AopMvcFactory());

            if (webType == WebType.WebApi)
                System.Web.Http.GlobalConfiguration.Configuration.Services.Replace(typeof(System.Web.Http.Dispatcher.IHttpControllerActivator), new AopWebApiFactory());

            if (webType == WebType.MvcAndWebApi)
            {
                System.Web.Mvc.ControllerBuilder.Current.SetControllerFactory(new AopMvcFactory());
                System.Web.Http.GlobalConfiguration.Configuration.Services.Replace(typeof(System.Web.Http.Dispatcher.IHttpControllerActivator), new AopWebApiFactory());
            }
        }

        public static void InitGeneric(string nameSpaceService, string nameSpaceModel, WebType webType, Type aopType = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (aopType != null && aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (string.IsNullOrEmpty(nameSpaceService))
                return;

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
                try
                {
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                    {
                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (!b.IsGenericType)
                        {
                            Init(b.Namespace, webType, aopType);
                            return;
                        }

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
                                   var sType = b.MakeGenericType(new Type[1] { m });

                                    foreach(var iface in sType.GetInterfaces())
                                    {
                                        ServiceInstance.SetValue(iface, FastAop.Instance(sType, iface, aopType));
                                        ServiceAopType.SetValue(iface, aopType);
                                        ServiceType.SetValue(iface, sType);
                                        ServiceTime.SetValue(iface, lifetime);
                                    }
                                }
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (ex is AopException)
                        throw ex;
                }
            });

            InitAutowiredGeneric(nameSpaceModel, webType, lifetime, aopType);

            if (webType == WebType.WebForm)
                return;

            if (webType == WebType.Mvc)
                System.Web.Mvc.ControllerBuilder.Current.SetControllerFactory(new AopMvcFactory());

            if (webType == WebType.WebApi)
                System.Web.Http.GlobalConfiguration.Configuration.Services.Replace(typeof(System.Web.Http.Dispatcher.IHttpControllerActivator), new AopWebApiFactory());

            if (webType == WebType.MvcAndWebApi)
            {
                System.Web.Mvc.ControllerBuilder.Current.SetControllerFactory(new AopMvcFactory());
                System.Web.Http.GlobalConfiguration.Configuration.Services.Replace(typeof(System.Web.Http.Dispatcher.IHttpControllerActivator), new AopWebApiFactory());
            }
        }

        private static void InitAutowired(string nameSpaceService, Type aopType = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            Type interfaceType;
            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
           {
               if (assembly.IsDynamic)
                   return;
               try
               {
                   assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceService)).ToList().ForEach(b =>
                   {
                       if (b.IsGenericType)
                           return;

                       if (b.IsAbstract && b.IsSealed)
                           return;

                       if (b.IsInterface)
                           return;

                       interfaceType = b;
                       if (b.GetInterfaces().Any())
                       {
                           foreach (var iface in b.GetInterfaces())
                           {
                               var obj = Instance(b,iface, lifetime);

                               if (obj == null)
                                   return;

                               ServiceAopType.SetValue(iface, aopType);
                               ServiceType.SetValue(iface, b);
                               ServiceInstance.SetValue(iface, obj);
                               ServiceTime.SetValue(iface, lifetime);
                           }
                       }
                       else
                       {
                           var obj = Instance(b, null, lifetime);

                           if (obj == null)
                               return;

                           Dic.SetValueDyn(b, obj);
                           ServiceAopType.SetValue(interfaceType, aopType);
                           ServiceType.SetValue(interfaceType, b);
                           ServiceTime.SetValue(interfaceType, lifetime);
                       }
                   });
               }
               catch (Exception ex)
               {
                   if (ex is AopException)
                       throw ex;
               }
           });
        }

        private static object Instance(Type b, Type iface, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            object obj = null, temp = null;
            foreach (var item in b.GetRuntimeFields())
            {
                if (item.GetCustomAttribute<Autowired>() == null)
                    continue;

                if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                    throw new AopException($"{b.Name} field {item} attribute must readonly");

                if (item.FieldType.isSysType())
                    throw new AopException($"{b.Name} field {item} is system type not support");

                obj = Dic.GetValueDyn(b);

                if (b.IsInterface && obj == null)
                    obj = ServiceInstance.GetValue(b);

                if (b.GetInterfaces().Any() && obj == null)
                    obj = ServiceInstance.GetValue(iface);

                if (obj == null)
                    continue;

                if (temp == null)
                {
                    var model = Constructor.Constructor.Get(b, null);
                    temp = Activator.CreateInstance(b, model.dynParam.ToArray());
                }

                foreach (var param in temp.GetType().GetRuntimeFields())
                {
                    if (param.GetCustomAttribute<Autowired>() == null)
                        continue;

                    if (!param.Attributes.HasFlag(FieldAttributes.InitOnly))
                        throw new AopException($"{b.Name} field {item} attribute must readonly");

                    if (item.FieldType.isSysType())
                        throw new AopException($"{b.Name} field {item} is system type not support");

                    if (param.FieldType.IsInterface)
                        Instance(ServiceInstance.GetValue(param.FieldType).GetType(),iface,lifetime);
                    else if (param.FieldType.GetInterfaces().Any())
                    {
                        foreach(var iType in param.FieldType.GetInterfaces())
                        {
                            Instance(ServiceInstance.GetValue(iType).GetType(), iType,lifetime);
                        }
                    }
                    else
                        Instance(Dic.GetValueDyn(param.FieldType).GetType(),iface);
                }

                if (item.FieldType.IsInterface)
                    item.SetValueDirect(__makeref(temp), ServiceInstance.GetValue(item.FieldType));
                else if (item.FieldType.GetInterfaces().Any())
                    item.SetValueDirect(__makeref(temp), ServiceInstance.GetValue(iface));
                else
                    item.SetValue(temp, Dic.GetValueDyn(item.FieldType));

                var objFildType = obj.GetType().GetRuntimeFields().First().FieldType;
                if (!objFildType.IsInterface && !objFildType.GetInterfaces().Any())
                    obj.GetType().GetRuntimeFields().First().SetValue(obj, temp);
                else
                    obj.GetType().GetRuntimeFields().First().SetValueDirect(__makeref(obj), temp);

                if (obj.GetType().FullName.EndsWith(".dynamic"))
                    objFildType.GetRuntimeFields().First().SetValue(obj, Dic.GetValueDyn(item.FieldType));
            }

            return obj;
        }

        private static void InitAutowiredGeneric(string nameSpaceModel, WebType webType, ServiceLifetime lifetime, Type aopType = null)
        {
            var list = InitModelType(nameSpaceModel);

            AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
            {
                if (assembly.IsDynamic)
                    return;

                try
                {
                    assembly.ExportedTypes.ToList().ForEach(b =>
                    {
                        object obj = null, temp = null;

                        foreach (var item in b.GetRuntimeFields())
                        {
                            if (item.GetCustomAttribute<Autowired>() == null)
                                continue;

                            if (!b.IsGenericType)
                                continue;

                            if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                                throw new AopException($"{b.Name} field {item} attribute must readonly");

                            if (item.FieldType.isSysType())
                                throw new AopException($"{b.Name} field {item} is system type not support");

                            if (ServiceInstance.GetValue(item.FieldType) == null)
                                FastAop.Init(b.Namespace, webType,aopType);

                            if (!b.IsInterface && !b.GetInterfaces().Any())
                                continue;

                            list.ForEach(m =>
                            {
                                var type = b.MakeGenericType(new Type[1] { m });
                                if (b.IsInterface)
                                    obj = ServiceInstance.GetValue(type);

                                if (b.GetInterfaces().Any())
                                    obj = ServiceInstance.GetValue(type.GetInterfaces().First());

                                if (obj == null)
                                    return;

                                if (temp == null)
                                {
                                    var tempType = obj.GetType().GetRuntimeFields().First().FieldType;
                                    var model = Constructor.Constructor.Get(tempType, null);
                                    temp = Activator.CreateInstance(tempType, model.dynParam.ToArray());
                                }
                                var newItem = temp.GetType().GetRuntimeFields().ToList().Find(a => a.FieldType == item.FieldType && a.Name == item.Name);

                                if (item.FieldType.IsInterface)
                                    newItem.SetValueDirect(__makeref(temp), ServiceInstance.GetValue(item.FieldType));
                                else
                                    newItem.SetValue(temp, Dic.GetValueDyn(item.FieldType));

                                obj.GetType().GetRuntimeFields().First().SetValueDirect(__makeref(obj), temp);
                            });
                        }

                        if (obj == null)
                            return;

                        list.ForEach(m =>
                        {
                            var sType = b.MakeGenericType(new Type[1] { m });
                            var interfaceType = sType;
                            if (b.GetInterfaces().Any())
                            {
                                foreach(var iface in sType.GetInterfaces())
                                {
                                    ServiceInstance.SetValue(iface, obj);
                                    ServiceAopType.SetValue(iface, aopType);
                                    ServiceType.SetValue(iface, b);
                                    ServiceTime.SetValue(iface, lifetime);
                                }
                            }
                            else
                            {
                                Dic.SetValueDyn(interfaceType, obj);
                                ServiceAopType.SetValue(interfaceType, aopType);
                                ServiceType.SetValue(interfaceType, b);
                                ServiceTime.SetValue(interfaceType, lifetime);
                            }
                        });
                    });
                }
                catch (Exception ex)
                {
                    if (ex is AopException)
                        throw ex;
                }
            });
        }

        private static object InstanceGeneric(List<Type> list, Type b, WebType webType, ServiceLifetime lifetime)
        {
            object obj = null, temp = null;
            foreach (var item in b.GetRuntimeFields())
            {
                if (item.GetCustomAttribute<Autowired>() == null)
                    continue;

                if (!item.Attributes.HasFlag(FieldAttributes.InitOnly))
                    throw new AopException($"{b.Name} field {item} attribute must readonly");

                if (item.FieldType.isSysType())
                    throw new AopException($"{b.Name} field {item} is system type not support");

                if (ServiceInstance.GetValue(item.FieldType) == null)
                    Init(b.Namespace, webType);

                if (!b.IsInterface && !b.GetInterfaces().Any())
                    continue;

                list.ForEach(m =>
                {
                    if (obj == null)
                    {
                        var typeGeneric = b.MakeGenericType(new Type[1] { m });
                        if (b.IsInterface)
                            obj = ServiceInstance.GetValue(typeGeneric);

                        if (b.GetInterfaces().Any())
                            obj = ServiceInstance.GetValue(typeGeneric.GetInterfaces().First());
                    }

                    if (obj == null)
                        return;

                    var type = obj.GetType().GetRuntimeFields().First().FieldType;
                    if (temp == null)
                    {
                        var model = Constructor.Constructor.Get(type, null);
                        temp = Activator.CreateInstance(type, model.dynParam.ToArray());
                    }

                    foreach (var param in temp.GetType().GetRuntimeFields())
                    {
                        if (param.GetCustomAttribute<Autowired>() == null)
                            continue;

                        if (!param.Attributes.HasFlag(FieldAttributes.InitOnly))
                            throw new AopException($"{type.Name} field {item} attribute must readonly");

                        if (param.FieldType.isSysType())
                            throw new AopException($"{type.Name} field {item} is system type not support");

                        if (param.FieldType.IsInterface)
                            InstanceGeneric(list, ServiceInstance.GetValue(param.FieldType).GetType(), webType,lifetime);
                        else if (param.FieldType.GetInterfaces().Any())
                            InstanceGeneric(list, ServiceInstance.GetValue(param.FieldType.GetInterfaces().First()).GetType(), webType,lifetime);
                    }

                    var newItem = temp.GetType().GetRuntimeFields().ToList().Find(a => a.FieldType == item.FieldType && a.Name == item.Name);
                    if (item.FieldType.IsInterface)
                        newItem.SetValueDirect(__makeref(temp), ServiceInstance.GetValue(item.FieldType));
                    else if (item.FieldType.GetInterfaces().Any())
                        newItem.SetValueDirect(__makeref(temp), ServiceInstance.GetValue(item.FieldType.GetInterfaces().First()));
                    else
                        newItem.SetValue(temp, Dic.GetValueDyn(item.FieldType));

                    obj.GetType().GetRuntimeFields().First().SetValueDirect(__makeref(obj), temp);

                });
            }

            return obj;
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

                    try
                    {
                        assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace.Contains(nameSpaceModel)).ToList().ForEach(b =>
                        {
                            if (b.IsPublic && b.IsClass && !b.IsAbstract && !b.IsGenericType)
                                list.Add(b);
                        });
                    }
                    catch { }
                });
            }
            return list;
        }

        public static object Instance(Type serviceType, Type interfaceType, Type attrType = null)
        {
            if (attrType != null && attrType.BaseType != typeof(FastAopAttribute))
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

            AssemblyName assemblyName;

            if (!model.interfaceType.IsGenericType)
                assemblyName = new AssemblyName(model.interfaceType.FullName);
            else
            {
                var modelName = model.interfaceType.GetGenericArguments().Length > 0 ? model.interfaceType.GetGenericArguments()[0].FullName : "";
                assemblyName = new AssemblyName($"{model.interfaceType.Namespace}.{model.interfaceType.Name.Replace("`1", "")}<{modelName}>");
            }

            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule(assemblyName.Name);
            var builder = module.DefineType(assemblyName.Name, TypeAttributes.Public, null, new Type[] { model.interfaceType });

            //Constructor method
            var field = builder.DefineField(assemblyName.Name, model.serviceType, FieldAttributes.Private);
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

                aopAttribute = FastAopContext.GetMethod(serviceMethodList, currentMthod, mTypes)?.GetCustomAttributes().Where(d => aopAttrType.IsAssignableFrom(d.GetType())).Cast<FastAopAttribute>().OrderBy(d => d.Sort).ToList() ?? new List<FastAopAttribute>();
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

        public enum ServiceLifetime
        {
            Singleton,
            Scoped,
            Transient
        }
    }
}