
using System;
using System.Collections;

namespace AntiTeleBot
{
    public class Media
    {
        public string track { get; set; }
        public string chunk { get; set; }
        public string timestamp { get; set; }
        public string payload { get; set; }
    }

    public class MessageMedia
    {
        public string @event { get; set; }
        public string sequenceNumber { get; set; }
        public Media media { get; set; }
        public string streamSid { get; set; }
    }

}