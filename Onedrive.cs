using System;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Xml;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Graph.Auth;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using PushOver;

namespace AntiTeleBot
{
    public static class Onedrive
    {
        public static string clientId;
        private static IPublicClientApplication pca;
        private static ILogger log;
        private const string Authority = "https://login.microsoftonline.com/consumers";
        private static string[] scopes = new string[] { "User.Read", "Files.Read.All", "Files.ReadWrite.All" };
        static async Task<AuthenticationResult> GetATokenForGraphDeviceCode()
        {
            var accounts = await pca.GetAccountsAsync();
            var result = await AcquireByDeviceCodeAsync(pca);
            return result;
        }
        private static async Task<AuthenticationResult> AcquireByDeviceCodeAsync(IPublicClientApplication pca)
        {
            var result = await pca.AcquireTokenWithDeviceCode(scopes,
                deviceCodeResult =>
                {
                    log.LogInformation(deviceCodeResult.Message);
                    if (System.Environment.GetEnvironmentVariable("PushOverUserId") != "")
                    {
                        PushOverSender.SendPushMessage(System.Environment.GetEnvironmentVariable("PushOverUserId"),
                            System.Environment.GetEnvironmentVariable("PushOverAppTokenId"), "AntiTeleBot", deviceCodeResult.Message);
                    }
                    Console.WriteLine(deviceCodeResult.Message);
                    return Task.FromResult(0);
                }).ExecuteAsync();
            Console.WriteLine(result.Account.Username);
            return result;
        }

        public static async Task UploadFileToOnedrive(ILogger logger, string recordingUrl, string caller)
        {
            log = logger;
            recordingUrl = recordingUrl + ".mp3";
            clientId = System.Environment.GetEnvironmentVariable("OnedriveApplicationCliendId");
            string token = "";
            string FolderName = System.Environment.GetEnvironmentVariable("OnedriveFolderName");
            pca = PublicClientApplicationBuilder
                    .Create(clientId)
                    .WithAuthority(Authority)
                    .WithDefaultRedirectUri()
                    .Build();
            TokenCacheHelper.EnableSerialization(pca.UserTokenCache,log);
            var accounts = await pca.GetAccountsAsync();
            GraphServiceClient graphClient = new GraphServiceClient(authenticationProvider: null);
            try
            {
                graphClient.AuthenticationProvider = new DelegateAuthenticationProvider(async (request) =>
                {
                    token = pca.AcquireTokenSilent(scopes,accounts.FirstOrDefault()).ExecuteAsync().Result.AccessToken;
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    await Task.FromResult(0);
                });
                await graphClient.Me.Drive.Request().GetAsync();
            }
            catch
            {
                token = "";
                log.LogInformation("Onedrive token invalid. Clearing");
            }
            if (token == "")
            {
                var a = await GetATokenForGraphDeviceCode();
                token = a.AccessToken;
                log.LogInformation("NEW TOKEN: " + token);
            }
            graphClient.AuthenticationProvider = new DelegateAuthenticationProvider(async (request) =>
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                await Task.FromResult<object>(null);
            });
            var t = Regex.Split(recordingUrl, "/");
            log.LogInformation(recordingUrl);
            var filename = caller.Replace("+", "") + "_" + DateTime.Now.ToString("s").Replace(":", ".") + ".mp3";
            var h = new HttpClient();
            var hresponse = await h.GetAsync(recordingUrl);
            Stream streamToReadFrom = await hresponse.Content.ReadAsStreamAsync();

            var drive = await graphClient.Me.Drive.Request().GetAsync();
            var items = await graphClient.Me.Drive.Root.Children.Request().GetAsync();
            var folder = items.Where(f => f.Name.ToLower() == FolderName.ToLower()).FirstOrDefault();
            if (folder == null)
            {
                var driveItem = new DriveItem
                {
                    Name = FolderName,
                    Folder = new Folder
                    {
                    }
                };
                await graphClient.Me.Drive.Root.Children.Request().AddAsync(driveItem);
                items = await graphClient.Me.Drive.Root.Children.Request().GetAsync();
                folder = items.Where(f => f.Name.ToLower() == FolderName.ToLower()).FirstOrDefault();
            }
            //using var stream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(@"The contents of the file goes here."));
            //var stream = new System.IO.FileStream(@"C:\_MY_DATA_\GIT_REPOS\a.json", System.IO.FileMode.Open);
            await graphClient.Me.Drive.Items[folder.Id].ItemWithPath(filename).Content.Request().PutAsync<DriveItem>(streamToReadFrom);
            if (System.Environment.GetEnvironmentVariable("PushOverUserId") != "")
                    {
                        PushOverSender.SendPushMessage(System.Environment.GetEnvironmentVariable("PushOverUserId"),
                            System.Environment.GetEnvironmentVariable("PushOverAppTokenId"), "AntiTeleBot", "Nowe nagranie na OneDrive od "+caller);
                    }
        }
    }
}