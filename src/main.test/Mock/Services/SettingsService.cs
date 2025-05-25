using PKISharp.WACS.Configuration.Settings;
using System;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class MockSettingsService : InheritSettings
    {
        public MockSettingsService(Settings? settings = null) : base(settings ?? new Settings()
        {
            ScheduledTask = new ScheduledTaskSettings
            {
                RenewalDays = 55,
                RenewalMinimumValidDays = 10
            }
        })
        {
            BaseUri = new Uri("https://www.simple-acme.com/");
            Valid = true;
        }
    }
}
