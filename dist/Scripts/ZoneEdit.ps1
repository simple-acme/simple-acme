<#
.SYNOPSIS
Add or remove a DNS TXT record in Zone Edit

.DESCRIPTION
Note that this script is intended to be run via the install script plugin from win-acme via the batch script wrapper. 
As such, we use positional parameters to avoid issues with using a dash in the cmd line. 

This script was copied and modified from the win-acme repository, which was originally copied from the Posh-ACME repository.
Please reference their license terms for use/modification:  https://github.com/rmbolger/Posh-ACME/blob/main/LICENSE

Credit for the original script goes to RMBolger, Thanks!

Zone Edit API Docs
https://support.zoneedit.com/en/knowledgebase/article/dynamic-dns
https://support.zoneedit.com/en/knowledgebase/article/changes-to-dynamic-dns

.PARAMETER RecordName
The fully qualified name of the TXT record.

.PARAMETER TxtValue
The value of the TXT record.

.PARAMETER ZEUser
The ZoneEdit Username.

.PARAMETER ZEKey
The ZoneEdit API Key.

.PARAMETER ExtraParams
This parameter can be ignored and is only used to prevent errors when splatting with more parameters than this function supports.

.EXAMPLE 
ZoneEdit.ps1 create {RecordName} {Token} {ZEUser} {ZEKey}
ZoneEdit.ps1 delete {RecordName} {Token} {ZEUser} {ZEKey}

create {RecordName} {Token} {vault://json/zeuser} {vault://json/zeauthtoken}
delete {RecordName} {Token} {vault://json/zeuser} {vault://json/zeauthtoken}
#>

param(
	[string]$Task,
	[string]$RecordName,
	[string]$TxtValue,
	[string]$ZEUser,
	[string]$ZEKey
)

<#
.SYNOPSIS
    Add a DNS TXT record to ZoneEdit.

.DESCRIPTION
    Add a DNS TXT record to ZoneEdit.

.PARAMETER RecordName
    The fully qualified name of the TXT record.

.PARAMETER TxtValue
    The value of the TXT record.

.PARAMETER ZEUser
    The ZoneEdit Username.

.PARAMETER ZEKey
    The ZoneEdit API Key.

.PARAMETER ExtraParams
    This parameter can be ignored and is only used to prevent errors when splatting with more parameters than this function supports.

.EXAMPLE
    Add-DnsTxt '_acme-challenge.example.com' 'txtvalue' -ZEUser 'xxxxxxxx' -ZEKey 'xxxxxxxx'

    Adds a TXT record for the specified site with the specified value.
#>
Function Add-DnsTxt {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory,Position=0)]
        [string]$RecordName,
        [Parameter(Mandatory,Position=1)]
        [string]$TxtValue,
        [Parameter(Mandatory,Position=2)]
        [string]$ZEUser,
        [Parameter(Mandatory,Position=3)]
        [string]$ZEKey,
        [Parameter(ValueFromRemainingArguments)]
        $ExtraParams
    )

    # set the API base
    $apiBase = "https://dynamic.zoneedit.com"

    # create the basic auth header
    $encodedCreds = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($ZEUser):$($ZEKey)"))
    $Headers = @{ Authorization = "Basic $encodedCreds" }

    # send the delete request
    Write-Output "Adding a TXT record for $RecordName with value $TxtValue"
    Invoke-RestMethod "$apiBase/txt-create.php?host=$RecordName&rdata=$TxtValue" -Headers $Headers

}

<#
.SYNOPSIS
    Remove a DNS TXT record to ZoneEdit.

.DESCRIPTION
    Remove a DNS TXT record to ZoneEdit.

.PARAMETER RecordName
    The fully qualified name of the TXT record.

.PARAMETER TxtValue
    The value of the TXT record.

.PARAMETER ZEUser
    The ZoneEdit API Token.

.PARAMETER ZEKey
    The ZoneEdit API Key.

.PARAMETER ExtraParams
    This parameter can be ignored and is only used to prevent errors when splatting with more parameters than this function supports.

.EXAMPLE
    Remove-DnsTxt '_acme-challenge.example.com' 'txtvalue' -ZEUser 'xxxxxxxx' -ZEKey 'xxxxxxxx'

    Removes a TXT record for the specified site with the specified value.
#>
Function Remove-DnsTxt {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory,Position=0)]
        [string]$RecordName,
        [Parameter(Mandatory,Position=1)]
        [string]$TxtValue,
        [Parameter(Mandatory,Position=2)]
        [string]$ZEUser,
        [Parameter(Mandatory,Position=3)]
        [string]$ZEKey,
        [Parameter(ValueFromRemainingArguments)]
        $ExtraParams
    )

    # set the API base
    $apiBase = "https://dynamic.zoneedit.com"

    # create the basic auth header
    $encodedCreds = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($ZEUser):$($ZEKey)"))
    $Headers = @{ Authorization = "Basic $encodedCreds" }

    # send the delete request
    Write-Output "Removing TXT record for $RecordName with value $TxtValue"
    Invoke-RestMethod "$apiBase/txt-delete.php?host=$RecordName&rdata=$TxtValue" -Headers $Headers
}

if ($Task -eq 'create'){
	Add-DnsTxt $RecordName $TxtValue $ZEUser $ZEKey
}

if ($Task -eq 'delete'){
	Remove-DnsTxt $RecordName $TxtValue $ZEUser $ZEKey
}
