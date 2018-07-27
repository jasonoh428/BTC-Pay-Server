﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using NBitcoin.DataEncoders;

namespace BTCPayServer.Payments.Lightning
{
    public enum LightningConnectionType
    {
        Charge,
        CLightning,
        LndREST,
        LndGRPC
    }
    public class LightningConnectionString
    {
        static Dictionary<string, LightningConnectionType> typeMapping;
        static Dictionary<LightningConnectionType, string> typeMappingReverse;
        static LightningConnectionString()
        {
            typeMapping = new Dictionary<string, LightningConnectionType>();
            typeMapping.Add("clightning", LightningConnectionType.CLightning);
            typeMapping.Add("charge", LightningConnectionType.Charge);
            typeMapping.Add("lnd-rest", LightningConnectionType.LndREST);
            typeMapping.Add("lnd-grpc", LightningConnectionType.LndGRPC);
            typeMappingReverse = new Dictionary<LightningConnectionType, string>();
            foreach (var kv in typeMapping)
            {
                typeMappingReverse.Add(kv.Value, kv.Key);
            }
        }
        public static bool TryParse(string str, bool supportLegacy, out LightningConnectionString connectionString)
        {
            return TryParse(str, supportLegacy, out connectionString, out var error);
        }
        public static bool TryParse(string str, bool supportLegacy, out LightningConnectionString connectionString, out string error)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (supportLegacy)
            {
                var parsed = TryParseLegacy(str, out connectionString, out error);
                if (!parsed)
                {
                    parsed = TryParseNewFormat(str, out connectionString, out error);
                }
                return parsed;
            }
            else
            {
                return TryParseNewFormat(str, out connectionString, out error);
            }
        }

        private static bool TryParseNewFormat(string str, out LightningConnectionString connectionString, out string error)
        {
            connectionString = null;
            error = null;
            var parts = str.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, string> keyValues = new Dictionary<string, string>();
            foreach (var part in parts.Select(p => p.Trim()))
            {
                var idx = part.IndexOf('=', StringComparison.OrdinalIgnoreCase);
                if (idx == -1)
                {
                    error = "The format of the connectionString should a list of key=value delimited by semicolon";
                    return false;
                }
                var key = part.Substring(0, idx).Trim().ToLowerInvariant();
                var value = part.Substring(idx + 1).Trim();
                if (keyValues.ContainsKey(key))
                {
                    error = $"Duplicate key {key}";
                    return false;
                }
                keyValues.Add(key, value);
            }

            var possibleTypes = String.Join(", ", typeMapping.Select(k => k.Key).ToArray());

            LightningConnectionString result = new LightningConnectionString();
            var type = Take(keyValues, "type");
            if (type == null)
            {
                error = $"The key 'type' is mandatory, possible values are {possibleTypes}";
                return false;
            }

            if (!typeMapping.TryGetValue(type.ToLowerInvariant(), out var connectionType))
            {
                error = $"The key 'type' is invalid, possible values are {possibleTypes}";
                return false;
            }

            result.ConnectionType = connectionType;

            switch (connectionType)
            {
                case LightningConnectionType.Charge:
                    {
                        var server = Take(keyValues, "server");
                        if (server == null)
                        {
                            error = $"The key 'server' is mandatory for charge connection strings";
                            return false;
                        }

                        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri)
                            || (uri.Scheme != "http" && uri.Scheme != "https"))
                        {
                            error = $"The key 'server' should be an URI starting by http:// or https://";
                            return false;
                        }

                        parts = uri.UserInfo.Split(':');
                        if (!string.IsNullOrEmpty(uri.UserInfo) && parts.Length == 2)
                        {
                            result.Username = parts[0];
                            result.Password = parts[1];
                        }
                        else
                        {
                            var apiToken = Take(keyValues, "api-token");
                            if (apiToken == null)
                            {
                                error = "The key 'api-token' is not found";
                                return false;
                            }
                            result.Username = "api-token";
                            result.Password = apiToken;
                        }
                        result.BaseUri = new UriBuilder(uri) { UserName = "", Password = "" }.Uri;
                    }
                    break;
                case LightningConnectionType.CLightning:
                    {
                        var server = Take(keyValues, "server");
                        if (server == null)
                        {
                            error = $"The key 'server' is mandatory for charge connection strings";
                            return false;
                        }

                        if (server.StartsWith("//", StringComparison.OrdinalIgnoreCase))
                            server = "unix:" + str;
                        else if (server.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                            server = "unix:/" + str;

                        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri)
                            || (uri.Scheme != "tcp" && uri.Scheme != "unix"))
                        {
                            error = $"The key 'server' should be an URI starting by tcp:// or unix:// or a path to the 'lightning-rpc' unix socket";
                            return false;
                        }
                        result.BaseUri = uri;
                    }
                    break;
                case LightningConnectionType.LndREST:
                case LightningConnectionType.LndGRPC:
                    {
                        var server = Take(keyValues, "server");
                        if (server == null)
                        {
                            error = $"The key 'server' is mandatory for lnd connection strings";
                            return false;
                        }
                        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri)
                            || (uri.Scheme != "http" && uri.Scheme != "https"))
                        {
                            error = $"The key 'server' should be an URI starting by http:// or https://";
                            return false;
                        }
                        parts = uri.UserInfo.Split(':');
                        if (!string.IsNullOrEmpty(uri.UserInfo) && parts.Length == 2)
                        {
                            result.Username = parts[0];
                            result.Password = parts[1];
                        }
                        result.BaseUri = new UriBuilder(uri) { UserName = "", Password = "" }.Uri;

                        var macaroon = Take(keyValues, "macaroon");
                        if (macaroon != null)
                        {
                            try
                            {
                                result.Macaroon = Encoder.DecodeData(macaroon);
                            }
                            catch
                            {
                                error = $"The key 'macaroon' format should be in hex";
                                return false;
                            }
                        }

                        var macaroonFilePath = Take(keyValues, "macaroonfilepath");
                        if (macaroonFilePath != null)
                        {
                            if (macaroon != null)
                            {
                                error = $"The key 'macaroon' is already specified";
                                return false;
                            }
                            if (!macaroonFilePath.EndsWith(".macaroon", StringComparison.OrdinalIgnoreCase))
                            {
                                error = $"The key 'macaroonfilepath' should point to a .macaroon file";
                                return false;
                            }
                            result.MacaroonFilePath = macaroonFilePath;
                        }

                        string securitySet = null;
                        var certthumbprint = Take(keyValues, "certthumbprint");
                        if (certthumbprint != null)
                        {
                            try
                            {
                                var bytes = Encoders.Hex.DecodeData(certthumbprint.Replace(":", string.Empty, StringComparison.OrdinalIgnoreCase));
                                if (bytes.Length != 32)
                                {
                                    error = $"The key 'certthumbprint' has invalid length: it should be the SHA256 of the PEM format of the certificate (32 bytes)";
                                    return false;
                                }
                                result.CertificateThumbprint = bytes;
                            }
                            catch
                            {
                                error = $"The key 'certthumbprint' has invalid format: it should be the SHA256 of the PEM format of the certificate";
                                return false;
                            }
                            securitySet = "certthumbprint";
                        }

                        var allowinsecureStr = Take(keyValues, "allowinsecure");

                        if (allowinsecureStr != null)
                        {
                            var allowedValues = new[] { "true", "false" };
                            if (!allowedValues.Any(v => v.Equals(allowinsecureStr, StringComparison.OrdinalIgnoreCase)))
                            {
                                error = $"The key 'allowinsecure' should be true or false";
                                return false;
                            }

                            bool allowInsecure = allowinsecureStr.Equals("true", StringComparison.OrdinalIgnoreCase);
                            if (securitySet != null && allowInsecure)
                            {
                                error = $"The key 'allowinsecure' conflict with '{securitySet}'";
                                return false;
                            }
                            result.AllowInsecure = allowInsecure;
                        }

                        if (!result.AllowInsecure && result.BaseUri.Scheme == "http")
                        {
                            error = $"The key 'allowinsecure' is false, but server's Uri is not using https";
                            return false;
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException(connectionType.ToString());
            }

            if (keyValues.Count != 0)
            {
                error = $"Unknown keys ({String.Join(", ", keyValues.Select(k => k.Key).ToArray())})";
                return false;
            }

            connectionString = result;
            return true;
        }

        public LightningConnectionString Clone()
        {
            LightningConnectionString.TryParse(this.ToString(), false, out var result);
            return result;
        }

        private static string Take(Dictionary<string, string> keyValues, string key)
        {
            if (keyValues.TryGetValue(key, out var v))
                keyValues.Remove(key);
            return v;
        }

        private static bool TryParseLegacy(string str, out LightningConnectionString connectionString, out string error)
        {
            if (str.StartsWith('/'))
                str = "unix:" + str;
            var result = new LightningConnectionString();
            connectionString = null;
            error = null;

            Uri uri;
            if (!Uri.TryCreate(str, UriKind.Absolute, out uri))
            {
                error = "Invalid URL";
                return false;
            }

            var supportedDomains = new string[] { "unix", "tcp", "http", "https" };
            if (!supportedDomains.Contains(uri.Scheme))
            {
                var protocols = String.Join(",", supportedDomains);
                error = $"The url support the following protocols {protocols}";
                return false;
            }
            if (uri.Scheme == "unix")
            {
                str = uri.AbsoluteUri.Substring("unix:".Length);
                while (str.Length >= 1 && str[0] == '/')
                {
                    str = str.Substring(1);
                }
                uri = new Uri("unix://" + str, UriKind.Absolute);
                result.ConnectionType = LightningConnectionType.CLightning;
            }

            if (uri.Scheme == "tcp")
                result.ConnectionType = LightningConnectionType.CLightning;

            if (uri.Scheme == "http" || uri.Scheme == "https")
            {
                var parts = uri.UserInfo.Split(':');
                if (string.IsNullOrEmpty(uri.UserInfo) || parts.Length != 2)
                {
                    error = "The url is missing user and password";
                    return false;
                }
                result.Username = parts[0];
                result.Password = parts[1];
                result.ConnectionType = LightningConnectionType.Charge;
            }
            else if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                error = "The url should not have user information";
                return false;
            }
            result.BaseUri = new UriBuilder(uri) { UserName = "", Password = "" }.Uri;
            result.IsLegacy = true;
            connectionString = result;
            return true;
        }

        public LightningConnectionString()
        {

        }

        public string Username { get; set; }
        public string Password { get; set; }
        public Uri BaseUri { get; set; }
        public bool IsLegacy { get; private set; }

        public LightningConnectionType ConnectionType
        {
            get;
            set;
        }
        public byte[] Macaroon { get; set; }
        public string MacaroonFilePath { get; set; }
        public byte[] CertificateThumbprint { get; set; }
        public bool AllowInsecure { get; set; }

        public Uri ToUri(bool withCredentials)
        {
            if (withCredentials)
            {
                return new UriBuilder(BaseUri) { UserName = Username ?? "", Password = Password ?? "" }.Uri;
            }
            else
            {
                return BaseUri;
            }
        }
        static NBitcoin.DataEncoders.DataEncoder Encoder = NBitcoin.DataEncoders.Encoders.Hex;
        public override string ToString()
        {
            var type = typeMappingReverse[ConnectionType];
            StringBuilder builder = new StringBuilder();
            builder.Append($"type={type}");
            switch (ConnectionType)
            {
                case LightningConnectionType.Charge:
                    if (Username == null || Username == "api-token")
                    {
                        builder.Append($";server={BaseUri};api-token={Password}");
                    }
                    else
                    {
                        builder.Append($";server={ToUri(true)}");
                    }
                    break;
                case LightningConnectionType.CLightning:
                    builder.Append($";server={BaseUri}");
                    break;
                case LightningConnectionType.LndREST:
                case LightningConnectionType.LndGRPC:
                    if (Username == null)
                    {
                        builder.Append($";server={BaseUri}");
                    }
                    else
                    {
                        builder.Append($";server={ToUri(true)}");
                    }
                    if (Macaroon != null)
                    {
                        builder.Append($";macaroon={Encoder.EncodeData(Macaroon)}");
                    }
                    if (MacaroonFilePath != null)
                    {
                        builder.Append($";macaroonfilepath={MacaroonFilePath}");
                    }
                    if (CertificateThumbprint != null)
                    {
                        builder.Append($";certthumbprint={Encoders.Hex.EncodeData(CertificateThumbprint)}");
                    }
                    if (AllowInsecure)
                    {
                        builder.Append($";allowinsecure=true");
                    }
                    break;
                default:
                    throw new NotSupportedException(type);
            }
            return builder.ToString();
        }
    }
}
