using System;

namespace FastAop
{
    internal class AopException : Exception
    {
        public AopException(string message) : base(message)
        {

        }
    }
}
