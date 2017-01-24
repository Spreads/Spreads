// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters;

namespace Spreads.Serialization {

    /// <summary>
    /// Extensions for JSON.NET
    /// </summary>
    public static class JsonExtensions {
        private static readonly JsonSerializer Serializer = new JsonSerializer();
        private static JsonSerializer _messageSerializer;
        private static MessageConverter _messageConverter;

        static JsonExtensions() {
        }

        /// <summary>
        ///
        /// </summary>
        public static T FromJson<T>(this string json) {
            var obj = JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.None
            });
            return obj;
        }

        internal static IMessage FromJson(this string json) {
            var obj = JsonConvert.DeserializeObject<IMessage>(json, _messageConverter);
            return obj;
        }

        /// <summary>
        ///
        /// </summary>
        public static object FromJson(this string json, Type type) {
            var obj = JsonConvert.DeserializeObject(json, type, new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.None,
                // NB this is important for correctness and performance
                // Transaction could have many null properties
                NullValueHandling = NullValueHandling.Ignore
            });
            return obj;
        }

        /// <summary>
        ///
        /// </summary>
        public static string ToJson<T>(this T obj) {
            var message = JsonConvert.SerializeObject(obj, Formatting.None,
                new JsonSerializerSettings {
                    TypeNameHandling = TypeNameHandling.None, // Objects
                    TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
                    // NB this is important for correctness and performance
                    // Transaction could have many null properties
                    NullValueHandling = NullValueHandling.Ignore
                });
            return message;
        }

        /// <summary>
        /// Returns indented JSON
        /// </summary>
        public static string ToJsonFormatted<T>(this T obj) {
            var message = JsonConvert.SerializeObject(obj, Formatting.Indented,
                new JsonSerializerSettings {
                    TypeNameHandling = TypeNameHandling.None, // Objects
                    TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
                    // NB this is important for correctness and performance
                    // Transaction could have many null properties
                    NullValueHandling = NullValueHandling.Ignore
                });
            return message;
        }

        public static MemoryStream WriteJson<T>(this T value) {
            return BinarySerializer.Json.Serialize<T>(value);
        }

        public static T ReadJson<T>(this MemoryStream stream) {
            return BinarySerializer.Json.Deserialize<T>(stream);
        }
    }
}
