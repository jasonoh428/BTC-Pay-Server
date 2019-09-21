﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Payments;
using BTCPayServer.Security;
using BTCPayServer.Services.Rates;
using NBitcoin;
using NBXplorer;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Data
{
    public static class StoreDataExtensions
    {
        public static bool HasClaim(this StoreData storeData, string claim)
        {
            return storeData.GetClaims().Any(c => c.Type == claim && c.Value == storeData.Id);
        }
        public static Claim[] GetClaims(this StoreData storeData)
        {
            List<Claim> claims = new List<Claim>();
            claims.AddRange(storeData.AdditionalClaims);
#pragma warning disable CS0612 // Type or member is obsolete
            var role = storeData.Role;
#pragma warning restore CS0612 // Type or member is obsolete
            if (role == StoreRoles.Owner)
            {
                claims.Add(new Claim(Policies.CanModifyStoreSettings.Key, storeData.Id));
            }

            if (role == StoreRoles.Owner || role == StoreRoles.Guest || storeData.GetStoreBlob().AnyoneCanInvoice)
            {
                claims.Add(new Claim(Policies.CanCreateInvoice.Key, storeData.Id));
            }
            return claims.ToArray();
        }

#pragma warning disable CS0618
        public static PaymentMethodId GetDefaultPaymentId(this StoreData storeData, BTCPayNetworkProvider networks)
        {
            PaymentMethodId[] paymentMethodIds = storeData.GetEnabledPaymentIds(networks);

            var defaultPaymentId = string.IsNullOrEmpty(storeData.DefaultCrypto) ? null : PaymentMethodId.Parse(storeData.DefaultCrypto);
            var chosen = paymentMethodIds.FirstOrDefault(f => f == defaultPaymentId) ??
                         paymentMethodIds.FirstOrDefault(f => f.CryptoCode == defaultPaymentId?.CryptoCode) ??
                         paymentMethodIds.FirstOrDefault();
            return chosen;
        }

        public static PaymentMethodId[] GetEnabledPaymentIds(this StoreData storeData, BTCPayNetworkProvider networks)
        {
            var excludeFilter = storeData.GetStoreBlob().GetExcludedPaymentMethods();
            var paymentMethodIds = storeData.GetSupportedPaymentMethods(networks).Select(p => p.PaymentId)
                                .Where(a => !excludeFilter.Match(a))
                                .OrderByDescending(a => a.CryptoCode == "BTC")
                                .ThenBy(a => a.CryptoCode)
                                .ThenBy(a => a.PaymentType == PaymentTypes.LightningLike ? 1 : 0)
                                .ToArray();
            return paymentMethodIds;
        }

        public static void SetDefaultPaymentId(this StoreData storeData, PaymentMethodId defaultPaymentId)
        {
            storeData.DefaultCrypto = defaultPaymentId.ToString();
        }
#pragma warning restore CS0618

        static Network Dummy = Network.Main;

        public static StoreBlob GetStoreBlob(this StoreData storeData)
        {
            var result = storeData.StoreBlob == null ? new StoreBlob() : new Serializer(Dummy).ToObject<StoreBlob>(Encoding.UTF8.GetString(storeData.StoreBlob));
            if (result.PreferredExchange == null)
                result.PreferredExchange = CoinAverageRateProvider.CoinAverageName;
            return result;
        }

        public static bool SetStoreBlob(this StoreData storeData, StoreBlob storeBlob)
        {
            var original = new Serializer(Dummy).ToString(storeData.GetStoreBlob());
            var newBlob = new Serializer(Dummy).ToString(storeBlob);
            if (original == newBlob)
                return false;
            storeData.StoreBlob = Encoding.UTF8.GetBytes(newBlob);
            return true;
        }

        public static IEnumerable<ISupportedPaymentMethod> GetSupportedPaymentMethods(this StoreData storeData, BTCPayNetworkProvider networks)
        {
            networks = networks.UnfilteredNetworks;
#pragma warning disable CS0618
            bool btcReturned = false;

            // Legacy stuff which should go away
            if (!string.IsNullOrEmpty(storeData.DerivationStrategy))
            {
                btcReturned = true;
                yield return DerivationSchemeSettings.Parse(storeData.DerivationStrategy, networks.BTC);
            }


            if (!string.IsNullOrEmpty(storeData.DerivationStrategies))
            {
                JObject strategies = JObject.Parse(storeData.DerivationStrategies);
                foreach (var strat in strategies.Properties())
                {
                    var paymentMethodId = PaymentMethodId.Parse(strat.Name);
                    var network = networks.GetNetwork<BTCPayNetworkBase>(paymentMethodId.CryptoCode);
                    if (network != null)
                    {
                        if (network == networks.BTC && paymentMethodId.PaymentType == PaymentTypes.BTCLike && btcReturned)
                            continue;
                        if (strat.Value.Type == JTokenType.Null)
                            continue;
                        yield return
                            paymentMethodId.PaymentType.DeserializeSupportedPaymentMethod(network, strat.Value);
                    }
                }
            }
#pragma warning restore CS0618
        }

        public static void SetSupportedPaymentMethod(this StoreData storeData, ISupportedPaymentMethod supportedPaymentMethod)
        {
            storeData.SetSupportedPaymentMethod(null, supportedPaymentMethod);
        }

        /// <summary>
        /// Set or remove a new supported payment method for the store
        /// </summary>
        /// <param name="paymentMethodId">The paymentMethodId</param>
        /// <param name="supportedPaymentMethod">The payment method, or null to remove</param>
        public static void SetSupportedPaymentMethod(this StoreData storeData, PaymentMethodId paymentMethodId, ISupportedPaymentMethod supportedPaymentMethod)
        {
            if (supportedPaymentMethod != null && paymentMethodId != null && paymentMethodId != supportedPaymentMethod.PaymentId)
            {
                throw new InvalidOperationException("Incoherent arguments, this should never happen");
            }
            if (supportedPaymentMethod == null && paymentMethodId == null)
                throw new ArgumentException($"{nameof(supportedPaymentMethod)} or {nameof(paymentMethodId)} should be specified");
            if (supportedPaymentMethod != null && paymentMethodId == null)
            {
                paymentMethodId = supportedPaymentMethod.PaymentId;
            }

#pragma warning disable CS0618
            JObject strategies = string.IsNullOrEmpty(storeData.DerivationStrategies) ? new JObject() : JObject.Parse(storeData.DerivationStrategies);
            bool existing = false;
            foreach (var strat in strategies.Properties().ToList())
            {
                var stratId = PaymentMethodId.Parse(strat.Name);
                if (stratId.IsBTCOnChain)
                {
                    // Legacy stuff which should go away
                    storeData.DerivationStrategy = null;
                }
                if (stratId == paymentMethodId)
                {
                    if (supportedPaymentMethod == null)
                    {
                        strat.Remove();
                    }
                    else
                    {
                        strat.Value = PaymentMethodExtensions.Serialize(supportedPaymentMethod);
                    }
                    existing = true;
                    break;
                }
            }

            if (!existing && supportedPaymentMethod == null && paymentMethodId.IsBTCOnChain)
            {
                storeData.DerivationStrategy = null;
            }
            else if (!existing && supportedPaymentMethod != null)
                strategies.Add(new JProperty(supportedPaymentMethod.PaymentId.ToString(), PaymentMethodExtensions.Serialize(supportedPaymentMethod)));
            storeData.DerivationStrategies = strategies.ToString();
#pragma warning restore CS0618
        }
    }
}
