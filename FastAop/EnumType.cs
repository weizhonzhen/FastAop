namespace FastAop
{
    public enum WebType
    {
        Mvc = 0,
        WebApi = 1,
        MvcAndWebApi = 2,
        WebForm = 3
    }

    public enum ServiceLifetime
    {
        Singleton,
        Scoped,
        Transient
    }
}
