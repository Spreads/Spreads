using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Serialization {




    public static class BinarySerializer {

        //


        /// <summary>
        /// Register custom binary convertor for a type.
        /// </summary>
        public static void RegisterConvertor<T>(IBinaryConverter<T> convertor) {
            TypeHelper<T>.RegisterConvertor(convertor);
        }


        public static int Serialize<T>(T value, DirectBuffer target) {
            target.Write<T>(0, value);


            throw new NotImplementedException();
        }


    }

    



}
