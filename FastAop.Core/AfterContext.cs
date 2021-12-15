namespace FastAop.Core
{
    public class AfterContext
    {
        public object[] Paramter { get; set; }

        public string ServiceArgumentName { get; set; }

        public string ServiceName { get; set; }

        public string MethodName { get; set; }

        public object Result { get; set; }
    }
}
