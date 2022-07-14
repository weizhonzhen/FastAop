using FastAop.Context;
using System;

namespace FastAop
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class FastAopAttribute : Attribute, IFastAop
    {
        public short Sort { get; set; } = 0;

        public abstract void Before(BeforeContext context);

        public abstract void After(AfterContext context);

        public abstract void Exception(ExceptionContext exception);
    }

    internal interface IFastAop
    {
        void Before(BeforeContext context);

        void After(AfterContext context);

        void Exception(ExceptionContext exception);
    }
}