﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Models.InvoicingModels;
using BTCPayServer.Rating;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Rates;
using NBitcoin;
using NBitpayClient;
using Newtonsoft.Json.Linq;
using InvoiceResponse = BTCPayServer.Models.InvoiceResponse;

namespace BTCPayServer.Payments
{
    /// <summary>
    /// This class customize invoice creation by the creation of payment details for the PaymentMethod during invoice creation
    /// </summary>
    public interface IPaymentMethodHandler
    {
        string PrettyDescription { get; }
        /// <summary>
        /// Create needed to track payments of this invoice
        /// </summary>
        /// <param name="supportedPaymentMethod"></param>
        /// <param name="paymentMethod"></param>
        /// <param name="store"></param>
        /// <param name="network"></param>
        /// <param name="preparePaymentObject"></param>
        /// <returns></returns>
        Task<IPaymentMethodDetails> CreatePaymentMethodDetails(ISupportedPaymentMethod supportedPaymentMethod,
            PaymentMethod paymentMethod, StoreData store, BTCPayNetworkBase network, object preparePaymentObject);

        /// <summary>
        /// This method called before the rate have been fetched
        /// </summary>
        /// <param name="supportedPaymentMethod"></param>
        /// <param name="store"></param>
        /// <param name="network"></param>
        /// <returns></returns>
        object PreparePayment(ISupportedPaymentMethod supportedPaymentMethod, StoreData store, BTCPayNetworkBase network);

        void PrepareInvoiceDto(InvoiceResponse invoiceResponse, InvoiceEntity invoiceEntity,
            InvoiceCryptoInfo invoiceCryptoInfo,
            PaymentMethodAccounting accounting, PaymentMethod info);


        string ToPrettyString(PaymentMethodId paymentMethodId);

        void PreparePaymentModel(PaymentModel model, InvoiceResponse invoiceResponse);
        string GetCryptoImage(PaymentMethodId paymentMethodId);
        string GetPaymentMethodName(PaymentMethodId paymentMethodId);

        Task<string> IsPaymentMethodAllowedBasedOnInvoiceAmount(StoreBlob storeBlob,
            Dictionary<CurrencyPair, Task<RateResult>> rate,
            Money amount, PaymentMethodId paymentMethodId);

        IEnumerable<PaymentMethodId> GetSupportedPaymentMethods();

        CryptoPaymentData GetCryptoPaymentData(PaymentEntity paymentEntity);

        ISupportedPaymentMethod DeserializeSupportedPaymentMethod(PaymentMethodId paymentMethodId, JToken value);
        IPaymentMethodDetails DeserializePaymentMethodDetails(JObject jobj);
        string GetTransactionLink(PaymentMethodId paymentMethodId, params object[] args);
    }

    public interface IPaymentMethodHandler<TSupportedPaymentMethod, TBTCPayNetwork> : IPaymentMethodHandler
        where TSupportedPaymentMethod : ISupportedPaymentMethod
        where TBTCPayNetwork : BTCPayNetworkBase
    {
        Task<IPaymentMethodDetails> CreatePaymentMethodDetails(TSupportedPaymentMethod supportedPaymentMethod,
            PaymentMethod paymentMethod, StoreData store, TBTCPayNetwork network, object preparePaymentObject);
    }

    public abstract class PaymentMethodHandlerBase<TSupportedPaymentMethod, TBTCPayNetwork> : IPaymentMethodHandler<
            TSupportedPaymentMethod, TBTCPayNetwork>
        where TSupportedPaymentMethod : ISupportedPaymentMethod
        where TBTCPayNetwork : BTCPayNetworkBase
    {
        public abstract string PrettyDescription { get; }
        public abstract PaymentTypes PaymentType { get; }

        public abstract Task<IPaymentMethodDetails> CreatePaymentMethodDetails(
            TSupportedPaymentMethod supportedPaymentMethod,
            PaymentMethod paymentMethod, StoreData store, TBTCPayNetwork network, object preparePaymentObject);

        public abstract void PrepareInvoiceDto(InvoiceResponse invoiceResponse, InvoiceEntity invoiceEntity,
            InvoiceCryptoInfo invoiceCryptoInfo, PaymentMethodAccounting accounting, PaymentMethod info);

        public abstract void PreparePaymentModel(PaymentModel model, InvoiceResponse invoiceResponse);
        public abstract string GetCryptoImage(PaymentMethodId paymentMethodId);
        public abstract string GetPaymentMethodName(PaymentMethodId paymentMethodId);

        public abstract Task<string> IsPaymentMethodAllowedBasedOnInvoiceAmount(StoreBlob storeBlob,
            Dictionary<CurrencyPair, Task<RateResult>> rate, Money amount, PaymentMethodId paymentMethodId);

        public abstract IEnumerable<PaymentMethodId> GetSupportedPaymentMethods();
        public abstract CryptoPaymentData GetCryptoPaymentData(PaymentEntity paymentEntity);

        public abstract ISupportedPaymentMethod DeserializeSupportedPaymentMethod(PaymentMethodId paymentMethodId, JToken value);
        public abstract IPaymentMethodDetails DeserializePaymentMethodDetails(JObject jobj);
        public abstract string GetTransactionLink(PaymentMethodId paymentMethodId, params object[] args);


        public virtual object PreparePayment(TSupportedPaymentMethod supportedPaymentMethod, StoreData store,
            BTCPayNetworkBase network)
        {
            return null;
        }

        public Task<IPaymentMethodDetails> CreatePaymentMethodDetails(ISupportedPaymentMethod supportedPaymentMethod, PaymentMethod paymentMethod,
            StoreData store, BTCPayNetworkBase network, object preparePaymentObject)
        {
            if (supportedPaymentMethod is TSupportedPaymentMethod method && network is TBTCPayNetwork correctNetwork)
            {
                return CreatePaymentMethodDetails(method, paymentMethod, store, correctNetwork, preparePaymentObject);
            }

            throw new NotSupportedException("Invalid supportedPaymentMethod");
        }

        object IPaymentMethodHandler.PreparePayment(ISupportedPaymentMethod supportedPaymentMethod, StoreData store,
            BTCPayNetworkBase network)
        {
            if (supportedPaymentMethod is TSupportedPaymentMethod method)
            {
                return PreparePayment(method, store, network);
            }

            throw new NotSupportedException("Invalid supportedPaymentMethod");
        }

        public string ToPrettyString(PaymentMethodId paymentMethodId)
        {
            return $"{paymentMethodId.CryptoCode} ({PrettyDescription})";
        }
    }
}
