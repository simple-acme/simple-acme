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
function Invoke-TraefikProbe { param([hashtable]$Context) Invoke-StubProbe -Context $Context -Connector 'traefik' }
function Invoke-TraefikDeploy { param([hashtable]$Context) Invoke-StubDeploy -Context $Context -Connector 'traefik' }
function Invoke-TraefikBind { param([hashtable]$Context) Invoke-StubBind -Context $Context -Connector 'traefik' }
function Invoke-TraefikActivate { param([hashtable]$Context) Invoke-StubActivate -Context $Context -Connector 'traefik' }
function Invoke-TraefikVerify { param([hashtable]$Context) Invoke-StubVerify -Context $Context -Connector 'traefik' }
function Invoke-TraefikRollback { param([hashtable]$Context) Invoke-StubRollback -Context $Context -Connector 'traefik' }

Export-ModuleMember -Function Invoke-TraefikProbe,Invoke-TraefikDeploy,Invoke-TraefikBind,Invoke-TraefikActivate,Invoke-TraefikVerify,Invoke-TraefikRollback
