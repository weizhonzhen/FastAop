using System;

namespace FastAop.Core
{
    internal class AopException : Exception
    {
        public AopException(string message) : base(message)
        {

        }
    }
}
