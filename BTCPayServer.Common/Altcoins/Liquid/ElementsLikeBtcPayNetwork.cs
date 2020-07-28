#if ALTCOINS_RELEASE || DEBUG
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBXplorer;
using NBXplorer.Models;

namespace BTCPayServer
{
    public class ElementsBTCPayNetwork : BTCPayNetwork
    {
        public string NetworkCryptoCode { get; set; }
        public uint256 AssetId { get; set; }
        public override bool ReadonlyWallet { get; set; } = true;

        public override IEnumerable<(MatchedOutput matchedOutput, OutPoint outPoint)> GetValidOutputs(
            NewTransactionEvent evtOutputs)
        {
            return evtOutputs.Outputs.Where(output =>
                output.Value is AssetMoney assetMoney && assetMoney.AssetId == AssetId).Select(output =>
            {
                var outpoint = new OutPoint(evtOutputs.TransactionData.TransactionHash, output.Index);
                return (output, outpoint);
            });
        }

        public override GetTransactionsResponse FilterValidTransactions(GetTransactionsResponse response)
        {
            TransactionInformationSet Filter(TransactionInformationSet transactionInformationSet)
            {
                return new TransactionInformationSet()
                {
                    Transactions =
                        transactionInformationSet.Transactions.FindAll(information =>
                            information.Outputs.Any(output =>
                                output.Value is AssetMoney assetMoney && assetMoney.AssetId == AssetId) ||
                            information.Inputs.Any(output =>
                                output.Value is AssetMoney assetMoney && assetMoney.AssetId == AssetId))
                };
            }

            return new GetTransactionsResponse()
            {
                Height = response.Height,
                ConfirmedTransactions = Filter(response.ConfirmedTransactions),
                ReplacedTransactions = Filter(response.ReplacedTransactions),
                UnconfirmedTransactions = Filter(response.UnconfirmedTransactions)
            };
        }


        public override string GenerateBIP21(string cryptoInfoAddress, Money cryptoInfoDue)
        {
            //precision 0: 10 = 0.00000010
            //precision 2: 10 = 0.00001000
            //precision 8: 10 = 10
            var money = new Money(cryptoInfoDue.ToDecimal(MoneyUnit.BTC) / decimal.Parse("1".PadRight(1 + 8 - Divisibility, '0')), MoneyUnit.BTC);
            return $"{base.GenerateBIP21(cryptoInfoAddress, money)}&assetid={AssetId}";
        }
    }
}
#endif
