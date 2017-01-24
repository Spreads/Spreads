// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Spreads.Serialization {

    /// <summary>
    /// Limits enum serialization only to defined values
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SafeEnumConverter<T> : StringEnumConverter {

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            var isDef = Enum.IsDefined(typeof(T), value);
            if (!isDef) {
                value = null;
            }
            base.WriteJson(writer, value, serializer);
        }
    }

    /// <summary>
    /// Serialize as string with ToString()
    /// </summary>
    public class ToStringConverter<T> : JsonConverter {

        public override bool CanConvert(Type objectType) {
            return true;
        }

        public override object ReadJson(JsonReader reader,
                                        Type objectType,
                                         object existingValue,
                                         JsonSerializer serializer) {
            var t = JToken.Load(reader);
            T target = t.Value<T>();
            return target;
        }

        public override void WriteJson(JsonWriter writer,
                                       object value,
                                       JsonSerializer serializer) {
            var t = JToken.FromObject(value.ToString());
            t.WriteTo(writer);
        }
    }

    /// <summary>
    /// Serialize Decimal to string without trailing zeros
    /// </summary>
    public class DecimalG29ToStringConverter : JsonConverter {

        public override bool CanConvert(Type objectType) {
            return objectType.Equals(typeof(decimal));
        }

        public override object ReadJson(JsonReader reader,
                                        Type objectType,
                                         object existingValue,
                                         JsonSerializer serializer) {
            var t = JToken.Load(reader);
            return t.Value<decimal>();
        }

        public override void WriteJson(JsonWriter writer,
                                       object value,
                                       JsonSerializer serializer) {
            decimal d = (decimal)value;
            var t = JToken.FromObject(d.ToString("G29"));
            t.WriteTo(writer);
        }
    }

    /// <summary>
    /// Convert DateTime to HHMMSS
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class HHMMSSDateTimeConverter : JsonConverter {

        public override bool CanConvert(Type objectType) {
            return true;
        }

        public override object ReadJson(JsonReader reader,
                                        Type objectType,
                                         object existingValue,
                                         JsonSerializer serializer) {
            var t = JToken.Load(reader);
            var target = t.Value<string>();
            if (target == null) return null;
            var hh = int.Parse(target.Substring(0, 2));
            var mm = int.Parse(target.Substring(2, 2));
            var ss = int.Parse(target.Substring(4, 2));
            var now = DateTime.Now;
            var dt = new DateTime(now.Year, now.Month, now.Day, hh, mm, ss);
            return dt;
        }

        public override void WriteJson(JsonWriter writer,
                                       object value,
                                       JsonSerializer serializer) {
            var t = JToken.FromObject(((DateTime)value).ToString("HHmmss"));
            t.WriteTo(writer);
        }
    }

    public class MessageConverter : JsonCreationConverter<IMessage> {
#if NET451x
        // TODO(?,low) reflection to cache types by names and use activator create instance
        // http://mattgabriel.co.uk/2016/02/10/object-creation-using-lambda-expression/
        static MessageConverter() {
            var assemblied = AppDomain.CurrentDomain
                .GetAssemblies();
            var types = assemblied
                    .SelectMany(s => {
                        try {
                            return s.GetTypes();
                        } catch {
                            return new Type[] { };
                        }
                    })
                    .Where(p => {
                        try {
                            return typeof(IMessage).IsAssignableFrom(p)
                                   && p.GetTypeInfo().GetCustomAttribute<MessageTypeAttribute>() != null
                                   && p.GetTypeInfo().IsClass && !p.GetTypeInfo().IsAbstract;
                        } catch {
                            return false;
                        }
                    }).ToList();
            foreach (var t in types) {
                var attr = t.GetTypeInfo().GetCustomAttribute<MessageTypeAttribute>();
                KnownTypes[attr.Type] = t;
            }
        }
#endif

        private static readonly ConcurrentDictionary<string, Type> KnownTypes = new ConcurrentDictionary<string, Type>();

        public static void RegisterType<T>(string type) where T : IMessage {
            if (!KnownTypes.TryAdd(type, typeof(T))) {
                throw new ArgumentException($"Type {type} already registered");
            }
        }

        // we learn object type from correlation id and a type stored in responses dictionary
        // ReSharper disable once RedundantAssignment
        protected override IMessage Create(Type objectType, JObject jObject) {
            if (FieldExists("type", jObject)) {
                // without id we have an event
                var type = jObject.GetValue("type").Value<string>();
                switch (type) {
                    case "ping":
                        return new PingMessage();

                    case "pong":
                        return new PongMessage();

                    default:
                        Type t;
                        if (KnownTypes.TryGetValue(type, out t)) {
                            var instance = Activator.CreateInstance(t);
                            return (IMessage)instance;
                        }
                        throw new InvalidOperationException("Unknown message type");
                }
            }
            throw new ArgumentException("Bad message format: no type field");
        }

        private static bool FieldExists(string fieldName, JObject jObject) {
            return jObject[fieldName] != null;
        }
    }

    public abstract class JsonCreationConverter<T> : JsonConverter {

        /// <summary>
        /// Create an instance of objectType, based properties in the JSON object
        /// </summary>
        /// <param name="objectType">type of object expected</param>
        /// <param name="jObject">
        /// contents of JSON object that will be deserialized
        /// </param>
        /// <returns></returns>
        protected abstract T Create(Type objectType, JObject jObject);

        public override bool CanConvert(Type objectType) {
            return typeof(T).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader,
                                        Type objectType,
                                         object existingValue,
                                         JsonSerializer serializer) {
            // Load JObject from stream
            JObject jObject = JObject.Load(reader);

            // Create target object based on JObject
            T target = Create(objectType, jObject);

            // Populate the object properties
            serializer.Populate(jObject.CreateReader(), target);

            return target;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer,
                                       object value,
                                       JsonSerializer serializer) {
            //writer.WriteValue(value);
            throw new NotSupportedException();
        }
    }
}
