using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;

namespace PKISharp.WACS.Services
{
    internal class TargetValidator(ILogService log, ISettingsService settings)
    {
        public bool IsValid(Target target, bool validateMax = true)
        {
            var ret = target.GetIdentifiers(true);
            if (validateMax)
            {
                var max = settings.Acme.MaxDomains ?? 100;
                if (ret.Count > max)
                {
                    log.Error($"Too many identifiers in a single certificate. The maximum is {max} identifiers.", max);
                    return false;
                }
            }
            if (ret.Count == 0)
            {
                log.Error("No valid identifiers provided.");
                return false;
            }
            if (target.CommonName != null)
            {
                if (!ret.Contains(target.CommonName))
                {
                    log.Error("Common name not contained in SAN list.");
                    return false;
                }
                if (target.CommonName.Value.Length > Constants.MaxCommonName)
                {
                    log.Error("Common name too long (max {max} chars).", Constants.MaxCommonName);
                    return false;
                }
            }
            return true;
        }
    }
}