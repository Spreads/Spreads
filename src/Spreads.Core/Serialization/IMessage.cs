// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Newtonsoft.Json;
using System;

namespace Spreads.Serialization
{
    [JsonConverter(typeof(MessageConverter))]
    public interface IMessage
    {
        [JsonProperty("type")]
        string Type { get; }

        [JsonProperty("id")]
        string Id { get; }
    }

    public class MessageTypeAttribute : Attribute
    {
        public MessageTypeAttribute(string type)
        {
            Type = type;
        }

        public string Type { get; }
    }

    [MessageType("ping")]
    public class PingMessage : IMessage
    {
        public string Type => "ping";
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime DateTimeUtc { get; set; } = DateTime.UtcNow;
    }

    [MessageType("pong")]
    public class PongMessage : IMessage
    {
        public string Type => "pong";
        public string Id { get; set; }
        public DateTime DateTimeUtc { get; set; } = DateTime.UtcNow;
    }
}