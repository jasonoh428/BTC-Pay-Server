﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Microsoft.AspNetCore.Http.Internal;
using System.IO;
using BTCPayServer.Authentication;
using System.Security.Principal;
using NBitpayClient.Extensions;
using BTCPayServer.Logging;
using Newtonsoft.Json;
using BTCPayServer.Models;
using BTCPayServer.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Http.Extensions;

namespace BTCPayServer.Hosting
{
	public class BTCPayMiddleware
	{
		TokenRepository _TokenRepository;
		RequestDelegate _Next;
		public BTCPayMiddleware(RequestDelegate next, TokenRepository tokenRepo)
		{
			_TokenRepository = tokenRepo ?? throw new ArgumentNullException(nameof(tokenRepo));
			_Next = next ?? throw new ArgumentNullException(nameof(next));
		}

		public async Task Invoke(HttpContext httpContext)
		{
			httpContext.Request.Headers.TryGetValue("x-signature", out StringValues values);
			var sig = values.FirstOrDefault();
			httpContext.Request.Headers.TryGetValue("x-identity", out values);
			var id = values.FirstOrDefault();
			if(!string.IsNullOrEmpty(sig) && !string.IsNullOrEmpty(id))
			{
				httpContext.Request.EnableRewind();

				string body = string.Empty;
				if(httpContext.Request.ContentLength != 0 && httpContext.Request.Body != null)
				{
					using(StreamReader reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, true, 1024, true))
					{
						body = reader.ReadToEnd();
					}
					httpContext.Request.Body.Position = 0;
				}

				var url = httpContext.Request.GetEncodedUrl();
				try
				{
					var key = new PubKey(id);
					if(BitIdExtensions.CheckBitIDSignature(key, sig, url, body))
					{
						var bitid = new BitIdentity(key);
						httpContext.User = new GenericPrincipal(bitid, new string[0]);
						Logs.PayServer.LogDebug($"BitId signature check success for SIN {bitid.SIN}");
					}
				}
				catch(FormatException) { }
				if(!(httpContext.User.Identity is BitIdentity))
					Logs.PayServer.LogDebug("BitId signature check failed");
			}

			try
			{
				await _Next(httpContext);
			}
			catch(UnauthorizedAccessException ex)
			{
				await HandleBitpayHttpException(httpContext, new BitpayHttpException(401, ex.Message));
			}
			catch(BitpayHttpException ex)
			{
				await HandleBitpayHttpException(httpContext, ex);
			}
			catch(Exception ex)
			{
				Logs.PayServer.LogCritical(new EventId(), ex, "Unhandled exception in BTCPayMiddleware");
				throw;
			}
		}

		private static async Task HandleBitpayHttpException(HttpContext httpContext, BitpayHttpException ex)
		{
			httpContext.Response.StatusCode = ex.StatusCode;
			using(var writer = new StreamWriter(httpContext.Response.Body, Encoding.UTF8, 1024, true))
			{
				var result = JsonConvert.SerializeObject(new BitpayErrorsModel(ex));
				writer.Write(result);
				await writer.FlushAsync();
			}
		}
	}
}
