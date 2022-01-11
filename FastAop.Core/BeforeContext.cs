namespace FastAop.Core
{
    public class BeforeContext
    {
        public object[] Paramter { get; set; }

        public string ServiceArgumentName { get; set; }

        public string ServiceName { get; set; }

        public string MethodName { get; set; }

        public bool IsReturn { get; set; }

        public object Result { get; set; }
    }
}
