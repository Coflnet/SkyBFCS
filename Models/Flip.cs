
using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.BFCS.Models
{
    [DataContract]
    public class ForwardedFlip
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }
        [DataMember(Name = "worth")]
        public long Worth { get; set; }
    }
}