using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal class ManualDns(
        LookupClientProvider dnsClient,
        ILogService log,
        IInputService input,
        ISettingsService settings) : DnsValidation<Manual>(dnsClient, log, settings)
    {
        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            input.CreateSpace();
            input.Show("Domain", record.Context.Identifier);
            input.Show("Record", record.Authority.Domain);
            input.Show("Type", "TXT");
            input.Show("Content", $"\"{record.Value}\"");
            input.Show("Note", "Some DNS managers add quotes automatically. A single set is needed.");
            if (!await input.Wait("Please press <Enter> after you've created and verified the record"))
            {
                _log.Warning("User aborted");
                return false;
            }

            if (!_settings.Validation.PreValidateDns)
            {
                return true;
            }

            // Pre-pre-validate, allowing the manual user to correct mistakes
            while (true)
            {
                if (await PreValidate(record))
                {
                    return true;
                }
                else
                {
                    input.CreateSpace();
                    input.Show(null, value: "The correct record has not yet been found by the local resolver. That means it's likely the validation attempt will fail, or your DNS provider needs a little more time to publish and synchronize the changes.");
                    var options = new List<Choice<bool?>>
                    {
                        Choice.Create<bool?>(null, "Retry check"),
                        Choice.Create<bool?>(true, "Ignore and continue"),
                        Choice.Create<bool?>(false, "Abort")
                    };
                    var chosen = await input.ChooseFromMenu("How would you like to proceed?", options);
                    if (chosen != null)
                    {
                        return chosen.Value;
                    }
                }
            }
        }

        public override Task DeleteRecord(DnsValidationRecord record)
        {
            input.CreateSpace();
            input.Show("Domain", record.Context.Identifier);
            input.Show("Record", record.Authority.Domain);
            input.Show("Type", "TXT");
            input.Show("Content", $"\"{record.Value}\"");
            input.Wait("Please press <Enter> after you've deleted the record");
            return Task.CompletedTask;
        }
    }
}
