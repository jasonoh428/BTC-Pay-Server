#if ALTCOINS
using BTCPayServer.Contracts;

namespace BTCPayServer.Services.Altcoins.Ethereum
{
    public class EthereumStoreNavExtension: IStoreNavExtension
    {
        public string Partial { get; } = "Ethereum/StoreNavEthereumExtension";
    }
}
#endif
