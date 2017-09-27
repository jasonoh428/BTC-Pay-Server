﻿using BTCPayServer.Data;
using BTCPayServer.Filters;
using BTCPayServer.Models.InvoicingModels;
using BTCPayServer.Servcices.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NBitcoin;
using NBitpayClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BTCPayServer.Controllers
{
    public partial class InvoiceController
    {

		[HttpGet]
		[Route("i/{invoiceId}")]
		[AcceptMediaTypeConstraint("application/bitcoin-paymentrequest", false)]
		public async Task<IActionResult> Checkout(string invoiceId)
		{
			var invoice = await _InvoiceRepository.GetInvoice(null, invoiceId);
			if(invoice == null)
				return NotFound();
			var store = await _StoreRepository.FindStore(invoice.StoreId);
			var dto = invoice.EntityToDTO();

			var model = new PaymentModel()
			{
				OrderId = invoice.OrderId,
				InvoiceId = invoice.Id,
				BTCAddress = invoice.DepositAddress.ToString(),
				BTCAmount = (invoice.GetTotalCryptoDue() - invoice.TxFee).ToString(),
				BTCTotalDue = invoice.GetTotalCryptoDue().ToString(),
				BTCDue = invoice.GetCryptoDue().ToString(),
				CustomerEmail = invoice.RefundMail,
				ExpirationSeconds = Math.Max(0, (int)(invoice.ExpirationTime - DateTimeOffset.UtcNow).TotalSeconds),
				MaxTimeSeconds = (int)(invoice.ExpirationTime - invoice.InvoiceTime).TotalSeconds,
				ItemDesc = invoice.ProductInformation.ItemDesc,
				Rate = invoice.Rate.ToString(),
				RedirectUrl = invoice.RedirectURL,
				StoreName = store.StoreName,
				TxFees = invoice.TxFee.ToString(),
				InvoiceBitcoinUrl = dto.PaymentUrls.BIP72,
				TxCount = invoice.GetTxCount(),
				BTCPaid = invoice.GetTotalPaid().ToString(),
				Status = invoice.Status
			};

			var expiration = TimeSpan.FromSeconds((double)model.ExpirationSeconds);
			model.TimeLeft = PrettyPrint(expiration);
			return View(model);
		}

		private string PrettyPrint(TimeSpan expiration)
		{
			StringBuilder builder = new StringBuilder();
			if(expiration.Days >= 1)
				builder.Append(expiration.Days.ToString());
			if(expiration.Hours >= 1)
				builder.Append(expiration.Hours.ToString("00"));
			builder.Append($"{expiration.Minutes.ToString("00")}:{expiration.Seconds.ToString("00")}");
			return builder.ToString();
		}

		[HttpGet]
		[Route("i/{invoiceId}/status")]
		public async Task<IActionResult> GetStatus(string invoiceId)
		{
			var invoice = await _InvoiceRepository.GetInvoice(null, invoiceId);
			if(invoice == null)
				return NotFound();
			return Content(invoice.Status);
		}

		[HttpPost]
		[Route("i/{invoiceId}/UpdateCustomer")]
		public async Task<IActionResult> UpdateCustomer(string invoiceId, [FromBody]UpdateCustomerModel data)
		{
			if(!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			await _InvoiceRepository.UpdateInvoice(invoiceId, data).ConfigureAwait(false);
			return Ok();
		}

		[HttpGet]
		[Route("invoices")]
		[Authorize(AuthenticationSchemes = "Identity.Application")]
		[BitpayAPIConstraint(false)]
		public async Task<IActionResult> ListInvoices(string searchTerm = null, int skip = 0, int count = 20)
		{
			var model = new InvoicesModel();
			foreach(var invoice in await _InvoiceRepository.GetInvoices(new InvoiceQuery()
			{
				TextSearch = searchTerm,
				Count = count,
				Skip = skip,
				UserId = GetUserId()
			}))
			{
				model.SearchTerm = searchTerm;
				model.Invoices.Add(new InvoiceModel()
				{
					Status = invoice.Status,
					Date = invoice.InvoiceTime,
					InvoiceId = invoice.Id,
					AmountCurrency = $"{invoice.ProductInformation.Price.ToString(CultureInfo.InvariantCulture)} {invoice.ProductInformation.Currency}"
				});
			}
			model.Skip = skip;
			model.Count = count;
			model.StatusMessage = StatusMessage;
			return View(model);
		}

		[HttpGet]
		[Route("invoices/create")]
		[Authorize(AuthenticationSchemes = "Identity.Application")]
		[BitpayAPIConstraint(false)]
		public async Task<IActionResult> CreateInvoice()
		{
			var stores = await GetStores(GetUserId());
			if(stores.Count() == 0)
			{
				StatusMessage = "Error: You need to create at least one store before creating a transaction";
				return RedirectToAction(nameof(StoresController.ListStores), "Stores");
			}
			return View(new CreateInvoiceModel() { Stores = stores });
		}

		[HttpPost]
		[Route("invoices/create")]
		[Authorize(AuthenticationSchemes = "Identity.Application")]
		[BitpayAPIConstraint(false)]
		public async Task<IActionResult> CreateInvoice(CreateInvoiceModel model)
		{
			if(!ModelState.IsValid)
			{
				model.Stores = await GetStores(GetUserId(), model.StoreId);
				return View(model);
			}
			var store = await _StoreRepository.FindStore(model.StoreId, GetUserId());
			if(string.IsNullOrEmpty(store.DerivationStrategy))
			{
				StatusMessage = "Error: You need to configure the derivation scheme in order to create an invoice";
				return RedirectToAction(nameof(StoresController.UpdateStore), "Stores", new
				{
					storeId = store.Id
				});
			}
			var result = await CreateInvoiceCore(new Invoice()
			{
				Price = model.Amount.Value,
				Currency = "USD",
				PosData = model.PosData,
				OrderId = model.OrderId,
				//RedirectURL = redirect + "redirect",
				NotificationURL = model.NotificationUrl,
				ItemDesc = model.ItemDesc,
				FullNotifications = true,
				BuyerEmail = model.BuyerEmail,
			}, store);
			
			StatusMessage = $"Invoice {result.Data.Id} just created!";
			return RedirectToAction(nameof(ListInvoices));
		}

		private async Task<SelectList> GetStores(string userId, string storeId = null)
		{
			return new	SelectList(await _StoreRepository.GetStoresByUserId(userId), nameof(StoreData.Id), nameof(StoreData.StoreName), storeId);
		}

		[HttpPost]
		[Authorize(AuthenticationSchemes = "Identity.Application")]
		[BitpayAPIConstraint(false)]
		public IActionResult SearchInvoice(InvoicesModel invoices)
		{
			return RedirectToAction(nameof(ListInvoices), new
			{
				searchTerm = invoices.SearchTerm,
				skip = invoices.Skip,
				count = invoices.Count,
			});
		}

		[TempData]
		public string StatusMessage
		{
			get;
			set;
		}

		private string GetUserId()
		{
			return _UserManager.GetUserId(User);
		}
	}
}
