﻿using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class UserRoleService : IUserRoleService
    {
        public bool AllowCertificateStore => true;

        public State IISState => State.EnabledState();

        public bool AllowAutoRenew => true;
    }
}
