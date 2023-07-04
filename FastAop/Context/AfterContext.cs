using FastAop.Cache;
using FastAop.Result;
using System.Reflection;
using System.Threading.Tasks;

namespace FastAop.Context
{
    public class AfterContext
    {
        internal object _Result;
        internal string _id;
        internal string _ServiceType;
        internal string[] _AttributeName;
        internal object[] _Paramter;

        public string Id
        {
            get { return _id; }
            set
            {
                if (string.IsNullOrEmpty(_id))
                    _id = value;
            }
        }

        public object[] Paramter
        {
            get { return _Paramter; }
            set
            {
                if (_Paramter == null)
                    _Paramter = value;
            }
        }

        public string ServiceType
        {
            get { return _ServiceType; }
            set
            {
                if (string.IsNullOrEmpty(_ServiceType))
                    _ServiceType = value;
            }
        }

        public MethodInfo Method
        {
            get
            {
                return FastAopCache.Get(Id);
            }
            internal set { }
        }

        public object Result
        {
            get
            {
                return _Result;
            }
            set
            {
                if (Method.ReturnType == typeof(void))
                    return;

                _Result = BaseResult.SetResult(this, value);
            }
        }

        public object TaskResult
        {
            get
            {
                return BaseResult.GetTaskResult(Result);
            }
            internal set { }
        }

        public bool IsTaskResult
        {
            get
            {
                return Method.ReturnType.BaseType == typeof(Task) || Method.ReturnType == typeof(Task);
            }
            internal set { }
        }

        public string[] AttributeName
        {
            get { return _AttributeName; }
            set
            {
                if (_AttributeName == null)
                    _AttributeName = value;
            }
        }
    }
}
