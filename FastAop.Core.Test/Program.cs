using Microsoft.Extensions.DependencyInjection;
using System;

namespace FastAop.Core.Test
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IServiceCollection services = new ServiceCollection();
            services.AddFastAop("FastAop.Core.Test");

            var model = services.BuildServiceProvider().GetRequiredService<ITestAop>();

            model.Test1("1", "3");
            model.Test1("2", "4");
            Console.Read();
        }
    }

    public class TestAop : ITestAop
    {
        [Log1Aop(Sort = 1)]
        [LogAop(Sort = 2)]
        public string Test1(string a, string b)
        {
            int ae = 0;
            a += "_b";
            //var ad = 9 / ae;
            return a;
        }
    }

    public interface ITestAop
    {
        string Test1(string a, string b);
    }


    public class LogAop : FastAopAttribute
    {
        public override void After(AfterContext context)
        {
            //throw new NotImplementedException();
        }


        public override void Before(BeforeContext context)
        {
            //throw new NotImplementedException();

        }

        public override void Exception(ExceptionContext exception)
        {
            //throw new NotImplementedException();
        }
    }

    public class Log1Aop : FastAopAttribute
    {
        public override void After(AfterContext context)
        {
            //throw new NotImplementedException();
        }


        public override void Before(BeforeContext context)
        {
            //throw new NotImplementedException();

        }

        public override void Exception(ExceptionContext exception)
        {
            //throw new NotImplementedException();

        }
    }
}
