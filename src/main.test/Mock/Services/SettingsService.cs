﻿using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Services;
using System;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class MockSettingsService : ISettings
    {
        public Uri BaseUri => new("https://www.simple-acme.com/");
        public UiSettings UI => new();
        public AcmeSettings Acme => new();
        public ExecutionSettings Execution => new();
        public ProxySettings Proxy => new();
        public CacheSettings Cache => new();
        public ScheduledTaskSettings ScheduledTask => new() { RenewalDays = 55, RenewalMinimumValidDays = 10 };
        public NotificationSettings Notification => new();
        public SecuritySettings Security => new();
        public ClientSettings Client => new();
        public SourceSettings Source => new();
        public ValidationSettings Validation => new();
        public OrderSettings Order => new();
        public CsrSettings Csr => new();
        public StoreSettings Store => new();
        public InstallationSettings Installation => new();
        public ScriptSettings Script => new();
        public SecretsSettings Secrets => new();
        public bool Valid => true;
    }
}
