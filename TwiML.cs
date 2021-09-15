using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
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
        private static List<Rozmowy> Szablon;

        private static string GetRandomAnswerByPhrase(string wypowiedz)
        {
            string result = "";
            var wypowiedz_split = wypowiedz.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (wypowiedz.Length >= 5)
            {
                foreach (var rekord in Szablon)
                {
                    foreach (var rekord2 in rekord.wypowiedz)
                    {
                        if (rekord2.IndexOf(wypowiedz, StringComparison.InvariantCultureIgnoreCase) >= 0 | wypowiedz.IndexOf(rekord2, StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            int RandomIndex = (new Random()).Next(0, rekord.odpowiedz.Count);
                            result = rekord.odpowiedz[RandomIndex];
                            return result;
                        }
                    }
                }
                foreach (var rekord in Szablon)
                {
                    foreach (var rekord2 in rekord.wypowiedz)
                    {
                        for (int i = 0; i < wypowiedz_split.Count() - 2; i++)
                        {
                            if ((wypowiedz_split[i] + " " + wypowiedz_split[i + 1] + " " + wypowiedz_split[i + 2]).IndexOf(rekord2, StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                int RandomIndex = (new Random()).Next(0, rekord.odpowiedz.Count);
                                result = rekord.odpowiedz[RandomIndex];
                                return result;
                            }
                        }
                    }
                }
            }
            return result;
        }
        [FunctionName("TwiML")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log, ExecutionContext context)
        {
            string szablonpath = context.FunctionAppDirectory + @"\" + System.Environment.GetEnvironmentVariable("ConversationTemplateFileName");
            Szablon = JsonSerializer.Deserialize<RRozmowy>(File.ReadAllText(szablonpath)).rozmowy;

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
            if (formValues.ContainsKey("UnstableSpeechResult") & Convert.ToBoolean(System.Environment.GetEnvironmentVariable("PartialSpeechRecognitionEnabled")))
            {
                string partialText = formValues["UnstableSpeechResult"].Trim();
                bool halocheck = false;
                if (partialText == "halo" | partialText.IndexOf("słychać", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    halocheck = true;
                }
                if (partialText.Length >= Convert.ToInt32(System.Environment.GetEnvironmentVariable("PartialSpeechMinimumChars")) | halocheck)
                {
                    if (halocheck)
                    {
                        partialText = "redirected";
                    }
                    string responseMessage2 = GetRandomAnswerByPhrase(partialText);
                    if (responseMessage2 != "")
                    {
                        responseMessage2 = responseMessage2.Replace("sameurl", furl);
                        string AccountSid = formValues["AccountSid"].Trim();
                        string authToken = System.Environment.GetEnvironmentVariable("TwilioAuthToken");
                        string CallSid = formValues["CallSid"].Trim();
                        log.LogInformation("partial twiml: "+ responseMessage2);
                        try
                        {
                            TwilioClient.Init(AccountSid, authToken);
                            var call = Twilio.Rest.Api.V2010.Account.CallResource.Update(
                                twiml: responseMessage2,
                                pathSid: CallSid);
                        }
                        catch { }
                        string b1 = "";
                    }
                }
                return new EmptyResult();
            }
            if (formValues.ContainsKey("RecordingUrl"))
            {
                string caller = "nieznany";
                if (formValues.ContainsKey("Caller")) { caller = formValues["Caller"].Trim(); }

                string AccountSid = formValues["AccountSid"].Trim();
                string authToken = System.Environment.GetEnvironmentVariable("TwilioAuthToken");
                string CallSid = formValues["CallSid"].Trim();
                TwilioClient.Init(AccountSid, authToken);
                var call = Twilio.Rest.Api.V2010.Account.CallResource.Fetch(pathSid: CallSid);
                if (caller == "nieznany")
                {
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
                log.LogInformation("speachresult: " + SpeechResult);
            }
            else
            {
                SpeechResult = "index";
            }
            string responseMessage = "";


            string CallStatus = "";
            string msg = "";
            if (formValues.ContainsKey("CallStatus")) { CallStatus = formValues["CallStatus"].Trim(); }
            if (formValues.ContainsKey("msg")) { msg = formValues["msg"].Trim(); }
            if (msg != "") { SpeechResult = ""; }
            if (SpeechResult == "index" & CallStatus == "in-progress" & msg == "")
            {
                //var s = req.IsHttps == true ? "https://" : "http://" + req.Host.Value + req.Path.Value + req.QueryString.Value;
                var fullurl = new Uri(furl);
                SpeechResult = "redirected";
                string AccountSid = formValues["AccountSid"].Trim();
                string authToken = System.Environment.GetEnvironmentVariable("TwilioAuthToken");
                string CallSid = formValues["CallSid"].Trim();
                TwilioClient.Init(AccountSid, authToken);
                //log.LogInformation("proba nagrywania");
                var recording = RecordingResource.Create(pathCallSid: CallSid, recordingStatusCallback: fullurl);
            }
            responseMessage = GetRandomAnswerByPhrase(SpeechResult);
            responseMessage = responseMessage.Replace("sameurl", furl);
            if (responseMessage == "")
            {
                responseMessage = GetRandomAnswerByPhrase("podtrzymaj").Replace("sameurl", furl);
            }
            log.LogInformation(responseMessage);

            return new ContentResult { Content = responseMessage, ContentType = "text/xml" };
        }
    }
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class Rozmowy
    {
        public List<string> wypowiedz { get; set; }
        public List<string> odpowiedz { get; set; }
    }

    public class RRozmowy
    {
        public List<Rozmowy> rozmowy { get; set; }
    }


}
