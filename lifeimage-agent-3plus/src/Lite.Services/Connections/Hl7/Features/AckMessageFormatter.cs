using Lite.Core.Connections;
using Lite.Core.Guard;
using Lite.Core.Models;
using System;
using System.Text;

namespace Lite.Services.Connections.Hl7
{
    public interface IAckMessageFormatter
    {
        string GetAckMessage(HL7Connection Connection, string controlId);
    }

    public sealed class AckMessageFormatter : IAckMessageFormatter
    {
        public string GetAckMessage(HL7Connection Connection, string controlId)
        {
            Throw.IfNull(Connection);

            var response = new Message();

            var msh = new Segment("MSH");
            msh.Field(2, "^~\\&");
            msh.Field(7, DateTime.Now.ToString("yyyyMMddhhmmssfff"));
            msh.Field(9, Connection.ack);  //  "ACK");
            msh.Field(10, Guid.NewGuid().ToString());
            msh.Field(11, "P");
            msh.Field(12, "2.5.1");
            response.Add(msh);

            var msa = new Segment("MSA");
            msa.Field(1, "AA");
            msa.Field(2, controlId);   //  msg.MessageControlId());
            response.Add(msa);


            // Put response message into an MLLP frame ( <VT> data <FS><CR> )
            //
            var frame = new StringBuilder();
            frame.Append((char)0x0B);
            frame.Append(response.Serialize());
            frame.Append((char)0x0D);
            frame.Append((char)0x1C);
            frame.Append((char)0x0D);

            return frame.ToString();
        }
    }
}
