using System.IO;
using System.Security.Cryptography;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
/* using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host; */
using Azure.Storage.Blobs;
using System.Linq;

static class TokenCacheHelper
{
    private static BlobContainerClient bcc;
    public static void EnableSerialization(ITokenCache tokenCache, ILogger logger)
    {

        log = logger;
        string cstring = System.Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        string containerName = System.Environment.GetEnvironmentVariable("TokenCacheStorageContainer");
        try { bcc = new BlobContainerClient(cstring, containerName); } catch { }
        if (!bcc.Exists())
        {
            bcc = (new BlobServiceClient(cstring)).CreateBlobContainerAsync(containerName).GetAwaiter().GetResult();
        }
        tokenCache.SetBeforeAccess(BeforeAccessNotification);
        tokenCache.SetAfterAccess(AfterAccessNotification);
    }

    /// <summary>
    /// Path to the token cache. Note that this could be something different for instance for MSIX applications:
    /// private static readonly string CacheFilePath =
    /// $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\{AppName}\msalcache.bin";
    /// </summary>
    public static readonly string CacheFilePath = System.IO.Path.GetTempPath() + "msalcache.bin3";
    private static ILogger log;

    private static readonly object FileLock = new object();
    private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        lock (FileLock)
        {
            var blob = bcc.GetBlobClient("msalcache.bin3");
            if (blob.Exists())
            {
                var stream = new MemoryStream();
                blob.DownloadToAsync(stream).GetAwaiter().GetResult();
                args.TokenCache.DeserializeMsalV3(stream.ToArray());
            }
        }
    }

    private static void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        // if the access operation resulted in a cache update
        if (args.HasStateChanged)
        {
            lock (FileLock)
            {
                // reflect changesgs in the persistent store
                (bcc.GetBlobClient("msalcache.bin3")).UploadAsync(new MemoryStream(args.TokenCache.SerializeMsalV3()),true).GetAwaiter().GetResult();
            }
        }
    }
}