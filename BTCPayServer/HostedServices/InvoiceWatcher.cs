﻿using NBXplorer;
using Microsoft.Extensions.Logging;
using NBXplorer.DerivationStrategy;
using NBXplorer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using BTCPayServer.Logging;
using System.Threading;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using Hangfire;
using BTCPayServer.Services.Wallets;
using BTCPayServer.Controllers;
using BTCPayServer.Events;
using Microsoft.AspNetCore.Hosting;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services;

namespace BTCPayServer.HostedServices
{
    public class InvoiceWatcher : IHostedService
    {
        class UpdateInvoiceContext
        {
            public UpdateInvoiceContext(InvoiceEntity invoice)
            {
                Invoice = invoice;
            }
            public InvoiceEntity Invoice { get; set; }
            public List<object> Events { get; set; } = new List<object>();

            bool _Dirty = false;
            public void MarkDirty()
            {
                _Dirty = true;
            }

            public bool Dirty => _Dirty;
        }

        InvoiceRepository _InvoiceRepository;
        EventAggregator _EventAggregator;
        BTCPayNetworkProvider _NetworkProvider;

        public InvoiceWatcher(
            BTCPayNetworkProvider networkProvider,
            InvoiceRepository invoiceRepository,
            EventAggregator eventAggregator)
        {
            PollInterval = TimeSpan.FromMinutes(1.0);
            _InvoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
            _EventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _NetworkProvider = networkProvider;
        }
        CompositeDisposable leases = new CompositeDisposable();


        private async Task UpdateInvoice(UpdateInvoiceContext context)
        {
            var invoice = context.Invoice;
            if (invoice.Status == "new" && invoice.ExpirationTime < DateTimeOffset.UtcNow)
            {
                context.MarkDirty();
                await _InvoiceRepository.UnaffectAddress(invoice.Id);

                context.Events.Add(new InvoiceEvent(invoice, 1004, "invoice_expired"));
                invoice.Status = "expired";
            }

            var derivationStrategies = invoice.GetDerivationStrategies(_NetworkProvider).ToArray();
            var payments = invoice.GetPayments().Where(p => p.Accounted).ToArray();
            var cryptoDataAll = invoice.GetCryptoData(_NetworkProvider);
            foreach (var cryptoData in cryptoDataAll.Select(c => c))
            {
                var accounting = cryptoData.Calculate();
                var network = _NetworkProvider.GetNetwork(cryptoData.GetId().CryptoCode);
                if (network == null)
                    continue;
                if (invoice.Status == "new" || invoice.Status == "expired")
                {
                    var totalPaid = payments.Select(p => p.GetValue(cryptoDataAll, cryptoData.GetId())).Sum();
                    if (totalPaid >= accounting.TotalDue)
                    {
                        if (invoice.Status == "new")
                        {
                            context.Events.Add(new InvoiceEvent(invoice, 1003, "invoice_paidInFull"));
                            invoice.Status = "paid";
                            invoice.ExceptionStatus = totalPaid > accounting.TotalDue ? "paidOver" : null;
                            await _InvoiceRepository.UnaffectAddress(invoice.Id);
                            context.MarkDirty();
                        }
                        else if (invoice.Status == "expired" && invoice.ExceptionStatus != "paidLate")
                        {
                            invoice.ExceptionStatus = "paidLate";
                            context.Events.Add(new InvoiceEvent(invoice, 1009, "invoice_paidAfterExpiration"));
                            context.MarkDirty();
                        }
                    }

                    if (totalPaid < accounting.TotalDue && invoice.GetPayments().Count != 0 && invoice.ExceptionStatus != "paidPartial")
                    {
                        invoice.ExceptionStatus = "paidPartial";
                        context.MarkDirty();
                    }
                }

                if (invoice.Status == "paid")
                {
                    var transactions = payments.Where(p => p.GetCryptoPaymentData().PaymentConfirmed(p, invoice.SpeedPolicy, network));

                    var totalConfirmed = transactions.Select(t => t.GetValue(cryptoDataAll, cryptoData.GetId())).Sum();

                    if (// Is after the monitoring deadline
                       (invoice.MonitoringExpiration < DateTimeOffset.UtcNow)
                       &&
                       // And not enough amount confirmed
                       (totalConfirmed < accounting.TotalDue))
                    {
                        await _InvoiceRepository.UnaffectAddress(invoice.Id);
                        context.Events.Add(new InvoiceEvent(invoice, 1013, "invoice_failedToConfirm"));
                        invoice.Status = "invalid";
                        context.MarkDirty();
                    }
                    else if (totalConfirmed >= accounting.TotalDue)
                    {
                        await _InvoiceRepository.UnaffectAddress(invoice.Id);
                        context.Events.Add(new InvoiceEvent(invoice, 1005, "invoice_confirmed"));
                        invoice.Status = "confirmed";
                        context.MarkDirty();
                    }
                }

                if (invoice.Status == "confirmed")
                {
                    var transactions = payments.Where(p => p.GetCryptoPaymentData().PaymentCompleted(p, network));
                    var totalConfirmed = transactions.Select(t => t.GetValue(cryptoDataAll, cryptoData.GetId())).Sum();
                    if (totalConfirmed >= accounting.TotalDue)
                    {
                        context.Events.Add(new InvoiceEvent(invoice, 1006, "invoice_completed"));
                        invoice.Status = "complete";
                        context.MarkDirty();
                    }
                }
            }
        }

        TimeSpan _PollInterval;
        public TimeSpan PollInterval
        {
            get
            {
                return _PollInterval;
            }
            set
            {
                _PollInterval = value;
            }
        }

        private void Watch(string invoiceId)
        {
            if (invoiceId == null)
                throw new ArgumentNullException(nameof(invoiceId));
            _WatchRequests.Add(invoiceId);
        }

        private async Task Wait(string invoiceId)
        {
            var invoice = await _InvoiceRepository.GetInvoice(null, invoiceId);
            try
            {
                var now = DateTimeOffset.UtcNow;
                if (invoice.ExpirationTime > now)
                {
                    await Task.Delay(invoice.ExpirationTime - now, _Cts.Token);
                }
                Watch(invoiceId);
                now = DateTimeOffset.UtcNow;
                if (invoice.MonitoringExpiration > now)
                {
                    await Task.Delay(invoice.MonitoringExpiration - now, _Cts.Token);
                }
                Watch(invoiceId);
            }
            catch when (_Cts.IsCancellationRequested)
            { }

        }

        BlockingCollection<string> _WatchRequests = new BlockingCollection<string>(new ConcurrentQueue<string>());

        Task _Loop;
        Task _WaitingInvoices;
        CancellationTokenSource _Cts;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _Loop = StartLoop(_Cts.Token);
            _WaitingInvoices = WaitPendingInvoices();

            leases.Add(_EventAggregator.Subscribe<Events.InvoiceNeedUpdateEvent>(b =>
            {
                Watch(b.InvoiceId);
            }));
            leases.Add(_EventAggregator.Subscribe<Events.InvoiceEvent>(async b =>
            {
                if (b.Name == "invoice_created")
                {
                    Watch(b.InvoiceId);
                    await Wait(b.InvoiceId);
                }

                if (b.Name == "invoice_receivedPayment")
                {
                    Watch(b.InvoiceId);
                }
            }));
            return Task.CompletedTask;
        }

        private async Task WaitPendingInvoices()
        {
            await Task.WhenAll((await _InvoiceRepository.GetPendingInvoices())
                .Select(id => Wait(id)).ToArray());
            _WaitingInvoices = null;
        }

        async Task StartLoop(CancellationToken cancellation)
        {
            Logs.PayServer.LogInformation("Start watching invoices");
            await Task.Delay(1).ConfigureAwait(false); // Small hack so that the caller does not block on GetConsumingEnumerable
            try
            {
                foreach (var invoiceId in _WatchRequests.GetConsumingEnumerable(cancellation))
                {
                    int maxLoop = 5;
                    int loopCount = -1;
                    while (!cancellation.IsCancellationRequested && loopCount < maxLoop)
                    {
                        loopCount++;
                        try
                        {
                            var invoice = await _InvoiceRepository.GetInvoice(null, invoiceId, true);
                            if (invoice == null)
                                break;
                            var updateContext = new UpdateInvoiceContext(invoice);
                            await UpdateInvoice(updateContext);
                            if (updateContext.Dirty)
                            {
                                await _InvoiceRepository.UpdateInvoiceStatus(invoice.Id, invoice.Status, invoice.ExceptionStatus);
                                updateContext.Events.Add(new InvoiceDataChangedEvent(invoice));
                            }

                            foreach (var evt in updateContext.Events)
                            {
                                _EventAggregator.Publish(evt, evt.GetType());
                            }

                            if (invoice.Status == "complete" ||
                               ((invoice.Status == "invalid" || invoice.Status == "expired") && invoice.MonitoringExpiration < DateTimeOffset.UtcNow))
                            {
                                if (await _InvoiceRepository.RemovePendingInvoice(invoice.Id))
                                    _EventAggregator.Publish(new InvoiceStopWatchedEvent(invoice.Id));
                                break;
                            }

                            if (updateContext.Events.Count == 0 || cancellation.IsCancellationRequested)
                                break;
                        }
                        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Logs.PayServer.LogError(ex, "Unhandled error on watching invoice " + invoiceId);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            Task.Delay(10000, cancellation)
                                .ContinueWith(t => _WatchRequests.Add(invoiceId), TaskScheduler.Default);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            break;
                        }
                    }
                }
            }
            catch when (cancellation.IsCancellationRequested)
            {
            }
            Logs.PayServer.LogInformation("Stop watching invoices");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            leases.Dispose();
            _Cts.Cancel();
            var waitingPendingInvoices = _WaitingInvoices ?? Task.CompletedTask;
            return Task.WhenAll(waitingPendingInvoices, _Loop);
        }
    }
}
