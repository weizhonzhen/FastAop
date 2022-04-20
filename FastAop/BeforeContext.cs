﻿using System;
using System.Reflection;
using System.Threading.Tasks;

namespace FastAop
{
    public class BeforeContext
    {
        private object _Result;
        public object[] Paramter { get; set; }

        public string ServiceType { get; set; }

        public string MethodName { get; set; }

        public MethodInfo Method { get { return string.IsNullOrEmpty(ServiceType) ? null : FastAopCache.GetType(ServiceType).GetMethod(MethodName); } internal set { } }

        public bool IsReturn { get; set; }

        public object Result
        {
            set { _Result = Convert.ChangeType(value, ResultType); }
            get { return _Result; }
        }

        public object TaskResult { get { return FastAop.GetTaskResult(Result); } internal set { } }

        public bool IsTaskResult { get { return Method.ReturnType.BaseType == typeof(Task) || Method.ReturnType == typeof(Task); } internal set { } }

        public string[] AttributeName { get; set; }

        public Type ResultType { get { return Method.ReturnType; } internal set { } }
    }
}