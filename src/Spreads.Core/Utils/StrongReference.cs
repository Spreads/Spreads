using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Utils {
    public sealed class StrongReference<T> where T : class {
        public StrongReference(T target) {
            Target = target;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetTarget(out T target) {
            // Call the worker method that has more performant but less user friendly signature.
            T o = this.Target;
            target = o;
            return o != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTarget(T target) {
            this.Target = target;
        }

        // This is property for better debugging experience (VS debugger shows values of properties when you hover over the variables)
        private T Target {
            get; set;
        }

    }
}
