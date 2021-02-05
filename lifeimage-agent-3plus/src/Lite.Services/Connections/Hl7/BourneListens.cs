using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;

namespace Lite.Services.Connections.Hl7
{
    /// <summary>
    /// BourneListens keeps track of tcp listeners used by HL7Connection and future socket-level listeners.
    /// </summary>
    public sealed class BourneListens : TcpListener
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("localaddr")]
        public IPAddress localaddr;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("port")]
        public int port;

        public BourneListens(IPAddress localaddr, int port) :
            base(localaddr, port)
        {
            ExclusiveAddressUse = true;
            this.localaddr = localaddr;
            this.port = port;
        }

        public new void Start()
        {
            if (!base.Active)
            {
                base.Start();
            }
        }

        public new void Stop()
        {
            if (base.Active)
            {
                base.Stop();
            }
        }

        public new bool Active()
        {
            return base.Active;
        }
    }
}
