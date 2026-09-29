<#
.SYNOPSIS
Validates prerequisites for configuring the runtime Entra application.

.DESCRIPTION
Performs read-only checks against the active Azure CLI session, Microsoft Graph,
and optionally the target Data Collection Rule and its sender role assignment
definition. It does not change Entra or Azure resources.

.EXAMPLE
./scripts/Test-Prerequisites.ps1 -TenantId <tenant-id> -ServicePrincipalObjectId <managed-identity-principal-id>

.EXAMPLE
./scripts/Test-Prerequisites.ps1 -TenantId <tenant-id> -ServicePrincipalObjectId <managed-identity-principal-id> `
    -DataCollectionRuleResourceId <dcr-resource-id>
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [guid] $TenantId,

    [Parameter(Mandatory)]
    [guid] $ServicePrincipalObjectId,

    [Parameter()]
    [ValidatePattern('^/subscriptions/[^/]+/resourceGroups/[^/]+/providers/Microsoft\.Insights/dataCollectionRules/[^/]+$')]
    [string] $DataCollectionRuleResourceId
)

$ErrorActionPreference = 'Stop'

$script:GraphBaseUrl = 'https://graph.microsoft.com/v1.0'
$script:GraphResourceAppId = '00000003-0000-0000-c000-000000000000'
$script:RequiredGraphPermission = 'DeviceManagementConfiguration.Read.All'
$script:DcrSenderRoleName = 'Monitoring Metrics Publisher'
$script:DcrSenderRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'

$script:Checks = [System.Collections.Generic.List[object]]::new()

function Add-Check {
    param(
        [Parameter(Mandatory)]
        [string] $Name,

        [Parameter(Mandatory)]
        [ValidateSet('PASS', 'FAIL', 'SKIP', 'INFO')]
        [string] $Status,

        [Parameter(Mandatory)]
        [string] $Details
    )

    $script:Checks.Add([pscustomobject]@{
        Check   = $Name
        Status  = $Status
        Details = $Details
    })
}

function Invoke-AzCliJson {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    $output = & az @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: $($output -join [Environment]::NewLine)"
    }

    $json = $output -join [Environment]::NewLine
    if ([string]::IsNullOrWhiteSpace($json)) {
        return $null
    }

    return $json | ConvertFrom-Json
}

function Invoke-GraphGet {
    param(
        [Parameter(Mandatory)]
        [string] $Uri
    )

    $parsedUri = [uri] $Uri
    if ($parsedUri.Scheme -ne 'https' -or $parsedUri.Host -ne 'graph.microsoft.com') {
        throw 'Refusing to send the Microsoft Graph access token to a non-Graph endpoint.'
    }

    Invoke-RestMethod -Method GET -Uri $Uri `
        -Headers @{ Authorization = "Bearer $script:GraphAccessToken" } `
        -ErrorAction Stop
}

function Get-GraphCollection {
    param(
        [Parameter(Mandatory)]
        [string] $Uri
    )

    $items = [System.Collections.Generic.List[object]]::new()
    $nextPage = $Uri
    while (-not [string]::IsNullOrWhiteSpace($nextPage)) {
        $response = Invoke-GraphGet -Uri $nextPage
        foreach ($item in $response.value) {
            $items.Add($item)
        }
        $nextPage = $response.'@odata.nextLink'
    }

    return $items.ToArray()
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Add-Check -Name 'Azure CLI' -Status FAIL -Details 'Install Azure CLI and sign in before running this check.'
    $script:Checks | Format-Table -AutoSize | Out-Host
    exit 1
}
Add-Check -Name 'Azure CLI' -Status PASS -Details 'Azure CLI is installed.'

$activeAccount = $null
try {
    $activeAccount = Invoke-AzCliJson -Arguments @('account', 'show', '--output', 'json', '--only-show-errors')
    Add-Check -Name 'Azure CLI sign-in' -Status PASS -Details "Signed in to tenant $($activeAccount.tenantId)."
}
catch {
    Add-Check -Name 'Azure CLI sign-in' -Status FAIL -Details 'No usable Azure CLI session. Run az login for the target tenant.'
}

if ($null -ne $activeAccount) {
    if ([guid] $activeAccount.tenantId -eq $TenantId) {
        Add-Check -Name 'Tenant selection' -Status PASS -Details "Active tenant matches $TenantId."
    }
    else {
        Add-Check -Name 'Tenant selection' -Status FAIL -Details "Active tenant $($activeAccount.tenantId) does not match $TenantId."
    }

    if ([guid] $activeAccount.tenantId -eq $TenantId) {
        try {
            $script:GraphAccessToken = & az account get-access-token `
                --tenant $TenantId `
                --resource 'https://graph.microsoft.com' `
                --query accessToken `
                --output tsv `
                --only-show-errors 2>$null
            if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($script:GraphAccessToken)) {
                throw 'Azure CLI did not return a Graph access token.'
            }
            Add-Check -Name 'Microsoft Graph token' -Status PASS -Details 'A Graph access token can be acquired; token value is not displayed.'
        }
        catch {
            Add-Check -Name 'Microsoft Graph token' -Status FAIL -Details $_.Exception.Message
        }

        if (-not [string]::IsNullOrWhiteSpace($script:GraphAccessToken)) {
            try {
                $runtimeServicePrincipal = Invoke-GraphGet -Uri "$script:GraphBaseUrl/servicePrincipals/$ServicePrincipalObjectId?`$select=id,appId,displayName"
                if ($runtimeServicePrincipal.id -ne $ServicePrincipalObjectId.Guid) {
                    throw "The service principal response did not match object ID $ServicePrincipalObjectId."
                }
                Add-Check -Name 'Managed identity service principal' -Status PASS -Details "Found '$($runtimeServicePrincipal.displayName)'."

                $graphServicePrincipalFilter = [uri]::EscapeDataString("appId eq '$script:GraphResourceAppId'")
                $graphServicePrincipals = @(Get-GraphCollection -Uri "$script:GraphBaseUrl/servicePrincipals?`$filter=$graphServicePrincipalFilter&`$select=id,appId,displayName,appRoles")
                if ($graphServicePrincipals.Count -ne 1) {
                    throw "Expected one Microsoft Graph service principal; found $($graphServicePrincipals.Count)."
                }
                $graphServicePrincipal = $graphServicePrincipals[0]
                $graphRole = @($graphServicePrincipal.appRoles | Where-Object {
                    $_.value -eq $script:RequiredGraphPermission -and
                    $_.isEnabled -and
                    $_.allowedMemberTypes -contains 'Application'
                })
                if ($graphRole.Count -ne 1) {
                    throw "Could not resolve the enabled application role '$requiredGraphPermission'."
                }
                Add-Check -Name 'Required Graph application role' -Status PASS -Details "Resolved '$script:RequiredGraphPermission'."

                $assignmentUri = "$script:GraphBaseUrl/servicePrincipals/$($runtimeServicePrincipal.id)/appRoleAssignments?`$select=appRoleId,resourceId,principalId"
                $null = Get-GraphCollection -Uri $assignmentUri
                Add-Check -Name 'Graph assignment read access' -Status PASS -Details 'Can read the runtime service principal app-role assignments.'
            }
            catch {
                Add-Check -Name 'Microsoft Graph prerequisites' -Status FAIL -Details $_.Exception.Message
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($DataCollectionRuleResourceId)) {
    Add-Check -Name 'DCR and Azure RBAC' -Status SKIP -Details 'Not checked because no DCR resource ID was supplied.'
}
else {
    try {
        $dataCollectionRule = Invoke-AzCliJson -Arguments @(
            'resource', 'show',
            '--ids', $DataCollectionRuleResourceId,
            '--api-version', '2024-03-11',
            '--output', 'json',
            '--only-show-errors'
        )
        if ($dataCollectionRule.type -ine 'Microsoft.Insights/dataCollectionRules' -or
            $dataCollectionRule.id -ine $DataCollectionRuleResourceId) {
            throw 'The resource ID did not resolve to the requested Data Collection Rule.'
        }
        Add-Check -Name 'Data Collection Rule' -Status PASS -Details "Found '$($dataCollectionRule.name)' and can read it."
    }
    catch {
        Add-Check -Name 'Data Collection Rule' -Status FAIL -Details $_.Exception.Message
    }

    try {
        $roleDefinitions = @(Invoke-AzCliJson -Arguments @(
            'role', 'definition', 'list',
            '--name', $script:DcrSenderRoleId,
            '--scope', $DataCollectionRuleResourceId,
            '--output', 'json',
            '--only-show-errors'
        ))
        $matchingRole = @($roleDefinitions | Where-Object {
            $_.name -eq $script:DcrSenderRoleId -and $_.roleName -eq $script:DcrSenderRoleName
        })
        if ($matchingRole.Count -ne 1) {
            throw "Could not resolve '$script:DcrSenderRoleName' ($script:DcrSenderRoleId) as assignable at the DCR scope."
        }
        Add-Check -Name 'DCR sender role definition' -Status PASS -Details "Resolved '$script:DcrSenderRoleName'."
    }
    catch {
        Add-Check -Name 'DCR sender role definition' -Status FAIL -Details $_.Exception.Message
    }
}

Add-Check -Name 'Assignment write permissions' -Status INFO -Details 'Not tested; confirming write access would require attempting a resource change.'
Remove-Variable GraphAccessToken -Scope Script -ErrorAction SilentlyContinue

$script:Checks | Format-Table -AutoSize | Out-Host
if (@($script:Checks | Where-Object Status -eq 'FAIL').Count -gt 0) {
    exit 1
}