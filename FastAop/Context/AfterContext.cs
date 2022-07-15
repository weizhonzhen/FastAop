﻿using FastAop.Cache;
using FastAop.Result;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace FastAop.Context
{
    public class AfterContext
    {
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

        public object Result { get; set; }

        public object TaskResult
        {
            get
            {
                return BaseResult.GetTaskResult(Result);
            }
            internal set { }
        }

        public bool IsTaskResult { get { return Method.ReturnType.BaseType == typeof(Task) || Method.ReturnType == typeof(Task); } internal set { } }

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