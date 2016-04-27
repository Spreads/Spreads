using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System {

    public static class DynamicUtil
    {
        private static dynamic _sr = (dynamic)new DynamicProperty();
        public static dynamic SR => _sr;
    }

    /// <summary>
    /// Any property exists and returns its name. Useful for quickly importing .NET's code with SR. resources.
    /// </summary>
    public class DynamicProperty : DynamicObject {
        public override System.Boolean TryGetMember(GetMemberBinder binder, out System.Object result)
        {
            result = binder.Name;
            return true;   
        }
    }
}
