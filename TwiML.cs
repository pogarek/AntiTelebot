using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Azure.WebJobs.Extensions.WebPubSub;
using Azure.Messaging.WebPubSub;

using System.Linq;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Utils;
using NAudio.Wave;
using System.Xml;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using RestSharp;
using Twilio.Rest.Api.V2010.Account.Call;
using Twilio.TwiML;
using Twilio;


namespace AntiTeleBot
{
    public static class AntiTeleBot
    {
        private static Dictionary<string, string> szablony = new Dictionary<string, string> {
            {"index",@"<Response><Say language=""pl-PL""></Say><Redirect>sameurl</Redirect></Response>"},

            {"redirected",@"<Response><Say language=""pl-PL"">Tak, słucham</Say><Connect><Stream url=""wss://6927-94-172-120-60.ngrok.io/ws""></Stream></Connect></Response>"},
            {"aa",@"<Response><Say language=""pl-PL"">Tak, słucham</Say><Connect><Stream url=""wss://8e1a-94-172-120-60.ngrok.io/ws""></Stream></Connect></Response>"},
            {"test kasia",@"<Response><Say language=""pl-PL"">Tak, słucham</Say><Connect><Stream url=""wss://8e1a-94-172-120-60.ngrok.io/ws""></Stream></Connect></Response>"}
        };


        public static VoiceResponse GetTwilio(string ConnectionUrl)
        {
            var tresponse = new VoiceResponse();
            Uri myUri = new Uri(ConnectionUrl);
            //string access_token = System.Web.HttpUtility.ParseQueryString(myUri.Query).Get("access_token");
            //string shorturl = @"wss://" + myUri.Host + myUri.AbsolutePath;
            var start = new Twilio.TwiML.Voice.Connect();
            //var stream = new Twilio.TwiML.Voice.Stream(url: ConnectionUrl);
            var stream = new Twilio.TwiML.Voice.Stream(url: "wss://041e-94-172-120-60.ngrok.io/ws");
            //var Param = new Twilio.TwiML.Voice.Parameter("access_token", access_token);
            //stream.Append(Param);
            start.Append(stream);
            //tresponse.Append(Stream());
            //tresponse.Pause(1);
            tresponse.Say("Tak, słucham", language: "pl-PL");
            tresponse.Append(start);
            //tresponse.Pause(1);
            tresponse.Say("abc", language: "pl-PL");
            //tresponse.Record(playBeep: false);

            /*var tresponse = new VoiceResponse();
            tresponse.Say("Tak, słucham", language:"pl-PL");
            tresponse.Gather(input : new[] {Twilio.TwiML.Voice.Gather.InputEnum.Speech}.ToList(), language: "pl-PL"); */
            return tresponse;
        }
        [FunctionName("TwiML")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log,  ExecutionContext context,[WebPubSubConnection()] WebPubSubConnection connection)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string furl = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetEncodedUrl(req);
            var formValues = new Dictionary<string, string>();
            try
            {
                
                var data = (new StreamReader(req.Body)).ReadToEnd();
                formValues = data.Split('&')
                    .Select(value => value.Split('='))
                    .ToDictionary(pair => Uri.UnescapeDataString(pair[0]).Replace("+", " "),
                                  pair => Uri.UnescapeDataString(pair[1]).Replace("+", " "));

                // Perform calculations, API lookups, etc. here
                // Insert spaces between the numbers to help the text-to-speech engine
                var number = formValues["From"]
                    .Replace("+", "")
                    .Aggregate(string.Empty, (c, i) => c + i + ' ')
                    .Trim();
            }
            catch { };
            var dict = req.GetQueryParameterDictionary();
            if (formValues.ContainsKey("RecordingUrl"))
            {
                string caller = "nieznany";
                if (formValues.ContainsKey("Caller")) { caller = formValues["Caller"].Trim(); }

                string AccountSid = formValues["AccountSid"].Trim();
                string authToken = System.Environment.GetEnvironmentVariable("TwilioAuthToken");
                string CallSid = formValues["CallSid"].Trim();
                TwilioClient.Init(AccountSid, authToken); 
                var call = Twilio.Rest.Api.V2010.Account.CallResource.Fetch(pathSid: CallSid);
                if (caller == "nieznany") {
                    caller = call.From;
                }
                await Onedrive.UploadFileToOnedrive(log, formValues["RecordingUrl"].Trim(), caller);
                return new OkObjectResult("");
            }
            //log.LogInformation(connection.Url);
            string SpeechResult = "";
            if (formValues.ContainsKey("SpeechResult"))
            {
                SpeechResult = formValues["SpeechResult"].Trim();
            }
            else
            {
                SpeechResult = "index";
            }
            string responseMessage = "";
            if (szablony.Keys.Contains(SpeechResult)) { responseMessage = szablony[SpeechResult].ToString(); }

            if (SpeechResult == "index" & formValues["CallStatus"].Trim() == "in-progress")
            {
                //var s = req.IsHttps == true ? "https://" : "http://" + req.Host.Value + req.Path.Value + req.QueryString.Value;
                var fullurl = new Uri(furl);
                SpeechResult = "redirected";
                if (szablony.Keys.Contains(SpeechResult)) { responseMessage = szablony[SpeechResult].ToString(); }
                string AccountSid = formValues["AccountSid"].Trim();
                string authToken = System.Environment.GetEnvironmentVariable("TwilioAuthToken");
                string CallSid = formValues["CallSid"].Trim();
                TwilioClient.Init(AccountSid, authToken);
                //log.LogInformation("proba nagrywania");
                var recording = RecordingResource.Create(pathCallSid: CallSid, recordingStatusCallback: fullurl);        
            }
            //string responseMessage = GetTwilio(connection.Url).ToString();
            responseMessage = responseMessage.Replace("sameurl", furl);
            log.LogInformation(responseMessage);

            return new ContentResult { Content = responseMessage, ContentType = "text/xml" };
        }

        [FunctionName("connected")]
        public static async Task<MessageResponse> GetMessages(
            [WebPubSubTrigger(WebPubSubEventType.User, "message")] BinaryData message,
            [WebPubSub()] IAsyncCollector<WebPubSubOperation> operations, ILogger log)
        {

            var message1 = message.ToString();
            MessageMedia messagemedia = null;
            try
            {
                messagemedia = JsonSerializer.Deserialize<MessageMedia>(message1);
                log.LogInformation(messagemedia.@event);
            }
            catch
            {

            }
            log.LogInformation(message1);
            Console.WriteLine(message1);
            await Task.FromResult<object>(null);

            return new MessageResponse
            {
                Message = BinaryData.FromString("aaa"),
                DataType = MessageDataType.Json
            };
        }

        [FunctionName("connect")]
        public static ServiceResponse Connect(
                            [WebPubSubTrigger(WebPubSubEventType.System, "connect")] ConnectionContext connectionContext, ILogger log)
        {
            log.LogInformation("client connected");
            return new ConnectResponse();
        }
        [FunctionName("disconnect")]
        [return: WebPubSub]
        public static WebPubSubOperation Disconnect(
            [WebPubSubTrigger(WebPubSubEventType.System, "disconnected")] ConnectionContext connectionContext, ILogger log)
        {
            log.LogInformation("client disconnected");
            return new SendToAll
            {
                Message = BinaryData.FromString("bye"),
                DataType = MessageDataType.Text
            };
        }

    }
}
