# CloseIIS80FWrule.ps1 - Closes port 80 for HTTP traffic by blocking the firewall rule

# Define the specific rule name for HTTP traffic
$ruleName = "World Wide Web Services (HTTP Traffic-In)"

# Find the firewall rule by name
$rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue

# Check if the rule exists and ensure the action is set to 'Block'
if (-not $rule) {
    Write-Output "Firewall rule '$ruleName' not found."
} else {
    Write-Output "Setting the firewall rule '$ruleName' to 'Block' to close port 80..."

    # If the rule isn't already set to 'Block', update it
    if ($rule.Action -ne "Block") {
        Set-NetFirewallRule -Name $rule.Name -Action Block
        Write-Output "Firewall rule '$ruleName' set to 'Block'."
    } else {
        Write-Output "Rule '$ruleName' is already set to 'Block'."
    }
}

# Instructions for configuring this script in win-acme:
# To ensure this script runs as the post-execution step in win-acme, you need to update the settings.json file.
# 1. Open the 'settings.json' file located in the win-acme configuration folder (usually C:\tools\win-acme\).
# 2. Locate the "Execution" section and set the DefaultPostExecutionScript path as follows:
#    "DefaultPostExecutionScript": "C:\\tools\\win-acme\\Scripts\\CloseIIS80FWrule.ps1"
# 3. This script will run after certificate renewal to close port 80 by blocking the HTTP traffic rule.
