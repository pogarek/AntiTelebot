using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading.Tasks;
//using PushoverClient;
using System.Net;
using System.Collections.Specialized;

namespace PushOver
{
    class PushOverSender
    {

        public static void SendPushMessage(string UserId, string AppTokenId, string MessageTitle, string MessageText)
        {
            var parameters = new NameValueCollection {
                { "token", AppTokenId },
                { "user", UserId },
                { "message", MessageText},
                { "title", MessageTitle }
};

            var client = new WebClient();
            client.UploadValues("https://api.pushover.net/1/messages.json", parameters);

        }
    }
}