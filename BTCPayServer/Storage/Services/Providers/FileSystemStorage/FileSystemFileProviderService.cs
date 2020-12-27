using System;
using System.IO;
using System.Threading.Tasks;
using BTCPayServer.Configuration;
using BTCPayServer.Data;
using BTCPayServer.Storage.Models;
using BTCPayServer.Storage.Services.Providers.FileSystemStorage.Configuration;
using Newtonsoft.Json;
using TwentyTwenty.Storage;
using TwentyTwenty.Storage.Local;

namespace BTCPayServer.Storage.Services.Providers.FileSystemStorage
{
    public class
        FileSystemFileProviderService : BaseTwentyTwentyStorageFileProviderServiceBase<FileSystemStorageConfiguration>
    {
        private readonly DataDirectories _datadirs;

        public FileSystemFileProviderService(DataDirectories datadirs)
        {
            _datadirs = datadirs;
        }
        public const string LocalStorageDirectoryName = "LocalStorage";

        public override StorageProvider StorageProvider()
        {
            return Storage.Models.StorageProvider.FileSystem;
        }

        protected override Task<IStorageProvider> GetStorageProvider(FileSystemStorageConfiguration configuration)
        {
            return Task.FromResult<IStorageProvider>(
                new LocalStorageProvider(new DirectoryInfo(_datadirs.StorageDir).FullName));
        }

        public override async Task<string> GetFileUrl(Uri baseUri, StoredFile storedFile, StorageSettings configuration)
        {
            var baseResult = await base.GetFileUrl(baseUri, storedFile, configuration);
            var url = new Uri(baseUri, LocalStorageDirectoryName);
            return baseResult.Replace(new DirectoryInfo(_datadirs.StorageDir).FullName, url.AbsoluteUri,
                StringComparison.InvariantCultureIgnoreCase);
        }

        public override async Task<string> GetTemporaryFileUrl(Uri baseUri, StoredFile storedFile,
            StorageSettings configuration, DateTimeOffset expiry, bool isDownload,
            BlobUrlAccess access = BlobUrlAccess.Read)
        {

            var localFileDescriptor = new TemporaryLocalFileDescriptor()
            {
                Expiry = expiry,
                FileId = storedFile.Id,
                IsDownload = isDownload
            };
            var name = Guid.NewGuid().ToString();
            var fullPath = Path.Combine(_datadirs.TempStorageDir, name);
            if (!File.Exists(fullPath))
            {
                File.Create(fullPath).Dispose();
            }

            await File.WriteAllTextAsync(Path.Combine(_datadirs.TempStorageDir, name), JsonConvert.SerializeObject(localFileDescriptor));

            return new Uri(baseUri, $"{LocalStorageDirectoryName}tmp/{name}{(isDownload ? "?download" : string.Empty)}").AbsoluteUri;
        }
    }
}
