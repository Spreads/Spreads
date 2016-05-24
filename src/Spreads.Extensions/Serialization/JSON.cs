using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Serialization;
using Spreads.Collections;

namespace Spreads.Serialization {



    // TODO! support series by converting them to SortedMap on serializatio and by deserializing as SM and casting to series
    internal class SpreadsContractResolver : DefaultContractResolver {
        protected override sealed JsonConverter ResolveContractConverter(Type ty) {

            // Serialize maps directly
            if (ty.IsGenericType && ty.GetGenericTypeDefinition() == typeof(SortedMap<,>)) {
                return new SpreadsJsonConverter();
            }

            // Other than maps, we are cool at arrays. For other types JSON.NET is cool
            if (!ty.IsArray) return base.ResolveContractConverter(ty);

            var elTy = ty.GetElementType();
            if (BlittableHelper.IsBlittable(elTy)
                || elTy == typeof(DateTimeOffset)
                || elTy == typeof(DateTime)) {
                return new SpreadsJsonConverter();
            }
            return base.ResolveContractConverter(ty);
        }
    }


    internal class SpreadsJsonConverter : JsonConverter {

        public override bool CanConvert(Type ty) {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type ty,
            object existingValue, JsonSerializer serializer) {



            var bytes = reader.Value as byte[];

            if (ty.IsGenericType && ty.GetGenericTypeDefinition() == typeof(SortedMap<,>)) {
                // will dispatch to Spreads types
                return Serializer.Deserialize(bytes, ty);
            }

            // Other than maps, we are cool at arrays. For other types JSON.NET is cool
            if (!ty.IsArray) {
                return serializer.Deserialize(reader, ty);
            }

            var elTy = ty.GetElementType();
            if (BlittableHelper.IsBlittable(elTy)
                //|| ty == typeof(DateTimeOffset)
                || ty == typeof(DateTime)) {
                //if (bytes == null) {
                //	return null;
                //} else if (bytes.Length == 0) {
                //	return Array.CreateInstance(ty.GetElementType(), 0);
                //}
                // will dispatch to Spreads types
                return Serializer.Deserialize(bytes, ty);
            }

            //var bytes = reader.Value as byte[];
            return serializer.Deserialize(reader, ty);


        }

        public override void WriteJson(JsonWriter writer, object value,
            JsonSerializer serializer) {

            var ty = value.GetType();

            //if (ty.IsGenericType && ty.GetGenericTypeDefinition() == typeof(SortedMap<,>)) {
            //	// will dispatch to Spreads types
            //	var bytes = Serializer.Serialize(value); // TODO resolvedCall = true
            //	writer.WriteValue(bytes);
            //}

            //// Other than maps, we are cool at arrays. For other types JSON.NET is cool
            //if (!ty.IsArray) {
            //	serializer.Serialize(writer, value);
            //}

            if (ty.IsArray) {
                var elTy = ty.GetElementType();
                if (BlittableHelper.IsBlittable(elTy)
                    //|| ty == typeof(DateTimeOffset)
                    || ty == typeof(DateTime)) {
                    //var arr = (Array)value;
                    //if (arr.Length == 0) {
                    //	writer.WriteValue(EmptyArray<byte>.Instance);
                    //} else {
                    // will dispatch to Spreads types
                    var bytes = Serializer.Serialize(value); // TODO resolvedCall = true
                    writer.WriteValue(bytes);
                        //}
                    } else {
                    serializer.Serialize(writer, value);
                }
            } else {
                // will dispatch to Spreads types
                var bytes = Serializer.Serialize(value); // TODO resolvedCall = true
                writer.WriteValue(bytes);
            }
        }
    }


    /// <summary>
    /// JSON.NET serializer with custom converters for special types
    /// </summary>
    internal class SpreadsJsonSerializer : ISerializer {
        JsonSerializer _serializer;
        public SpreadsJsonSerializer() {
            _serializer = new JsonSerializer();
            _serializer.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            _serializer.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            _serializer.ContractResolver = new SpreadsContractResolver();
        }

        public object Deserialize(byte[] bytes, Type type) {
            MemoryStream ms = new MemoryStream(bytes);
            using (BsonReader reader = new BsonReader(ms)) {
                return _serializer.Deserialize(reader, type);
            }
        }

        public T Deserialize<T>(byte[] bytes) {
            MemoryStream ms = new MemoryStream(bytes);
            using (BsonReader reader = new BsonReader(ms, typeof(T).IsArray, DateTimeKind.Unspecified)) {
                return _serializer.Deserialize<T>(reader);
            }
        }

        [Obsolete]
        public T DeserializeFromJson<T>(string json) {
            using (var reader = new StringReader(json)) {
                using (var jsonReader = new JsonTextReader(reader)) {
                    return _serializer.Deserialize<T>(jsonReader);
                }
            }
        }

        public byte[] Serialize<T>(T value) {
            MemoryStream ms = new MemoryStream();
            using (BsonWriter writer = new BsonWriter(ms)) {
                _serializer.Serialize(writer, value);
            }
            return ms.ToArray();
        }

        public byte[] Serialize(object obj) {
            MemoryStream ms = new MemoryStream();
            using (BsonWriter writer = new BsonWriter(ms)) {
                _serializer.Serialize(writer, obj);
            }
            return ms.ToArray();  //Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
        }

        [Obsolete]
        public string SerializeToJson<T>(T value) {
            using (var writer = new StringWriter()) {
                using (var jsonWriter = new JsonTextWriter(writer)) {
                    _serializer.Serialize(jsonWriter, value);
                    return writer.ToString();
                }
            }
        }
    }
}
