using Autofac.Core;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS
{
    internal class RenewalDescriber(
        ISharingLifetimeScope container,
        IPluginService plugin,
        ISettings settings,
        IInputService input,
        ILogService log,
        DueDateStaticService dueDate,
        IAutofacBuilder autofacBuilder)
    {
        private readonly DueDateStaticService _dueDate = dueDate;

        /// <summary>
        /// Write the command line that can be used to create 
        /// </summary>
        /// <param name="renewal"></param>
        public string Describe(Renewal renewal)
        {
            // List the command line that may be used to (re)create this renewal
            var args = new Dictionary<string, string?>();
            void addArgs(PluginOptions p)
            {
                var arguments = p.Describe(container, autofacBuilder, plugin);
                foreach (var arg in arguments)
                {
                    var meta = arg.Key;
                    if (!args.ContainsKey(meta.ArgumentName))
                    {
                        var value = arg.Value;
                        if (value != null)
                        {
                            var add = true;
                            if (value is ProtectedString protectedString)
                            {
                                value = protectedString.Value?.StartsWith(SecretServiceManager.VaultPrefix) ?? false ? protectedString.Value : (object)"*******";
                            }
                            else if (value is string singleString)
                            {
                                value = meta.Secret ? "*******" : Escape(singleString);
                            }
                            else if (value is List<string> listString)
                            {
                                value = Escape(string.Join(",", listString));
                            }
                            else if (value is List<int> listInt)
                            {
                                value = string.Join(",", listInt);
                            }
                            else if (value is List<long> listLong)
                            {
                                value = string.Join(",", listLong);
                            }
                            else if (value is bool boolean)
                            {
                                value = boolean ? null : add = false;
                            }
                            if (add)
                            {
                                args.Add(meta.ArgumentName, value?.ToString());
                            }
                        }
                    }
                }
            }

            args.Add("source", plugin.GetPlugin(renewal.TargetPluginOptions).Trigger.ToLower());
            addArgs(renewal.TargetPluginOptions);
            var validationPlugin = plugin.GetPlugin(renewal.ValidationPluginOptions);
            var validationName = validationPlugin.Trigger.ToLower();
            if (!string.Equals(validationName, settings.Validation.DefaultValidation ?? "selfhosting", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("validation", validationName);
            }
            addArgs(renewal.ValidationPluginOptions);
            if (renewal.OrderPluginOptions != null)
            {
                var orderName = plugin.GetPlugin(renewal.OrderPluginOptions).Trigger.ToLower();
                if (!string.Equals(orderName, settings.Order.DefaultPlugin ?? "single", StringComparison.OrdinalIgnoreCase))
                {
                    args.Add("order", orderName);
                }
                addArgs(renewal.OrderPluginOptions);
            }
            if (renewal.CsrPluginOptions != null)
            {
                var csrName = plugin.GetPlugin(renewal.CsrPluginOptions).Trigger.ToLower();
                if (!string.Equals(csrName, settings.Csr.DefaultCsr ?? "rsa", StringComparison.OrdinalIgnoreCase))
                {
                    args.Add("csr", csrName);
                }
                addArgs(renewal.CsrPluginOptions);
            }
            var storeNames = string.Join(",", renewal.StorePluginOptions.Select(plugin.GetPlugin).Select(x => x.Trigger.ToLower()));
            if (!string.Equals(storeNames, settings.Store.DefaultStore, StringComparison.OrdinalIgnoreCase))
            {
                args.Add("store", storeNames);
            }
            foreach (var so in renewal.StorePluginOptions)
            {
                addArgs(so);
            }
            var installationNames = string.Join(",", renewal.InstallationPluginOptions.Select(plugin.GetPlugin).Select(x => x.Trigger.ToLower()));
            if (!string.Equals(installationNames, settings.Installation.DefaultInstallation ?? "none", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("installation", installationNames);
            }
            foreach (var so in renewal.InstallationPluginOptions)
            {
                addArgs(so);
            }
            if (!string.IsNullOrWhiteSpace(renewal.FriendlyName))
            {
                args.Add("friendlyname", renewal.FriendlyName);
            }
            if (!string.IsNullOrWhiteSpace(renewal.Account))
            {
                args.Add("account", renewal.Account);
            }
            return "wacs.exe " + string.Join(" ", args.Select(a => $"--{a.Key.ToLower()} {a.Value}".Trim()));
        }

        /// <summary>
        /// Escape command line argument
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string Escape(string value)
        {
            if (value.Contains(' ') || value.Contains('"'))
            {
                return $"\"{value.Replace("\"", "\\\"")}\"";
            }
            return value;
        }

        /// <summary>
        /// Show renewal details on screen
        /// </summary>
        /// <param name="renewal"></param>
        public void Show(Renewal renewal)
        {
            try
            {
                input.CreateSpace();
                input.Show("Id", renewal.Id);
                input.Show("File", $"{renewal.Id}.renewal.json");
                if (string.IsNullOrWhiteSpace(renewal.Account))
                {
                    input.Show("Account", "Default account");
                }
                else
                {
                    input.Show("Account", $"Named account: {renewal.Account}");
                }
                if (string.IsNullOrWhiteSpace(renewal.FriendlyName))
                {
                    input.Show("Auto-FriendlyName", renewal.LastFriendlyName);
                }
                else
                {
                    input.Show("FriendlyName", renewal.FriendlyName);
                }
                input.Show(".pfx password", renewal.PfxPassword?.Value);
                var expires = renewal.History.Where(x => x.Success == true).LastOrDefault()?.ExpireDate;
                if (expires == null)
                {
                    input.Show("Expires", "Unknown");
                }
                else
                {
                    input.Show("Expires", input.FormatDate(expires.Value));
                }
                var dueDate = _dueDate.DueDate(renewal);
                if (dueDate == null)
                {
                    input.Show("Renewal due", "Now");
                }
                else
                {
                    if (dueDate.Start != dueDate.End)
                    {
                        input.Show("Renewal due start", input.FormatDate(dueDate.Start));
                        input.Show("Renewal due end", input.FormatDate(dueDate.End));
                    }
                    else
                    {
                        input.Show("Renewal due", input.FormatDate(dueDate.End));
                    }
                }
                input.Show("Renewed", $"{renewal.History.Where(x => x.Success == true).Count()} times");
                input.Show("Command", Describe(renewal));

                input.CreateSpace();
                input.Show($"Plugins");
                input.CreateSpace();

                renewal.TargetPluginOptions.Show(input, plugin);
                renewal.ValidationPluginOptions.Show(input, plugin);
                renewal.OrderPluginOptions?.Show(input, plugin);
                renewal.CsrPluginOptions?.Show(input, plugin);
                foreach (var ipo in renewal.StorePluginOptions)
                {
                    ipo.Show(input, plugin);
                }
                foreach (var ipo in renewal.InstallationPluginOptions)
                {
                    ipo.Show(input, plugin);
                }
                input.CreateSpace();
                input.Show($"Orders");
                input.CreateSpace();
                var orders = _dueDate.CurrentOrders(renewal);
                var i = 0;
                foreach (var order in orders)
                {
                    input.Show($"Order {++i}/{orders.Count}", order.Key);
                    input.Show($"Renewed", $"{order.RenewCount} times", 1);
                    input.Show($"Last thumbprint", order.LastThumbprint, 1);
                    if (order.LastRenewal != null)
                    {
                        input.Show($"Last date", input.FormatDate(order.LastRenewal.Value), 1);
                    }
                    var orderDue = order.DueDate;
                    if (orderDue.Start != orderDue.End)
                    {
                        input.Show("Next start", input.FormatDate(orderDue.Start), 1);
                        input.Show("Next end", input.FormatDate(orderDue.End), 1);
                    }
                    else
                    {
                        input.Show("Next due", input.FormatDate(orderDue.End), 1);
                    }
                    if (order.Revoked)
                    {
                        input.Show($"Revoked", "true", 1);
                    }
                    input.CreateSpace();
                }

                input.Show($"History");
                input.CreateSpace();

                var historyLimit = 10;
                var h = renewal.History.Count;
                foreach (var history in renewal.History.AsEnumerable().Reverse().Take(historyLimit))
                {
                    input.Show($"History {h--}/{renewal.History.Count}");
                    input.Show($"Date", input.FormatDate(history.Date), 1);
                    foreach (var order in history.OrderResults)
                    {
                        input.Show($"Order", order.Name, 1);
                        if (order.Success == true)
                        {
                            input.Show($"Success", "true", 2);
                            input.Show($"Thumbprint", order.Thumbprint, 2);
                        }
                        if (order.Missing == true)
                        {
                            input.Show($"Missing", "true", 2);
                        }
                        if (order.Revoked == true)
                        {
                            input.Show($"Revoked", "true", 2);
                        }
                        if (order.ErrorMessages != null && order.ErrorMessages.Count != 0)
                        {
                            input.Show($"Errors", string.Join(", ", order.ErrorMessages.Select(x => x.ReplaceNewLines())), 2);
                        }
                    }
                    input.CreateSpace();
                }

            }
            catch (Exception ex)
            {
                log.Error(ex, "Unable to list details for target");
            }
        }
    }
}