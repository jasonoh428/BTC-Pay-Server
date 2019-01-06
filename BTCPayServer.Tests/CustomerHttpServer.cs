﻿using Microsoft.AspNetCore.Hosting;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Threading.Channels;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace BTCPayServer.Tests
{
    public class CustomServer : IDisposable
    {
        IWebHost _Host = null;
        CancellationTokenSource _Closed = new CancellationTokenSource();
        Channel<JObject> _Requests = Channel.CreateUnbounded<JObject>();
        public CustomServer()
        { 
            var port = Utils.FreeTcpPort();
            _Host = new WebHostBuilder()
                .Configure(app =>
                {
                    app.Run(req =>
                    {
                        _Requests.Writer.WriteAsync(JsonConvert.DeserializeObject<JObject>(new StreamReader(req.Request.Body).ReadToEnd()), _Closed.Token);
                        req.Response.StatusCode = 200;
                        return Task.CompletedTask;
                    });
                })
                .UseKestrel()
                .UseUrls("http://127.0.0.1:" + port)
                .Build();
            _Host.Start();
        }

        public Uri GetUri()
        {
            return new Uri(_Host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First());
        }

        public async Task<JObject> GetNextRequest()
        {
            using (CancellationTokenSource cancellation = new CancellationTokenSource(2000000))
            {
                try
                {
                    JObject req = null;
                    while(!await _Requests.Reader.WaitToReadAsync(cancellation.Token) || 
                        !_Requests.Reader.TryRead(out req))
                    {

                    }
                    return req;
                }
                catch (TaskCanceledException)
                {
                    throw new Xunit.Sdk.XunitException("Callback to the webserver was expected, check if the callback url is accessible from internet");
                }
            }
        }

        public void Dispose()
        {
            _Closed.Cancel();
            _Host.Dispose();
        }
    }
}
