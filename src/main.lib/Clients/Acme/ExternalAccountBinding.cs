﻿using ACMESharp;
using ACMESharp.Crypto;
using ACMESharp.Crypto.JOSE;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using static ACMESharp.Crypto.JOSE.JwsHelper;

namespace PKISharp.WACS.Clients.Acme
{
    internal class ExternalAccountBinding(string algorithm, string accountKey, string keyIdentifier, string key, string url)
    {
        public string AccountKey { get; set; } = accountKey;
        public string Algorithm { get; set; } = algorithm;
        public string Key { get; set; } = key;
        public string KeyIdentifier { get; set; } = keyIdentifier;
        public string Url { get; set; } = url;

        public JwsSignedPayload Payload()
        {
            var ph = new ProtectedHeader
            {
                Algorithm = Algorithm,
                KeyIdentifier = KeyIdentifier,
                Url = Url
            };
            var protectedHeader = JsonSerializer.Serialize(ph, AcmeJson.Insensitive.ProtectedHeader);
            return SignFlatJsonAsObject(Sign, AccountKey, protectedHeader);
        }

        public byte[] Sign(byte[] input)
        {
            var keyBytes = Base64Tool.UrlDecode(Key);
            switch (Algorithm)
            {
                case "HS256":
                    {
                        using var hmac = new HMACSHA256(keyBytes);
                        return hmac.ComputeHash(input);
                    }
                case "HS384":
                    {
                        using var hmac = new HMACSHA384(keyBytes);
                        return hmac.ComputeHash(input);
                    }
                case "HS512":
                    {
                        using var hmac = new HMACSHA512(keyBytes);
                        return hmac.ComputeHash(input);
                    }
            }
            throw new InvalidOperationException($"Unsupported algorithm {Algorithm}");
        }
    }
}
