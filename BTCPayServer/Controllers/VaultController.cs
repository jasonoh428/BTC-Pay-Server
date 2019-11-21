﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.ModelBinders;
using BTCPayServer.Models;
using BTCPayServer.Models.StoreViewModels;
using BTCPayServer.Payments;
using BTCPayServer.Security;
using BTCPayServer.Services;
using LedgerWallet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBXplorer.DerivationStrategy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Controllers
{
    [Route("vault")]
    public class VaultController : Controller
    {
        private readonly IAuthorizationService _authorizationService;

        public VaultController(BTCPayNetworkProvider networks, IAuthorizationService authorizationService)
        {
            Networks = networks;
            _authorizationService = authorizationService;
        }

        public BTCPayNetworkProvider Networks { get; }

        [HttpGet]
        [Route("{cryptoCode}/xpub")]
        [Route("wallets/{walletId}/xpub")]
        public async Task<IActionResult> VaultBridgeConnection(string cryptoCode = null,
            [ModelBinder(typeof(WalletIdModelBinder))]
            WalletId walletId = null)
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
                return NotFound();
            cryptoCode = cryptoCode ?? walletId.CryptoCode;
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            {
                var cancellationToken = cts.Token;
                var network = Networks.GetNetwork<BTCPayNetwork>(cryptoCode);
                if (network == null)
                    return NotFound();
                var websocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                var hwi = new Hwi.HwiClient(network.NBitcoinNetwork)
                {
                    Transport = new HwiWebSocketTransport(websocket)
                };
                Hwi.HwiDeviceClient device = null;
                HDFingerprint? fingerprint = null;
                var websocketHelper = new WebSocketHelper(websocket);
                JObject o = null;
                try
                {
                    while (true)
                    {
                        var command = await websocketHelper.NextMessageAsync(cancellationToken);
                        switch (command)
                        {
                            case "ask-sign":
                                if (device == null)
                                {
                                    await websocketHelper.Send("{ \"error\": \"need-device\"}", cancellationToken);
                                    continue;
                                }
                                if (walletId == null)
                                {
                                    await websocketHelper.Send("{ \"error\": \"invalid-walletId\"}", cancellationToken);
                                    continue;
                                }
                                if (fingerprint is null)
                                {
                                    try
                                    {
                                        fingerprint = (await device.GetXPubAsync(new KeyPath("44'"), cancellationToken)).ExtPubKey.ParentFingerprint;
                                    }
                                    catch (Hwi.HwiException ex) when (ex.ErrorCode == Hwi.HwiErrorCode.DeviceNotReady)
                                    {
                                        await websocketHelper.Send("{ \"error\": \"need-pin\"}", cancellationToken);
                                        continue;
                                    }
                                }
                                await websocketHelper.Send("{ \"info\": \"ready\"}", cancellationToken);
                                o = JObject.Parse(await websocketHelper.NextMessageAsync(cancellationToken));
                                var authorization = await _authorizationService.AuthorizeAsync(User, Policies.CanModifyStoreSettings.Key);
                                if (!authorization.Succeeded)
                                {
                                    await websocketHelper.Send("{ \"error\": \"not-authorized\"}", cancellationToken);
                                    continue;
                                }
                                var psbt = PSBT.Parse(o["psbt"].Value<string>(), network.NBitcoinNetwork);
                                var derivationSettings = GetDerivationSchemeSettings(walletId);
                                derivationSettings.RebaseKeyPaths(psbt);
                                var signing = derivationSettings.GetSigningAccountKeySettings();
                                if (signing.GetRootedKeyPath()?.MasterFingerprint != fingerprint)
                                {
                                    await websocketHelper.Send("{ \"error\": \"wrong-wallet\"}", cancellationToken);
                                    continue;
                                }
                                try
                                {
                                    psbt = await device.SignPSBTAsync(psbt, cancellationToken);
                                }
                                catch (Hwi.HwiException ex) when (ex.ErrorCode == Hwi.HwiErrorCode.DeviceNotReady)
                                {
                                    await websocketHelper.Send("{ \"error\": \"need-pin\"}", cancellationToken);
                                    continue;
                                }
                                catch (Hwi.HwiException)
                                {
                                    await websocketHelper.Send("{ \"error\": \"user-reject\"}", cancellationToken);
                                    continue;
                                }
                                o = new JObject();
                                o.Add("psbt", psbt.ToBase64());
                                await websocketHelper.Send(o.ToString(), cancellationToken);
                                break;
                            case "ask-pin":
                                if (device == null)
                                {
                                    await websocketHelper.Send("{ \"error\": \"need-device\"}", cancellationToken);
                                    continue;
                                }
                                await device.PromptPinAsync(cancellationToken);
                                await websocketHelper.Send("{ \"info\": \"prompted, please input the pin\"}", cancellationToken);
                                o = JObject.Parse(await websocketHelper.NextMessageAsync(cancellationToken));
                                var pin = (int)o["pinCode"].Value<long>();
                                var passphrase = o["passphrase"].Value<string>();
                                device.Password = passphrase;
                                if (await device.SendPinAsync(pin, cancellationToken))
                                {
                                    await websocketHelper.Send("{ \"info\": \"the pin is correct\"}", cancellationToken);
                                }
                                else
                                {
                                    await websocketHelper.Send("{ \"error\": \"incorrect-pin\"}", cancellationToken);
                                    continue;
                                }
                                break;
                            case "ask-xpubs":
                                if (device == null)
                                {
                                    await websocketHelper.Send("{ \"error\": \"need-device\"}", cancellationToken);
                                    continue;
                                }
                                JObject result = new JObject();
                                var factory = network.NBXplorerNetwork.DerivationStrategyFactory;
                                var keyPath = new KeyPath("84'").Derive(network.CoinType).Derive(0, true);
                                BitcoinExtPubKey xpub = null;
                                try
                                {
                                    xpub = await device.GetXPubAsync(keyPath);
                                }
                                catch (Hwi.HwiException ex) when (ex.ErrorCode == Hwi.HwiErrorCode.DeviceNotReady)
                                {
                                    await websocketHelper.Send("{ \"error\": \"need-pin\"}", cancellationToken);
                                    continue;
                                }
                                if (fingerprint is null)
                                {
                                    fingerprint = (await device.GetXPubAsync(new KeyPath("44'"), cancellationToken)).ExtPubKey.ParentFingerprint;
                                }
                                result["fingerprint"] = fingerprint.Value.ToString();
                                var strategy = factory.CreateDirectDerivationStrategy(xpub, new DerivationStrategyOptions()
                                {
                                    ScriptPubKeyType = ScriptPubKeyType.Segwit
                                });
                                AddDerivationSchemeToJson("segwit", result, keyPath, xpub, strategy);
                                keyPath = new KeyPath("49'").Derive(network.CoinType).Derive(0, true);
                                xpub = await device.GetXPubAsync(keyPath);
                                strategy = factory.CreateDirectDerivationStrategy(xpub, new DerivationStrategyOptions()
                                {
                                    ScriptPubKeyType = ScriptPubKeyType.SegwitP2SH
                                });
                                AddDerivationSchemeToJson("segwitWrapped", result, keyPath, xpub, strategy);
                                keyPath = new KeyPath("44'").Derive(network.CoinType).Derive(0, true);
                                xpub = await device.GetXPubAsync(keyPath);
                                strategy = factory.CreateDirectDerivationStrategy(xpub, new DerivationStrategyOptions()
                                {
                                    ScriptPubKeyType = ScriptPubKeyType.Legacy
                                });
                                AddDerivationSchemeToJson("legacy", result, keyPath, xpub, strategy);
                                await websocketHelper.Send(result.ToString(), cancellationToken);
                                break;
                            case "ask-device":
                                var devices = (await hwi.EnumerateDevicesAsync(cancellationToken)).ToList();
                                device = devices.FirstOrDefault();
                                if (device == null)
                                {
                                    await websocketHelper.Send("{ \"error\": \"no-device\"}", cancellationToken);
                                    continue;
                                }
                                fingerprint = device.Fingerprint;
                                JObject json = new JObject();
                                json.Add("model", device.Model.ToString());
                                json.Add("fingerprint", device.Fingerprint?.ToString());
                                await websocketHelper.Send(json.ToString(), cancellationToken);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    JObject obj = new JObject();
                    obj.Add("error", "unknown-error");
                    obj.Add("details", ex.ToString());
                    try
                    {
                        await websocketHelper.Send(obj.ToString(), cancellationToken);
                    }
                    catch { }
                }
                finally
                {
                    await websocketHelper.DisposeAsync(cancellationToken);
                }
            }
            return new EmptyResult();
        }

        public StoreData CurrentStore
        {
            get
            {
                return HttpContext.GetStoreData();
            }
        }

        private DerivationSchemeSettings GetDerivationSchemeSettings(WalletId walletId)
        {
            var paymentMethod = CurrentStore
                            .GetSupportedPaymentMethods(Networks)
                            .OfType<DerivationSchemeSettings>()
                            .FirstOrDefault(p => p.PaymentId.PaymentType == Payments.PaymentTypes.BTCLike && p.PaymentId.CryptoCode == walletId.CryptoCode);
            return paymentMethod;
        }

        private void AddDerivationSchemeToJson(string propertyName, JObject result, KeyPath keyPath, BitcoinExtPubKey xpub, DerivationStrategyBase strategy)
        {
            result.Add(new JProperty(propertyName, new JObject()
            {
                new JProperty("strategy", strategy.ToString()),
                new JProperty("accountKey", xpub.ToString()),
                new JProperty("keyPath", keyPath.ToString()),
            }));
        }
    }
}
