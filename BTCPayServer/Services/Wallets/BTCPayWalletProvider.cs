﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Services.Wallets
{
    public class BTCPayWalletProvider
    {
        private ExplorerClientProvider _Client;
        BTCPayNetworkProvider _NetworkProvider;
        IOptions<MemoryCacheOptions> _Options;
        public BTCPayWalletProvider(ExplorerClientProvider client,
                                    IOptions<MemoryCacheOptions> memoryCacheOption,
                                    BTCPayNetworkProvider networkProvider)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            _Client = client;
            _NetworkProvider = networkProvider;
            _Options = memoryCacheOption;
        }

        public BTCPayWallet GetWallet(BTCPayNetwork network)
        {
            if (network == null)
                throw new ArgumentNullException(nameof(network));
            return GetWallet(network.CryptoCode);
        }
        public BTCPayWallet GetWallet(string cryptoCode)
        {
            if (cryptoCode == null)
                throw new ArgumentNullException(nameof(cryptoCode));
            var network = _NetworkProvider.GetNetwork(cryptoCode);
            var client = _Client.GetExplorerClient(cryptoCode);
            if (network == null || client == null)
                return null;
            return new BTCPayWallet(client, new MemoryCache(_Options), network);
        }

        public bool IsAvailable(BTCPayNetwork network)
        {
            return _Client.IsAvailable(network);
        }
    }
}
