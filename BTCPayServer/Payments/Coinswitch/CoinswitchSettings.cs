namespace BTCPayServer.Payments.CoinSwitch
{
    public class CoinSwitchSettings
    {
        public string MerchantId { get; set; }

        public bool Enabled { get; set; }
        
        public bool IsConfigured()
        {
            return
                !string.IsNullOrEmpty(MerchantId);
        }
    }
}
