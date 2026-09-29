<#
.SYNOPSIS
Configures Microsoft Graph application permission and optional DCR ingestion RBAC for an existing runtime application.

.DESCRIPTION
Uses the current Azure CLI login to locate the managed-identity service principal, assign
DeviceManagementConfiguration.Read.All, verify the Graph app-role assignment, and optionally
assign Monitoring Metrics Publisher at a Data Collection Rule scope.

The signed-in identity needs Microsoft Graph application read and app-role assignment
permissions, plus permission to create Azure role assignments when a DCR scope is supplied.
Run az login for the target tenant before running this script.

.EXAMPLE
./scripts/Initialize-EntraApplication.ps1 -TenantId <tenant-id> -ServicePrincipalObjectId <managed-identity-principal-id>

.EXAMPLE
./scripts/Initialize-EntraApplication.ps1 -TenantId <tenant-id> -ServicePrincipalObjectId <managed-identity-principal-id> `
    -DataCollectionRuleResourceId <dcr-resource-id> -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
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

$graphBaseUrl = 'https://graph.microsoft.com/v1.0'
$graphResourceAppId = '00000003-0000-0000-c000-000000000000'
$requiredGraphPermission = 'DeviceManagementConfiguration.Read.All'
$dcrSenderRoleName = 'Monitoring Metrics Publisher'
$dcrSenderRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'

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

function Invoke-GraphRequest {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('GET', 'POST')]
        [string] $Method,

        [Parameter(Mandatory)]
        [string] $Uri,

        [Parameter()]
        [object] $Body
    )

    $parsedUri = [uri] $Uri
    if ($parsedUri.Scheme -ne 'https' -or $parsedUri.Host -ne 'graph.microsoft.com') {
        throw 'Refusing to send the Microsoft Graph access token to a non-Graph endpoint.'
    }

    $requestParameters = @{
        Method      = $Method
        Uri         = $Uri
        Headers     = @{ Authorization = "Bearer $script:GraphAccessToken" }
        ErrorAction = 'Stop'
    }
    if ($null -ne $Body) {
        $requestParameters.ContentType = 'application/json'
        $requestParameters.Body = $Body | ConvertTo-Json -Depth 10 -Compress
    }

    try {
        return Invoke-RestMethod @requestParameters
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        throw "Microsoft Graph request failed (HTTP $statusCode): $($_.Exception.Message)"
    }
}

function Get-GraphCollection {
    param(
        [Parameter(Mandatory)]
        [string] $Uri
    )

    $items = [System.Collections.Generic.List[object]]::new()
    $nextPage = $Uri
    while (-not [string]::IsNullOrWhiteSpace($nextPage)) {
        $response = Invoke-GraphRequest -Method GET -Uri $nextPage
        foreach ($item in $response.value) {
            $items.Add($item)
        }
        $nextPage = $response.'@odata.nextLink'
    }

    return $items.ToArray()
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI is required. Install it and run az login for the target tenant.'
}

$activeTenantId = & az account show --query tenantId --output tsv 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($activeTenantId)) {
    throw 'No Azure CLI session found. Run az login for the target tenant first.'
}
if ([guid] $activeTenantId -ne $TenantId) {
    throw "The active Azure CLI tenant ($activeTenantId) does not match the requested tenant ($TenantId). Run az login --tenant $TenantId."
}

$script:GraphAccessToken = & az account get-access-token `
    --tenant $TenantId `
    --resource 'https://graph.microsoft.com' `
    --query accessToken `
    --output tsv 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($script:GraphAccessToken)) {
    throw 'Unable to obtain a Microsoft Graph access token from the current Azure CLI session.'
}

$runtimeServicePrincipalUri = "$graphBaseUrl/servicePrincipals/$ServicePrincipalObjectId?`$select=id,appId,displayName"
$runtimeServicePrincipal = Invoke-GraphRequest -Method GET -Uri $runtimeServicePrincipalUri
if ($runtimeServicePrincipal.id -ne $ServicePrincipalObjectId.Guid) {
    throw "The service principal response did not match object ID $ServicePrincipalObjectId."
}

$graphServicePrincipalFilter = [uri]::EscapeDataString("appId eq '$graphResourceAppId'")
$graphServicePrincipalUri = "$graphBaseUrl/servicePrincipals?`$filter=$graphServicePrincipalFilter&`$select=id,appId,displayName,appRoles"
$graphServicePrincipals = @(Get-GraphCollection -Uri $graphServicePrincipalUri)
if ($graphServicePrincipals.Count -ne 1) {
    throw "Expected one Microsoft Graph service principal; found $($graphServicePrincipals.Count)."
}
$graphServicePrincipal = $graphServicePrincipals[0]

$graphRole = @($graphServicePrincipal.appRoles | Where-Object {
    $_.value -eq $requiredGraphPermission -and
    $_.isEnabled -and
    $_.allowedMemberTypes -contains 'Application'
})
if ($graphRole.Count -ne 1) {
    throw "Could not resolve the enabled Microsoft Graph application role '$requiredGraphPermission'."
}

$existingGraphAssignmentsUri = "$graphBaseUrl/servicePrincipals/$($runtimeServicePrincipal.id)/appRoleAssignments?`$select=appRoleId,resourceId,principalId"
$existingGraphAssignments = @(Get-GraphCollection -Uri $existingGraphAssignmentsUri)
$hasGraphPermission = @($existingGraphAssignments | Where-Object {
    $_.resourceId -eq $graphServicePrincipal.id -and $_.appRoleId -eq $graphRole[0].id
}).Count -gt 0

if ($hasGraphPermission) {
    $graphPermissionStatus = 'AlreadyAssigned'
}
elseif ($PSCmdlet.ShouldProcess($runtimeServicePrincipal.displayName, "Assign Microsoft Graph $requiredGraphPermission application permission")) {
    $assignment = @{
        principalId = $runtimeServicePrincipal.id
        resourceId  = $graphServicePrincipal.id
        appRoleId   = $graphRole[0].id
    }
    Invoke-GraphRequest `
        -Method POST `
        -Uri "$graphBaseUrl/servicePrincipals/$($runtimeServicePrincipal.id)/appRoleAssignments" `
        -Body $assignment | Out-Null

    $verifiedAssignments = @(Get-GraphCollection -Uri $existingGraphAssignmentsUri)
    $hasGraphPermission = @($verifiedAssignments | Where-Object {
        $_.resourceId -eq $graphServicePrincipal.id -and $_.appRoleId -eq $graphRole[0].id
    }).Count -gt 0
    if (-not $hasGraphPermission) {
        throw "Microsoft Graph permission '$requiredGraphPermission' was not present after assignment."
    }
    $graphPermissionStatus = 'AssignedAndVerified'
}
else {
    $graphPermissionStatus = 'WouldAssign'
}

$dcrRoleStatus = 'NotRequested'
if (-not [string]::IsNullOrWhiteSpace($DataCollectionRuleResourceId)) {
    $roleAssignments = @(Invoke-AzCliJson -Arguments @(
        'role', 'assignment', 'list',
        '--scope', $DataCollectionRuleResourceId,
        '--all',
        '--output', 'json'
    ))
    $existingDcrAssignment = @($roleAssignments | Where-Object {
        $_.scope -eq $DataCollectionRuleResourceId -and
        $_.principalId -eq $runtimeServicePrincipal.id -and
        $_.roleDefinitionId -match "/$dcrSenderRoleId$"
    })

    if ($existingDcrAssignment.Count -gt 0) {
        $dcrRoleStatus = 'AlreadyAssigned'
    }
    elseif ($PSCmdlet.ShouldProcess($DataCollectionRuleResourceId, "Assign $dcrSenderRoleName to $($runtimeServicePrincipal.displayName)")) {
        Invoke-AzCliJson -Arguments @(
            'role', 'assignment', 'create',
            '--assignee-object-id', $runtimeServicePrincipal.id,
            '--assignee-principal-type', 'ServicePrincipal',
            '--role', $dcrSenderRoleId,
            '--scope', $DataCollectionRuleResourceId,
            '--output', 'json'
        ) | Out-Null

        $verifiedDcrAssignments = @(Invoke-AzCliJson -Arguments @(
            'role', 'assignment', 'list',
            '--scope', $DataCollectionRuleResourceId,
            '--all',
            '--output', 'json'
        ))
        $hasDcrRoleAssignment = @($verifiedDcrAssignments | Where-Object {
            $_.scope -eq $DataCollectionRuleResourceId -and
            $_.principalId -eq $runtimeServicePrincipal.id -and
            $_.roleDefinitionId -match "/$dcrSenderRoleId$"
        }).Count -gt 0

        if (-not $hasDcrRoleAssignment) {
            throw "Role assignment '$dcrSenderRoleName' was not present at the DCR scope after creation."
        }
        $dcrRoleStatus = 'AssignedAndVerified'
    }
    else {
        $dcrRoleStatus = 'WouldAssign'
    }
}

Remove-Variable GraphAccessToken -Scope Script -ErrorAction SilentlyContinue

[pscustomobject] @{
    TenantId                       = $TenantId
    ServicePrincipalAppId          = $runtimeServicePrincipal.appId
    ServicePrincipalName           = $runtimeServicePrincipal.displayName
    ServicePrincipalObjectId       = $runtimeServicePrincipal.id
    GraphApplicationPermission     = $requiredGraphPermission
    GraphPermissionStatus          = $graphPermissionStatus
    DataCollectionRuleResourceId   = $DataCollectionRuleResourceId
    DcrSenderRole                  = if ($DataCollectionRuleResourceId) { $dcrSenderRoleName } else { $null }
    DcrRoleAssignmentStatus        = $dcrRoleStatus
}