<#!
.SYNOPSIS
Deploys a prepared Azure Functions zip package.

.DESCRIPTION
Validates the Azure CLI session and target Function App, deploys the package
with zip deployment, and verifies that the Function App remains available.

.EXAMPLE
./scripts/Deploy-Function.ps1 `
    -ResourceGroupName "rg-example" `
    -FunctionAppName "func-epm-log-collector" `
    -PackagePath "./publish/function.zip"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $ResourceGroupName,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $FunctionAppName,

    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI is required. Install it and sign in before deploying the Function App.'
}

$account = & az account show --output json --only-show-errors 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "No usable Azure CLI session found: $($account -join [Environment]::NewLine)"
}

$package = (Resolve-Path -LiteralPath $PackagePath).Path
$functionApp = & az functionapp show `
    --resource-group $ResourceGroupName `
    --name $FunctionAppName `
    --output json `
    --only-show-errors 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Unable to locate Function App '$FunctionAppName' in resource group '$ResourceGroupName': $($functionApp -join [Environment]::NewLine)"
}

$functionAppDetails = $functionApp -join [Environment]::NewLine | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($functionAppDetails.defaultHostName)) {
    throw "Function App '$FunctionAppName' does not have a default host name."
}

& az functionapp deployment source config-zip `
    --resource-group $ResourceGroupName `
    --name $FunctionAppName `
    --src $package `
    --only-show-errors `
    --output none
if ($LASTEXITCODE -ne 0) {
    throw "Zip deployment failed for Function App '$FunctionAppName'."
}

$deployedFunctionApp = & az functionapp show `
    --resource-group $ResourceGroupName `
    --name $FunctionAppName `
    --query '{state:state, defaultHostName:defaultHostName}' `
    --output json `
    --only-show-errors 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Unable to verify Function App '$FunctionAppName' after deployment: $($deployedFunctionApp -join [Environment]::NewLine)"
}

$deploymentStatus = $deployedFunctionApp -join [Environment]::NewLine | ConvertFrom-Json
if ($deploymentStatus.state -ne 'Running') {
    throw "Function App '$FunctionAppName' is not running after deployment. Current state: '$($deploymentStatus.state)'."
}

[pscustomobject]@{
    FunctionAppName = $FunctionAppName
    HostName        = $deploymentStatus.defaultHostName
    State           = $deploymentStatus.state
    PackagePath     = $package
}