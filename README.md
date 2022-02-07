# FastAop.Core
nuget url: https://www.nuget.org/packages/FastAop.Core/

in Startup.cs Startup mothod
```csharp
//more aop by 
services.AddFastAop("FastAop.Core.Test");

//add Global aop
services.AddFastAop("FastAop.Core.Test",typeof(LogAop));

//one aop
var model = FastAop.Instance<TestAop, ITestAop>();
dynamic model = FastAop.InstanceDyn(typeof(Test_Aop));//not interface class


public class TestAop : ITestAop
{
    [Log1Aop(Sort = 1)]
    [LogAop(Sort = 2)]
    public string Test1(string a, string b)
    {
        int ae = 0;
        a += "_b";
        var ad = 9 / ae;
        return a;
    }
}

public class Test_Aop
{
    [Log1Aop(Sort = 1)]
    [LogAop(Sort = 2)]
    public string Test1(string a, string b)
    {
        int ae = 0;
        a += "_b";
        var ad = 9 / ae;
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
        context.Result = "update result After"; //update result data
    }

    public override void Before(BeforeContext context)
    {
        context.IsReturn = true;
        context.Result = "update result Before"; //update result data 
    }

    public override void Exception(ExceptionContext exception)
    {
        context.IsReturn = true;
        context.Result = "update result Exception"; //update result data 
    }
}

public class Log1Aop : FastAopAttribute
{
    public override void After(AfterContext context)
    {
         context.Result = "update result After"; //update result data
    }

    public override void Before(BeforeContext context)
    {
        context.IsReturn = true;
        context.Result = "update result Before"; //update result data 
    }

    public override void Exception(ExceptionContext exception)
    {
        context.IsReturn = true;
        context.Result = "update result Exception"; //update result data 
    }
}

```
Test
```csharp
var model = services.BuildServiceProvider().GetRequiredService<ITestAop>();
model.Test1("1", "3");  //result data is "update result Exception"
model.Test1("2", "4"); //result data is "update result Exception"

dynamic model = FastAopContext.Resolve<Test_Aop>();//not interface class
model.Test1("1", "3");  //result data is "update result Exception"
model.Test1("2", "4"); //result data is "update result Exception"
 ```
# FastAop
nuget url: https://www.nuget.org/packages/FastAop/

Test
```csharp
var model = (ITestAop)FastAop.Instance(typeof(TestAop), typeof(ITestAop));
or
var model = FastAop.Instance<TestAop, ITestAop>();

//aotuo add aop
var model = (ITestAop)FastAop.AddAttribute(typeof(LogAop), typeof(TestAop), typeof(ITestAop));
model.Test1("1", "3"); //result data is "update result Exception"
model.Test1("2", "4"); //result data is "update result Exception"
```
