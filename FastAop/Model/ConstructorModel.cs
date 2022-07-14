using System;
using System.Collections.Generic;

namespace FastAop.Model
{
    internal class ConstructorModel
    {
        public List<object> dynParam { get; set; } = new List<object>();

        public List<Type> constructorType { get; set; } = new List<Type>();

        public List<Type> dynType { get; set; } = new List<Type>();

        public Type serviceType { get; set; }

        public Type interfaceType { get; set; }
    }
}
