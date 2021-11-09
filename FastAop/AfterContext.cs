namespace FastAop
{
    public class AfterContext
    {
        public object[] Paramter { get; set; }

        public string MethodName { get; set; }

        public object Result { get; set; }
    }
}
