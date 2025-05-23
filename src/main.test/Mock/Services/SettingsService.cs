using PKISharp.WACS.Configuration.Settings;
using System;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class MockSettingsService : Settings
    {
        public MockSettingsService()
        {
            BaseUri = new Uri("https://www.simple-acme.com/");
            ScheduledTask.RenewalDays = 55;
            ScheduledTask.RenewalMinimumValidDays = 10;
            Valid = true;
        }
    }
}
