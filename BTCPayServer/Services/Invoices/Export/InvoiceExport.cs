﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Payments.Bitcoin;
using BTCPayServer.Services.Invoices;
using Newtonsoft.Json;

namespace BTCPayServer.Services.Invoices.Export
{
    public class InvoiceExport
    {
        public BTCPayNetworkProvider Networks { get; }

        public InvoiceExport(BTCPayNetworkProvider networks)
        {
            Networks = networks;
        }
        public string Process(InvoiceEntity[] invoices, string fileFormat)
        {
            var csvInvoices = new List<ExportInvoiceHolder>();
            foreach (var i in invoices)
            {
                csvInvoices.AddRange(convertFromDb(i));
            }

            if (String.Equals(fileFormat, "json", StringComparison.OrdinalIgnoreCase))
                return processJson(csvInvoices);
            else if (String.Equals(fileFormat, "csv", StringComparison.OrdinalIgnoreCase))
                return processCsv(csvInvoices);
            else
                throw new Exception("Export format not supported");
        }

        private string processJson(List<ExportInvoiceHolder> invoices)
        {
            var serializerSett = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
            var json = JsonConvert.SerializeObject(invoices, Formatting.Indented, serializerSett);

            return json;
        }

        private string processCsv(List<ExportInvoiceHolder> invoices)
        {
            var serializer = new CsvSerializer<ExportInvoiceHolder>();
            var csv = serializer.Serialize(invoices);

            return csv;
        }

        private IEnumerable<ExportInvoiceHolder> convertFromDb(InvoiceEntity invoice)
        {
            var exportList = new List<ExportInvoiceHolder>();
            // in this first version we are only exporting invoices that were paid
            foreach (var payment in invoice.GetPayments())
            {
                // not accounted payments are payments which got double spent like RBfed
                if (!payment.Accounted)
                    continue;

                var cryptoCode = payment.GetPaymentMethodId().CryptoCode;
                var pdata = payment.GetCryptoPaymentData();

                var pmethod = invoice.GetPaymentMethod(payment.GetPaymentMethodId(), Networks);

                var target = new ExportInvoiceHolder
                {
                    ReceivedDate = payment.ReceivedTime.UtcDateTime,
                    PaymentId = pdata.GetPaymentId(),
                    CryptoCode = cryptoCode,
                    ConversionRate = pmethod.Rate,
                    PaymentType = payment.GetPaymentMethodId().PaymentType == Payments.PaymentTypes.BTCLike ? "OnChain" : "OffChain",
                    Destination = payment.GetCryptoPaymentData().GetDestination(Networks.GetNetwork(cryptoCode)),
                    Paid = pdata.GetValue().ToString(CultureInfo.InvariantCulture),
                    OrderId = invoice.OrderId,
                    StoreId = invoice.StoreId,
                    InvoiceId = invoice.Id,
                    InvoiceCreatedDate = invoice.InvoiceTime.UtcDateTime,
                    InvoiceExpirationDate = invoice.ExpirationTime.UtcDateTime,
                    InvoiceMonitoringDate = invoice.MonitoringExpiration.UtcDateTime,
#pragma warning disable CS0618 // Type or member is obsolete
                    InvoiceFullStatus = invoice.GetInvoiceState().ToString(),
                    InvoiceStatus = invoice.StatusString,
                    InvoiceExceptionStatus = invoice.ExceptionStatusString,
#pragma warning restore CS0618 // Type or member is obsolete
                    InvoiceItemCode = invoice.ProductInformation.ItemCode,
                    InvoiceItemDesc = invoice.ProductInformation.ItemDesc,
                    InvoicePrice = invoice.ProductInformation.Price,
                    InvoiceCurrency = invoice.ProductInformation.Currency,
                };

                exportList.Add(target);
            }

            exportList = exportList.OrderBy(a => a.ReceivedDate).ToList();

            return exportList;
        }
    }

    public class ExportInvoiceHolder
    {
        public DateTime ReceivedDate { get; set; }
        public string StoreId { get; set; }
        public string OrderId { get; set; }
        public string InvoiceId { get; set; }
        public DateTime InvoiceCreatedDate { get; set; }
        public DateTime InvoiceExpirationDate { get; set; }
        public DateTime InvoiceMonitoringDate { get; set; }

        public string PaymentId { get; set; }
        public string Destination { get; set; }
        public string PaymentType { get; set; }
        public string Paid { get; set; }
        public string CryptoCode { get; set; }
        public decimal ConversionRate { get; set; }

        public decimal InvoicePrice { get; set; }
        public string InvoiceCurrency { get; set; }
        public string InvoiceItemCode { get; set; }
        public string InvoiceItemDesc { get; set; }
        public string InvoiceFullStatus { get; set; }
        public string InvoiceStatus { get; set; }
        public string InvoiceExceptionStatus { get; set; }
    }
}
