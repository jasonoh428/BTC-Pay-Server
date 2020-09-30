using System;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Services.Shopify.Models
{
    public class ShopifySettings
    {
        [Display(Name = "Shop Name")]
        public string ShopName { get; set; }
        public string ApiKey { get; set; }
        public string Password { get; set; }

        public bool CredentialsPopulated()
        {
            return
                !string.IsNullOrWhiteSpace(ShopName) &&
                !string.IsNullOrWhiteSpace(ApiKey) &&
                !string.IsNullOrWhiteSpace(Password);
        }
        public DateTimeOffset? IntegratedAt { get; set; }
    }
}
