﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Rating;
using BTCPayServer.Services.Rates;

namespace BTCPayServer.Tests.Mocks
{
    public class MockRateProvider : IRateProvider
    {
        public ExchangeRates ExchangeRates { get; set; } = new ExchangeRates();
        public Task<ExchangeRates> GetRatesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ExchangeRates);
        }
    }
}
