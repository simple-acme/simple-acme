# OpenIIS80FWrule.ps1 - Opens port 80 for HTTP traffic by enabling the firewall rule

# Define the specific rule name for HTTP traffic
$ruleName = "World Wide Web Services (HTTP Traffic-In)"

# Find the firewall rule by name
$rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue

# Check if the rule exists and ensure the action is set to 'Allow'
if (-not $rule) {
    Write-Output "Firewall rule '$ruleName' not found."
} else {
    Write-Output "Setting the firewall rule '$ruleName' to 'Allow' for HTTP traffic..."

    # If the rule isn't already set to 'Allow', update it
    if ($rule.Action -ne "Allow") {
        Set-NetFirewallRule -Name $rule.Name -Action Allow
        Write-Output "Firewall rule '$ruleName' set to 'Allow'."
    } else {
        Write-Output "Rule '$ruleName' is already set to 'Allow'."
    }
}

# Instructions for configuring this script in win-acme:
# To ensure this script runs as the pre-execution step in win-acme, you need to update the settings.json file.
# 1. Open the 'settings.json' file located in the win-acme configuration folder (usually C:\tools\win-acme\).
# 2. Locate the "Execution" section and set the DefaultPreExecutionScript path as follows:
#    "DefaultPreExecutionScript": "C:\\tools\\win-acme\\Scripts\\OpenIIS80FWrule.ps1"
# 3. This script will run before certificate renewal to open port 80 for the HTTP-01 challenge.
