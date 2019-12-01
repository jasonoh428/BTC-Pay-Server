using System;
using System.Linq;
using NBitcoin;

namespace BTCPayServer
{
    public static class MoneyExtensions
    {
        public static Money GetValue(this IMoney m, BTCPayNetwork network = null)
        {
            switch (m)
            {
                case Money money:
                    return money;
                case MoneyBag mb:
                    return mb.Select(money => money.GetValue(network)).Sum();
//                case AssetMoney assetMoney:
//                    if (network is ElementsBTCPayNetwork elementsBTCPayNetwork)
//                    {
//                        return elementsBTCPayNetwork.AssetId == assetMoney.AssetId
//                            ? new Money(assetMoney.Quantity)
//                            : Money.Zero;
//                    }
//                    throw new NotSupportedException("IMoney type not supported");
                default:
                    throw new NotSupportedException("IMoney type not supported");
            }
        }
    }
}
