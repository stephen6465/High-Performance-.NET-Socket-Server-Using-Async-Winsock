using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_MagLink
{
    class AckMessages
    {
        public String ack;
        private StringBuilder t;
        private String _messageId;
        IEFMagLinkRepository _repository;
        public AckMessages(String message , IEFMagLinkRepository repository)
        {
            _repository = repository;
            this.setAck(message);
            //string ack1 = await this.makeAck(message);
                           
        }

        public async void setAck(String message)
        {
            //String t =  
            this.ack = await makeAck(message);
        }

        public async Task<String> makeAck(String message)
        {

            DateTime date = DateTime.Now;
            string format = "yyyyMMddHHmmss";
            StringBuilder hl7DateStr = new StringBuilder(date.ToString(format));
            var AckSegments1 = _repository.GetAckMessageAsync();
            var AckSegments = await AckSegments1;

            _messageId = getMessageId(message, 9);
            StringBuilder ackSegmentString = new StringBuilder();

            foreach (var ackSegment in AckSegments.Where(a=> a.Segment == "MSH-A").OrderBy(s=> s.Sequence))
            {
                
                    switch (ackSegment.ContentOut.ToUpper().Trim())
                    {
                        case "HL7DATE_TIME":
                            ackSegmentString.Append(hl7DateStr).Append("|");
                            break;
                        case "MESSAGEID":
                            ackSegmentString.Append(_messageId).Append("|");
                            break;
                        default:
                            ackSegmentString.Append(ackSegment.ContentOut.Trim()).Append("|");
                            break;
                    }

            }

            ackSegmentString.Append((char) 13);
           
            foreach (var ackSegment in AckSegments.Where(a => a.Segment == "MSA").OrderBy(s => s.Sequence))
            {
                ackSegmentString.Append(ackSegment.ContentOut).Append("|");

            }

            //String tempString = message.Substring(1, message.IndexOf((char)13));
            //String firstPartAck = @"MSH|^~\&|MagView||||";
            ////20140510233808||ACK|";
            //String datetimesec = hl7DateStr.ToString() + "||ACK|";
            //String secondPartAck = "|T|2.x||||||||";
            //String thirdPartAck = "MSA|AA||||||";
            //t = new StringBuilder(firstPartAck);
            //
            //t.Append(datetimesec);
            //t.Append(_messageId);
            //t.Append(secondPartAck);
            //t.Append((char)13);
            //t.Append(thirdPartAck);
            //HL7 hl7 = new HL7();
            //String tempString2 = "";
            
            ack = HL7.CreateMLLPMessage(ackSegmentString.ToString());
            return ack;
        }

        // Fix this to enable the ability to chang akc's based on config
        public String buildAckFromDB()
        {

            return "";
        }

        // Finish making the getmessageID function
        public String getMessageId(String message, int fieldNum)
        {
            Message t = new Message(message);
            String tempString = t.getElement("MSH", fieldNum);

            return tempString;
        }

        public String getMessageId()
        {
            return this._messageId;

        }

    }

}
