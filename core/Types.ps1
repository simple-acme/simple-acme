Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:CertificateEventSchema = @{
    event                = [string]
    renewal_id           = [string]
    deployment_policy_id = [string]
    domain               = [string]
    cert_path            = [string]
    key_path             = [string]
    fullchain_path       = [string]
    thumbprint           = [string]
    issuer               = [string]
    not_before           = [string]
    not_after            = [string]
}

$script:ConnectorJobSchema = @{
    job_id                = [string]
    renewal_id            = [string]
    deployment_policy_id  = [string]
    connector_type        = [string]
    step                  = [string]
    status                = [string]
    artifact_ref          = [string]
    previous_artifact_ref = [string]
    attempt               = [int]
    error_detail          = [string]
    created_at            = [string]
    updated_at            = [string]
}

$script:DeploymentPolicySchema = @{
    policy_id        = [string]
    fanout_policy    = [string]
    quorum_threshold = [int]
    connectors       = [array]
}

$script:ConnectorConfigSchema = @{
    connector_type = [string]
    label          = [string]
    settings       = [hashtable]
}

$script:ConnectorContextSchema = @{
    job_id                = [string]
    event                 = [hashtable]
    config                = [hashtable]
    artifact_ref          = [string]
    previous_artifact_ref = [string]
}

function ConvertTo-Hashtable {
    param([Parameter(Mandatory)]$InputObject)

    if ($null -eq $InputObject) { return $null }
    if ($InputObject -is [hashtable]) { return $InputObject }
    if ($InputObject -is [System.Collections.IDictionary]) {
        $result = @{}
        foreach ($key in $InputObject.Keys) {
            $result[$key] = ConvertTo-Hashtable -InputObject $InputObject[$key]
        }
        return $result
    }
    if ($InputObject -is [System.Collections.IEnumerable] -and -not ($InputObject -is [string])) {
        $list = @()
        foreach ($item in $InputObject) {
            $list += ,(ConvertTo-Hashtable -InputObject $item)
        }
        return $list
    }

    return $InputObject
}

function Assert-Schema {
    param(
        [Parameter(Mandatory)][hashtable]$Object,
        [Parameter(Mandatory)][hashtable]$Schema,
        [Parameter(Mandatory)][string]$SchemaName
    )

    foreach ($field in $Schema.Keys) {
        if (-not $Object.ContainsKey($field)) {
            throw "${SchemaName} missing required field '$field'."
        }

        $expectedType = $Schema[$field]
        if ($null -eq $Object[$field]) { continue }

        if ($expectedType -eq [array]) {
            if (-not ($Object[$field] -is [System.Array])) {
                throw "${SchemaName} field '$field' expected array but found '$($Object[$field].GetType().FullName)'."
            }
            continue
        }

        if ($expectedType -eq [hashtable]) {
            if (-not ($Object[$field] -is [hashtable])) {
                throw "${SchemaName} field '$field' expected hashtable but found '$($Object[$field].GetType().FullName)'."
            }
            continue
        }

        if (-not ($Object[$field] -is $expectedType)) {
            throw "${SchemaName} field '$field' expected '$($expectedType.FullName)' but found '$($Object[$field].GetType().FullName)'."
        }
    }
}

function Assert-CertificateEvent { param([hashtable]$Event) Assert-Schema -Object $Event -Schema $script:CertificateEventSchema -SchemaName 'CertificateEvent' }
function Assert-ConnectorJobRecord { param([hashtable]$Job) Assert-Schema -Object $Job -Schema $script:ConnectorJobSchema -SchemaName 'ConnectorJobRecord' }
function Assert-DeploymentPolicy { param([hashtable]$Policy) Assert-Schema -Object $Policy -Schema $script:DeploymentPolicySchema -SchemaName 'DeploymentPolicy' }
function Assert-ConnectorConfig { param([hashtable]$Config) Assert-Schema -Object $Config -Schema $script:ConnectorConfigSchema -SchemaName 'ConnectorConfig' }
function Assert-ConnectorContext { param([hashtable]$Context) Assert-Schema -Object $Context -Schema $script:ConnectorContextSchema -SchemaName 'ConnectorContext' }
