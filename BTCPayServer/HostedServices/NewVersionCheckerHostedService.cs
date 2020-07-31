using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Services;
using BTCPayServer.Services.Notifications;
using BTCPayServer.Services.Notifications.Blobs;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.HostedServices
{
    public class NewVersionCheckerHostedService : BaseAsyncService
    {
        private readonly SettingsRepository _settingsRepository;
        private readonly BTCPayServerEnvironment _env;
        private readonly NotificationSender _notificationSender;
        private readonly IVersionFetcher _versionFetcher;

        public NewVersionCheckerHostedService(SettingsRepository settingsRepository, BTCPayServerEnvironment env,
            NotificationSender notificationSender, IVersionFetcher versionFetcher)
        {
            _settingsRepository = settingsRepository;
            _env = env;
            _notificationSender = notificationSender;
            _versionFetcher = versionFetcher;
        }

        internal override Task[] InitializeTasks()
        {
            return new Task[] { CreateLoopTask(LoopVersionCheck) };
        }

        protected async Task LoopVersionCheck()
        {
            await ProcessVersionCheck();
            await Task.Delay(TimeSpan.FromDays(1), Cancellation);
        }

        public async Task ProcessVersionCheck()
        {
            var policies = await _settingsRepository.GetSettingAsync<PoliciesSettings>() ?? new PoliciesSettings();
            if (policies.CheckForNewVersions)
            {
                var tag = await _versionFetcher.Fetch(Cancellation);
                if (tag != null && tag != _env.Version)
                {
                    var dh = await _settingsRepository.GetSettingAsync<NewVersionCheckerDataHolder>() ?? new NewVersionCheckerDataHolder();
                    if (dh.LastVersion != tag)
                    {
                        await _notificationSender.SendNotification(new AdminScope(), new NewVersionNotification(tag));

                        dh.LastVersion = tag;
                        await _settingsRepository.UpdateSetting(dh);
                    }
                }
            }
        }
    }

    public class NewVersionCheckerDataHolder
    {
        public string LastVersion { get; set; }
    }

    public interface IVersionFetcher
    {
        Task<string> Fetch(CancellationToken cancellation);
    }

    public class GithubVersionFetcher : IVersionFetcher, IDisposable
    {
        private readonly HttpClient _httpClient;
        public GithubVersionFetcher(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient(nameof(GithubVersionFetcher));
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BTCPayServer/NewVersionChecker");
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public async Task<string> Fetch(CancellationToken cancellation)
        {
            const string url = "https://api.github.com/repos/btcpayserver/btcpayserver/releases/latest";
            var resp = await _httpClient.GetAsync(url, cancellation);

            if (resp.IsSuccessStatusCode)
            {
                var jobj = await resp.Content.ReadAsAsync<JObject>(cancellation);
                var tag = jobj["name"].ToString();
                return tag;
            }

            return null;
        }
    }
}
