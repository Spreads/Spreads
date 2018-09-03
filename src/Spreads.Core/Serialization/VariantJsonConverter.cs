//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using Spreads.DataTypes;
//using System;
//using System.Diagnostics;

//namespace Spreads.Serialization
//{
//    public class VariantJsonConverter : JsonConverter
//    {
//        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//        {
//            var variant = (Variant)value;
//            var typeCode = variant.TypeEnum;

//            writer.WriteStartArray();

//            writer.WriteValue((byte)typeCode);

//            if (typeCode == TypeEnum.Array)
//            {
//                // TODO for byte[] JSON.NET will use binary, not array
//                var subTypeCode = variant.ElementTypeEnum;
//                writer.WriteValue((byte)subTypeCode);

//                if (subTypeCode == TypeEnum.FixedBinary)
//                {
//                    // array of fixed binaries - 4 elements
//                    var size = variant.ElementByteSize;
//                    writer.WriteValue(size);
//                    throw new NotImplementedException("Need special handling of fixed binary values");
//                }
//            }

//            if (typeCode == TypeEnum.FixedBinary)
//            {
//                var size = variant.ByteSize;
//                writer.WriteValue(size);
//                throw new NotImplementedException("Need special handling of fixed binary values");
//            }

//            if (typeCode == TypeEnum.Object)
//            {
//                var subTypeCode = variant.ElementTypeEnum;
//                writer.WriteValue((byte)subTypeCode);
//            }

//            if (typeCode != TypeEnum.None)
//            {
//                var obj = variant.ToObject();
//                var t = JToken.FromObject(obj, serializer);
//                t.WriteTo(writer);
//            }

//            writer.WriteEndArray();
//        }

//        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//        {
//            if (reader.TokenType == JsonToken.Null)
//            {
//                return Variant.FromObject(null);
//            }
//            if (reader.TokenType != JsonToken.StartArray)
//            {
//                throw new Exception("Invalid JSON for Variant type");
//            }

//            var codeValue = reader.ReadAsInt32();
//            Debug.Assert(codeValue != null, "codeValue != null");
//            var typeCode = (TypeEnum)(byte)codeValue;
//            TypeEnum subTypeCode = TypeEnum.None;
//            if (typeCode == TypeEnum.Array)
//            {
//                var subTypeCodeValue = reader.ReadAsInt32();
//                Debug.Assert(subTypeCodeValue != null, "subTypeCodeValue != null");
//                subTypeCode = (TypeEnum)(byte)subTypeCodeValue;
//                if (subTypeCode == TypeEnum.FixedBinary)
//                {
//                    throw new NotImplementedException("Need special handling of fixed binary values");
//                }
//            }

//            if (typeCode == TypeEnum.FixedBinary)
//            {
//                //var size = reader.ReadAsInt32();
//                throw new NotImplementedException("Need special handling of fixed binary values");
//            }

//            object obj;
//            if (typeCode == TypeEnum.Object)
//            {
//                var objectTypeCodeValue = reader.ReadAsInt32();
//                Debug.Assert(objectTypeCodeValue != null, "objectTypeCodeValue != null");
//                var objectTypeCode = (byte)objectTypeCodeValue;
//                var type = KnownTypeAttribute.GetType(objectTypeCode);
//                if (!reader.Read()) throw new Exception("Cannot read JSON");
//                obj = serializer.Deserialize(reader, type);
//            }
//            else if (typeCode != TypeEnum.None)
//            {
//                if (!reader.Read()) throw new Exception("Cannot read JSON");
//                var type = VariantHelper.GetType(typeCode, subTypeCode);
//                obj = serializer.Deserialize(reader, type);
//            }
//            else
//            {
//                obj = Variant.Create(null);
//            }

//            if (!reader.Read()) throw new Exception("Cannot read JSON");
//            Trace.Assert(reader.TokenType == JsonToken.EndArray);
//            return Variant.FromObject(obj);
//        }

//        public override bool CanConvert(Type objectType)
//        {
//            // TODO actually we can do any type
//            return objectType == typeof(Variant);
//        }
//    }
//}