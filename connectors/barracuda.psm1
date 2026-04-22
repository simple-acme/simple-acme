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
function Invoke-BarracudaProbe { param([hashtable]$Context) Invoke-StubProbe -Context $Context -Connector 'barracuda' }
function Invoke-BarracudaDeploy { param([hashtable]$Context) Invoke-StubDeploy -Context $Context -Connector 'barracuda' }
function Invoke-BarracudaBind { param([hashtable]$Context) Invoke-StubBind -Context $Context -Connector 'barracuda' }
function Invoke-BarracudaActivate { param([hashtable]$Context) Invoke-StubActivate -Context $Context -Connector 'barracuda' }
function Invoke-BarracudaVerify { param([hashtable]$Context) Invoke-StubVerify -Context $Context -Connector 'barracuda' }
function Invoke-BarracudaRollback { param([hashtable]$Context) Invoke-StubRollback -Context $Context -Connector 'barracuda' }

Export-ModuleMember -Function Invoke-BarracudaProbe,Invoke-BarracudaDeploy,Invoke-BarracudaBind,Invoke-BarracudaActivate,Invoke-BarracudaVerify,Invoke-BarracudaRollback
