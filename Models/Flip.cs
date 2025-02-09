using System.Runtime.Serialization;

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