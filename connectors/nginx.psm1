Set-StrictMode -Version Latest

function New-NotImplementedError {
    param([string]$Connector,[string]$Step)
    $msg = "$Connector connector is not implemented for step '$Step'."
    $ex = New-Object System.NotImplementedException($msg)
    $err = New-Object System.Management.Automation.ErrorRecord($ex, ("CERTIFICAAT.CONNECTOR.NOT_IMPLEMENTED.{0}.{1}" -f $Connector.ToUpperInvariant(), $Step.ToUpperInvariant()), [System.Management.Automation.ErrorCategory]::NotImplemented, $null)
    throw $err
}

function Invoke-StubProbe { param([hashtable]$Context,[string]$Connector) New-NotImplementedError -Connector $Connector -Step 'Probe' }
function Invoke-StubDeploy { param([hashtable]$Context,[string]$Connector) New-NotImplementedError -Connector $Connector -Step 'Deploy' }
function Invoke-StubBind { param([hashtable]$Context,[string]$Connector) New-NotImplementedError -Connector $Connector -Step 'Bind' }
function Invoke-StubActivate { param([hashtable]$Context,[string]$Connector) New-NotImplementedError -Connector $Connector -Step 'Activate' }
function Invoke-StubVerify { param([hashtable]$Context,[string]$Connector) New-NotImplementedError -Connector $Connector -Step 'Verify' }
function Invoke-StubRollback { param([hashtable]$Context,[string]$Connector) New-NotImplementedError -Connector $Connector -Step 'Rollback' }
function Invoke-NginxProbe { param([hashtable]$Context) Invoke-StubProbe -Context $Context -Connector 'nginx' }
function Invoke-NginxDeploy { param([hashtable]$Context) Invoke-StubDeploy -Context $Context -Connector 'nginx' }
function Invoke-NginxBind { param([hashtable]$Context) Invoke-StubBind -Context $Context -Connector 'nginx' }
function Invoke-NginxActivate { param([hashtable]$Context) Invoke-StubActivate -Context $Context -Connector 'nginx' }
function Invoke-NginxVerify { param([hashtable]$Context) Invoke-StubVerify -Context $Context -Connector 'nginx' }
function Invoke-NginxRollback { param([hashtable]$Context) Invoke-StubRollback -Context $Context -Connector 'nginx' }

Export-ModuleMember -Function Invoke-NginxProbe,Invoke-NginxDeploy,Invoke-NginxBind,Invoke-NginxActivate,Invoke-NginxVerify,Invoke-NginxRollback
