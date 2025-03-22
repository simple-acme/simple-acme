using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class SecretService : ISecretService
    {
        private readonly List<Tuple<string, string>> _secrets;

        public SecretService()
        {
            _secrets =
            [
                new Tuple<string, string>("key1", "secret1"),
                new Tuple<string, string>("key2", "secret2")
            ];
        }

        public string Prefix => "mock";
        public Task DeleteSecret(string key) { _secrets.RemoveAll(x => x.Item1 == key); return Task.CompletedTask; }
        public Task<string?> GetSecret(string? identifier) => Task.FromResult(_secrets.FirstOrDefault(x => x.Item1 == identifier)?.Item2);
        public IEnumerable<string> ListKeys() => _secrets.Select(x => x.Item1);
        public Task PutSecret(string identifier, string secret)
        {
            var existing = _secrets.FirstOrDefault(x => x.Item1 == identifier);
            if (existing != null)
            {
                _ = _secrets.Remove(existing);
            }
            _secrets.Add(new Tuple<string, string>(identifier, secret));
            return Task.CompletedTask;
        }

        public Task Encrypt() => Task.CompletedTask;
    }
}
