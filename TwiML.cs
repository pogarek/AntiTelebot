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
        private static string Fulllogfilename = "";
        private static ILogger log;
        private static async Task LogString(string text)
        {
            text = $"{DateTime.UtcNow.ToString("s")} {text}";
            log.LogInformation(text);
            bool written = false;
            do
            {
                try
                {
                    await File.AppendAllTextAsync(Fulllogfilename, text + System.Environment.NewLine);
                    //log.LogInformation("appended");
                    written = true;
                }
                catch
                {
                    //log.LogInformation("exception");
                }
            } while (!written);

        }

        private static async Task<string> GetRandomAnswerByPhrase(string wypowiedz)
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
                            await LogString($"matching phrase1: {wypowiedz}  Keyword: {rekord2}");
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
                                await LogString($"matching phrase2: {wypowiedz_split[i]} {wypowiedz_split[i+1]} {wypowiedz_split[i+2]} Keyword: {rekord2} ");
                                return result;
                            }
                        }
                    }
                }
            }
            return result;
        }
        [FunctionName("TwiML")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log2, ExecutionContext context)
        {
            log = log2;
            string szablonpath = context.FunctionAppDirectory + @"\" + System.Environment.GetEnvironmentVariable("ConversationTemplateFileName");

            Szablon = JsonSerializer.Deserialize<RRozmowy>(File.ReadAllText(szablonpath)).rozmowy;

            //data initialization and collecting inputs from Twilio
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
            Fulllogfilename = @$"{System.IO.Path.GetTempPath()}\{formValues["CallSid"].Trim()}.txt";
            Fulllogfilename = @$"{context.FunctionAppDirectory}\{formValues["CallSid"].Trim()}.txt";
            
            //parsing partial speech result
            if (formValues.ContainsKey("UnstableSpeechResult") & Convert.ToBoolean(System.Environment.GetEnvironmentVariable("PartialSpeechRecognitionEnabled")))
            {
                string partialText = formValues["UnstableSpeechResult"].Trim().ToLower();
                bool halocheck = false;
                await LogString("partial text:" + partialText);
                var PartialHaloCheckWords = Szablon.Where(s2 => s2.wypowiedz.Contains("redirected")).Select(s => s.wypowiedz).FirstOrDefault();
                if (PartialHaloCheckWords.Contains(partialText))
                {
                    halocheck = true;
                }
                /*if (partialText == "halo" | partialText.IndexOf("słychać", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    halocheck = true;
                }*/
                if (partialText.Length >= Convert.ToInt32(System.Environment.GetEnvironmentVariable("PartialSpeechMinimumChars")) | halocheck)
                {
                    if (halocheck)
                    {
                        partialText = "redirected";
                    }
                    string responseMessage2 = await GetRandomAnswerByPhrase(partialText);
                    //podtrzymanie rozmowy
                    if (responseMessage2 == "" & partialText.Length >= Convert.ToInt32(System.Environment.GetEnvironmentVariable("SaySomethingWhenPartialSpeechLongerThanChars")))
                    {
                        responseMessage2 = await GetRandomAnswerByPhrase(System.Environment.GetEnvironmentVariable("KeepConversationKeyWord"));
                    }
                    if (responseMessage2 != "")
                    {
                        responseMessage2 = responseMessage2.Replace("sameurl", furl);
                        string AccountSid = formValues["AccountSid"].Trim();
                        string authToken = System.Environment.GetEnvironmentVariable("TwilioAuthToken");
                        string CallSid = formValues["CallSid"].Trim();
                        await LogString("partial twiml: " + responseMessage2);
                        try
                        {
                            TwilioClient.Init(AccountSid, authToken);
                            var call = Twilio.Rest.Api.V2010.Account.CallResource.Update(
                                twiml: responseMessage2,
                                pathSid: CallSid);
                        }
                        catch { }
                    }
                    string b1 = "";
                }
                //log.LogInformation("emty result");
                return new NoContentResult();
            }
            //the call has ended, let's upload recording to Onedrive
            if (formValues.ContainsKey("RecordingUrl"))
            {
                string caller = "unknown";
                if (formValues.ContainsKey("Caller")) { caller = formValues["Caller"].Trim(); }

                string AccountSid = formValues["AccountSid"].Trim();
                string authToken = System.Environment.GetEnvironmentVariable("TwilioAuthToken");
                string CallSid = formValues["CallSid"].Trim();
                TwilioClient.Init(AccountSid, authToken);
                var call = Twilio.Rest.Api.V2010.Account.CallResource.Fetch(pathSid: CallSid);
                if (caller == "unknown")
                {
                    caller = call.From;
                }
                System.Threading.Thread.Sleep(500);
                /* try {
                    System.IO.File.Copy(Fulllogfilename,@$"{context.FunctionAppDirectory}\{formValues["CallSid"].Trim()}.txt");
                } catch {} */
                await Onedrive.UploadFileToOnedrive(log, formValues["RecordingUrl"].Trim(), caller, Fulllogfilename);
                System.IO.File.Delete(Fulllogfilename);
                return new OkObjectResult("");
            }
            //log.LogInformation(connection.Url);
            string SpeechResult = "";
            //speech recognizion has completed
            if (formValues.ContainsKey("SpeechResult"))
            {
                SpeechResult = formValues["SpeechResult"].Trim();
                await LogString("speachresult: " + SpeechResult);
            }
            //if not let's assume that we are at the beginning of the call
            else
            {
                SpeechResult = "index";
            }
            string responseMessage = "";

            string CallStatus = "";
            //let's check is the call is incoming or in-progress already
            if (formValues.ContainsKey("CallStatus")) { CallStatus = formValues["CallStatus"].Trim(); }
            //if call is already in-progress we can start recording 
            if (SpeechResult == "index" & CallStatus == "in-progress")
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
            //finding reply for spech result or redirecting to record or saying greetings
            responseMessage = await GetRandomAnswerByPhrase(SpeechResult);
            //in case we need to set url . THis must be dynamic becuase url is up to host runnign the code
            responseMessage = responseMessage.Replace("sameurl", furl);
            // if no reply got matched from template, let's say/play something generic
            if (responseMessage == "")
            {
                responseMessage = (await GetRandomAnswerByPhrase(System.Environment.GetEnvironmentVariable("KeepConversationKeyWord"))).Replace("sameurl", furl);
            }
            await LogString(responseMessage);

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
