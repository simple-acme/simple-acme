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
function Invoke-HaproxyProbe { param([hashtable]$Context) Invoke-StubProbe -Context $Context -Connector 'haproxy' }
function Invoke-HaproxyDeploy { param([hashtable]$Context) Invoke-StubDeploy -Context $Context -Connector 'haproxy' }
function Invoke-HaproxyBind { param([hashtable]$Context) Invoke-StubBind -Context $Context -Connector 'haproxy' }
function Invoke-HaproxyActivate { param([hashtable]$Context) Invoke-StubActivate -Context $Context -Connector 'haproxy' }
function Invoke-HaproxyVerify { param([hashtable]$Context) Invoke-StubVerify -Context $Context -Connector 'haproxy' }
function Invoke-HaproxyRollback { param([hashtable]$Context) Invoke-StubRollback -Context $Context -Connector 'haproxy' }

Export-ModuleMember -Function Invoke-HaproxyProbe,Invoke-HaproxyDeploy,Invoke-HaproxyBind,Invoke-HaproxyActivate,Invoke-HaproxyVerify,Invoke-HaproxyRollback
