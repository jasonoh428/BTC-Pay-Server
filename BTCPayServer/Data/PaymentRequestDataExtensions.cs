﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBXplorer;
using Newtonsoft.Json.Linq;
using static BTCPayServer.Data.PaymentRequestData;

namespace BTCPayServer.Data
{
    public static class PaymentRequestDataExtensions
    {
        public static PaymentRequestBlob GetBlob(this PaymentRequestData paymentRequestData)
        {
            var result = paymentRequestData.Blob == null
                ? new PaymentRequestBlob()
                : JObject.Parse(ZipUtils.Unzip(paymentRequestData.Blob)).ToObject<PaymentRequestBlob>();
            return result;
        }

        public static bool SetBlob(this PaymentRequestData paymentRequestData, PaymentRequestBlob blob)
        {
            var original = new Serializer(Network.Main).ToString(paymentRequestData.GetBlob());
            var newBlob = new Serializer(Network.Main).ToString(blob);
            if (original == newBlob)
                return false;
            paymentRequestData.Blob = ZipUtils.Zip(newBlob);
            return true;
        }
    }
}
