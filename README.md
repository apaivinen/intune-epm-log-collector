# Intune EPM Elevation Request Log Collector

Azure Function solution for collecting Microsoft Intune Endpoint Privilege Management (EPM) elevation requests from Microsoft Graph and ingesting them into an Azure Log Analytics workspace.

The repository contains:

* C# Azure Function application code
* Microsoft Graph integration
* Azure Monitor Logs ingestion integration
* Bicep infrastructure-as-code
* GitHub Actions workflows for infrastructure and application deployment
* PowerShell bootstrap scripts for Microsoft Entra application permissions
* Visual Studio Code development container configuration

The goal is to provide a repeatable, version-controlled solution that can be deployed and maintained through GitHub.

---

## Solution Overview

The Azure Function periodically queries Microsoft Graph for Endpoint Privilege Management elevation requests.

The collected events are transformed into a Log Analytics compatible format and sent to a custom Log Analytics table through the Azure Monitor Logs Ingestion API.

The collected information should include, at minimum:

* Elevation request ID
* Request creation time
* Request status
* Requesting user
* User ID
* Device name
* Device ID
* User-provided justification
* Requested application
* Application path
* Application publisher
* Application version
* File hash
* Request expiry time
* Reviewer
* Reviewer justification
* Review completion time

The resulting data can then be queried with KQL and used by:

* Microsoft Sentinel
* Azure Monitor Workbooks
* Log Analytics queries
* Alert rules
* Hunting queries
* Reporting and dashboards

---

# Architecture

```text
┌─────────────────────────────────────────────┐
│ Microsoft Intune                            │
│ Endpoint Privilege Management               │
│                                             │
│ Elevation Requests                          │
└──────────────────────┬──────────────────────┘
                       │
                * [x] Create `.gitignore`
                * [x] Create `.gitattributes` with LF line endings
                       │
                       │ GET
                       │ /beta/deviceManagement/
                       │ elevationRequests
                       ▼
┌─────────────────────────────────────────────┐
│ Azure Function App                          │
│                                             │
│ C# / .NET Isolated Worker                   │
│ Timer Trigger                               │
│                                             │
│ 1. Authenticate                             │
│ 2. Query Graph                              │
│ 3. Handle paging                            │
│ 4. Filter new records                       │
│ 5. Transform records                        │
│ 6. Submit logs                              │
└──────────────────────┬──────────────────────┘
                       │
                       │ Azure Monitor
                       │ Logs Ingestion API
                       ▼
┌─────────────────────────────────────────────┐
│ Data Collection Rule                        │
│                                             │
│ Custom stream                               │
│ Transformation                              │
└──────────────────────┬──────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────┐
│ Log Analytics Workspace                     │
│                                             │
│ EpmElevationRequests_CL                     │
└─────────────────────────────────────────────┘
```

---

# Microsoft Graph Source

The solution reads Endpoint Privilege Management elevation requests from the Microsoft Graph beta API.

Endpoint:

```http
GET https://graph.microsoft.com/beta/deviceManagement/elevationRequests
```

The application uses application authentication rather than delegated user authentication.

Required Microsoft Graph application permission:

```text
DeviceManagementConfiguration.Read.All
```

Admin consent must be granted for the permission.

> [!IMPORTANT]
> The Endpoint Privilege Management API currently uses the Microsoft Graph `/beta` endpoint. Beta APIs can change independently of this repository and should therefore be monitored for breaking changes.

The Function must also support Microsoft Graph pagination by following the returned `@odata.nextLink` until all applicable results have been retrieved.

---

# Function Processing Flow

The intended execution flow is:

```text
Timer trigger
      │
      ▼
Acquire Microsoft Graph token
      │
      ▼
Read last successful collection watermark
      │
      ▼
Query Graph elevation requests
      │
      ▼
Follow Graph pagination
      │
      ▼
Identify requests newer than watermark
      │
      ▼
Map Graph objects into Log Analytics schema
      │
      ▼
Send records to Logs Ingestion API
      │
      ▼
Verify successful ingestion request
      │
      ▼
Update collection watermark
```

The watermark must only be updated after successful submission of the collected events.

This prevents a temporary Graph or Azure Monitor failure from causing records to be permanently skipped.

---

# Azure Function

The Function App is implemented using:

```text
Language: C#
Azure Functions runtime: v4
Execution model: .NET isolated worker
Target framework: .NET 10 (LTS; support ends November 14, 2028)
```

The isolated worker model should be used so that the application does not depend on the legacy Azure Functions in-process runtime.

> [!IMPORTANT]
> .NET 10 Azure Functions apps are not supported on the Linux Consumption plan. Use Flex Consumption, Premium, or Dedicated when hosting on Linux.

The Function App Bicep module targets an existing Linux Premium or Dedicated plan and storage account. Runtime settings are supplied as a secure module parameter; credentials are not embedded in the template.

---

## Function Trigger

The collector should use a Timer Trigger.

Example schedule:

```text
Every 5 minutes
```

The schedule must be configurable through Function App configuration rather than hard-coded into the application.

Example configuration:

```text
EpmCollectionSchedule = 0 */5 * * * *
```

The exact production polling interval can be changed without recompiling the Function.

---

# Authentication

There are two separate authentication contexts in the solution.

## 1. GitHub Deployment Authentication

GitHub Actions authenticates to Azure using a Microsoft Entra application / service principal.

Authentication uses:

```text
Application ID
Client secret
Tenant ID
Subscription ID
```

GitHub Environments are intentionally **not used** by this repository.

Repository-level GitHub Variables and Secrets are used instead.

### Repository Variables

Example repository variables:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
AZURE_RESOURCE_GROUP
AZURE_FUNCTION_APP_NAME
AZURE_LOCATION
```

`AZURE_CLIENT_ID` is not considered secret and is stored as a GitHub repository variable.

### Repository Secrets

```text
AZURE_CLIENT_SECRET
```

The client secret must only be stored as a GitHub repository secret.

It must never be committed into the repository.

---

## 2. Function Runtime Authentication

The Azure Function requires authentication for:

1. Microsoft Graph
2. Azure Monitor Logs Ingestion API

The initial implementation uses an Entra application/service principal and client credentials.

The required application configuration must be provided to the Function App securely through Azure configuration.

The repository must not contain runtime client secrets.

A future implementation may replace service-principal credentials with Managed Identity where appropriate.

---

# Microsoft Entra Bootstrap

The repository contains a PowerShell bootstrap script for configuring the application permissions required by the runtime application.

Example location:

```text
scripts/
└── Initialize-EntraApplication.ps1
```

The script is intended to be executed manually.

It is **not** executed as part of the normal GitHub Actions deployment process.

The bootstrap process is only required when initially creating or configuring the application.

The script should:

1. Locate the Entra application/service principal.
2. Locate the Microsoft Graph service principal.
3. Resolve the required Microsoft Graph application role.
4. Assign the application permission.
5. Optionally grant required Azure RBAC permissions.
6. Validate the resulting permissions.
7. Display a summary of applied configuration.

At minimum, the runtime application requires:

```text
Microsoft Graph
└── DeviceManagementConfiguration.Read.All
    └── Application permission
```

The application also requires permission to send data through the Azure Monitor Data Collection Rule.

The required Azure RBAC assignment should be created against the smallest practical scope, preferably the Data Collection Rule rather than the entire subscription or resource group.

The RBAC module assigns the built-in `Monitoring Metrics Publisher` role to the runtime service principal at the DCR scope for Logs Ingestion API access.

---

# Log Analytics Ingestion

The Function sends data to Log Analytics using the Azure Monitor Logs Ingestion API.

The ingestion architecture consists of:

```text
Function
   │
   ▼
Logs Ingestion API
   │
   ▼
Data Collection Rule
   │
   ▼
Custom Log Analytics table
```

A dedicated Data Collection Rule should be created by the Bicep deployment.

Where supported, the DCR's direct logs ingestion endpoint should be used.

The Data Collection Rule is configured for direct ingestion and exposes its generated Logs Ingestion endpoint. A separate Data Collection Endpoint can be introduced if required by network architecture or Private Link.

---

# Log Analytics Table

Proposed table name:

```text
EpmElevationRequests_CL
```

The exact table schema must be defined in Bicep and kept aligned with the C# ingestion model.

Defined schema:

| Column                               | Type     | Description                        |
| ------------------------------------ | -------- | ---------------------------------- |
| `TimeGenerated`                      | datetime | Event timestamp                    |
| `ElevationRequestId`                 | string   | Graph elevation request ID         |
| `RequestCreatedDateTime`             | datetime | Time the request was created       |
| `RequestLastModifiedDateTime`        | datetime | Last modification time             |
| `Status`                             | string   | Current request status             |
| `RequestedByUserId`                  | string   | Entra user object ID               |
| `RequestedByUserPrincipalName`       | string   | UPN of requesting user             |
| `RequestedOnDeviceId`                | string   | Device ID                          |
| `DeviceName`                         | string   | Device hostname                    |
| `RequestJustification`               | string   | Justification entered by the user  |
| `FileName`                           | string   | Requested executable               |
| `FilePath`                           | string   | Application path                   |
| `FileDescription`                    | string   | Application description            |
| `FileHash`                           | string   | File hash                          |
| `PublisherName`                      | string   | Application publisher              |
| `ProductName`                        | string   | Product name                       |
| `ProductInternalName`                | string   | Internal product name              |
| `ProductVersion`                     | string   | Product version                    |
| `RequestExpiryDateTime`              | datetime | Request expiration                 |
| `ReviewCompletedByUserId`            | string   | Reviewer Entra object ID           |
| `ReviewCompletedByUserPrincipalName` | string   | Reviewer UPN                       |
| `ReviewCompletedDateTime`            | datetime | Review completion timestamp        |
| `ReviewerJustification`              | string   | Reviewer justification             |
| `IngestionTime`                      | datetime | Time collector processed the event |
| `Source`                             | string   | Static source identifier           |

Suggested source value:

```text
MicrosoftIntuneEPM
```

---

# Duplicate Prevention and Collection Watermark

The Function is periodically polling an API rather than receiving individual events through an event-driven interface.

The solution therefore needs a mechanism for tracking which records have already been processed.

The recommended implementation uses a collection watermark stored in Azure Storage.

The Function App already requires an Azure Storage account, so an additional database is not required.

Example:

```text
Storage account
└── Blob container
    └── checkpoints
        └── epm-elevation-requests.json
```

Example checkpoint:

```json
{
  "lastSuccessfulCollection": "2026-09-29T12:00:00Z"
}
```

The collector should query an overlap period before the watermark to protect against delayed records.

Example:

```text
lastSuccessfulCollection - 5 minutes
```

Records should be identified by the Graph elevation request ID.

The checkpoint must only advance after successful Log Analytics ingestion.

---

# Error Handling

The Function must gracefully handle failures from both Microsoft Graph and Azure Monitor.

Expected scenarios include:

* Graph authentication failure
* Graph HTTP 429 throttling
* Graph 5xx responses
* Graph pagination failures
* Invalid Graph response
* Logs Ingestion API authentication failure
* Logs Ingestion API throttling
* Schema validation failure
* Network failure
* Azure Storage checkpoint failure

Transient failures should use retry logic with exponential backoff.

HTTP `429` responses must respect the `Retry-After` header when supplied.

A failed execution must not advance the collection watermark.

---

# Logging

The Function should use structured logging through `ILogger`.

Do not log:

* Client secrets
* Access tokens
* Authorization headers

Normal execution should record information such as:

```text
Collection started
Graph pages retrieved
Elevation requests retrieved
New elevation requests identified
Records submitted to Log Analytics
Checkpoint updated
Collection completed
```

Example:

```text
Retrieved 52 elevation requests from Microsoft Graph.
17 requests are newer than the current collection watermark.
Successfully ingested 17 records into Log Analytics.
```

Failures should contain enough information for troubleshooting without exposing credentials.

---

# Configuration

Application configuration should be separated from application code.

Example Function App settings:

```text
EpmCollectionSchedule
GraphBaseUrl
GraphTenantId
GraphClientId
GraphClientSecret
LogsIngestionEndpoint
DataCollectionRuleImmutableId
DataCollectionStreamName
LogsIngestionMaxBatchSizeBytes
CheckpointContainerName
CheckpointBlobName
CollectionOverlap
```

Example values:

```text
GraphBaseUrl=https://graph.microsoft.com/beta
DataCollectionStreamName=Custom-EpmElevationRequests
LogsIngestionMaxBatchSizeBytes=900000
CheckpointContainerName=checkpoints
CheckpointBlobName=epm-elevation-requests.json
CollectionOverlap=00:05:00
```

Secrets must never be committed to:

```text
local.settings.json
Bicep parameter files
GitHub workflow YAML
source code
```

A local developer can provide credentials through an untracked `local.settings.json` file.

---

# Repository Structure

Proposed repository structure:

```text
.
├── .devcontainer/
│   ├── devcontainer.json
│   └── Dockerfile
│
├── .github/
│   └── workflows/
│       ├── deploy-infrastructure.yml
│       └── deploy-function.yml
│
├── infrastructure/
│   ├── main.bicep
│   ├── modules/
│   │   ├── function-app.bicep
│   │   ├── storage-account.bicep
│   │   ├── log-analytics.bicep
│   │   ├── log-table.bicep
│   │   ├── data-collection-rule.bicep
│   │   └── role-assignments.bicep
│   │
│   └── parameters/
│       └── example.bicepparam
│
├── scripts/
│   ├── Initialize-EntraApplication.ps1
│   ├── Deploy-Function.ps1
│   └── Test-Prerequisites.ps1
│
├── src/
│   └── EpmLogCollector/
│       ├── Functions/
│       │   └── CollectElevationRequests.cs
│       │
│       ├── Clients/
│       │   ├── GraphClient.cs
│       │   └── LogsIngestionClient.cs
│       │
│       ├── Services/
│       │   ├── ElevationRequestCollector.cs
│       │   ├── ElevationRequestMapper.cs
│       │   └── CheckpointService.cs
│       │
│       ├── Models/
│       │   ├── Graph/
│       │   │   └── ElevationRequest.cs
│       │   └── LogAnalytics/
│       │       └── EpmElevationRequestLog.cs
│       │
│       ├── Configuration/
│       │   └── CollectorOptions.cs
│       │
│       ├── Program.cs
│       ├── host.json
│       └── EpmLogCollector.csproj
│
├── tests/
│   └── EpmLogCollector.Tests/
│
├── .gitignore
├── README.md
└── LICENSE
```

---

# Infrastructure as Code

All Azure infrastructure should be deployed using Bicep.

Infrastructure deployment must be independent from Function application deployment.

The infrastructure workflow should deploy resources including:

```text
Resource Group                    Optional if pre-existing
Storage Account                   Required by Function App
Function App                      Required
Function hosting plan             Required
Application Insights              Recommended
Log Analytics Workspace           Existing or deployed
Custom Log Analytics table        Required
Data Collection Rule              Required
RBAC assignments                  Required
```

The infrastructure should be divided into Bicep modules to keep resources independently maintainable.

---

# Infrastructure Deployment Workflow

Workflow:

```text
.github/workflows/deploy-infrastructure.yml
```

The workflow must only execute manually:

```yaml
on:
  workflow_dispatch:
```

High-level workflow:

```text
Manual workflow trigger
        │
        ▼
Checkout repository
        │
        ▼
Authenticate to Azure
        │
        ▼
Validate Bicep
        │
        ▼
Run Bicep what-if
        │
        ▼
Deploy Bicep
        │
        ▼
Display deployment outputs
```

The workflow uses repository variables and secrets for Azure authentication.

No GitHub Environment is required.

---

# Function Application Deployment Workflow

Workflow:

```text
.github/workflows/deploy-function.yml
```

The application deployment is independent of infrastructure deployment.

It must also only execute manually:

```yaml
on:
  workflow_dispatch:
```

High-level workflow:

```text
Manual workflow trigger
        │
        ▼
Checkout repository
        │
        ▼
Authenticate to Azure
        │
        ▼
Restore .NET dependencies
        │
        ▼
Build
        │
        ▼
Run tests
        │
        ▼
Publish Function
        │
        ▼
Package deployment
        │
        ▼
PowerShell deployment script
        │
        ▼
Azure Function App
```

The workflow should call:

```text
scripts/Deploy-Function.ps1
```

rather than containing all deployment logic directly inside the GitHub Actions YAML.

This keeps the deployment process reusable from both GitHub Actions and a developer workstation.

---

# PowerShell Function Deployment

The deployment script should:

1. Validate required arguments.
2. Verify Azure authentication.
3. Locate the Function App.
4. Build or locate the prepared deployment package.
5. Deploy the package.
6. Validate deployment result.
7. Return a non-zero exit code on failure.

Example usage:

```powershell
./scripts/Deploy-Function.ps1 `
    -ResourceGroupName "rg-example" `
    -FunctionAppName "func-epm-log-collector" `
    -PackagePath "./publish/function.zip"
```

The GitHub workflow should be responsible for authentication.

The PowerShell script should be responsible for application deployment.

---

# Development Container

Development should be performed using Visual Studio Code Dev Containers.

Configuration:

```text
.devcontainer/
├── devcontainer.json
└── Dockerfile
```

The development container should contain all tools needed for:

* C# development
* Azure Functions development
* Bicep development
* PowerShell development
* Azure administration
* GitHub workflow development

Required tools include:

```text
.NET 10 SDK
Azure Functions Core Tools
Azure CLI
Bicep CLI
PowerShell 7
Git
GitHub CLI
curl
jq
```

Recommended Visual Studio Code extensions:

```text
C#
C# Dev Kit
Azure Functions
Bicep
PowerShell
GitHub Actions
```

The goal is that a developer can:

1. Clone the repository.
2. Open the repository in Visual Studio Code.
3. Select **Reopen in Container**.
4. Authenticate to Azure.
5. Build and test the complete solution without installing additional development dependencies locally.

---

# Local Development

## Open Development Container

Clone the repository:

```bash
git clone <repository-url>
cd <repository-name>
code .
```

Then select:

```text
Dev Containers: Reopen in Container
```

---

## Azure Authentication

Authenticate from inside the development container:

```bash
az login
```

Verify:

```bash
az account show
```

---

## Restore

```bash
dotnet restore
```

---

## Build

```bash
dotnet build
```

---

## Test

```bash
dotnet test
```

---

## Run Azure Function Locally

Create:

```text
src/EpmLogCollector/local.settings.json
```

This file must be excluded through `.gitignore`.

Then start the Function:

```bash
cd src/EpmLogCollector
func start
```

---

# GitHub Repository Configuration

The following settings must be configured before deployment.

## Variables

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
AZURE_RESOURCE_GROUP
AZURE_LOCATION
AZURE_FUNCTION_APP_NAME
```

## Secrets

```text
AZURE_CLIENT_SECRET
```

Do not use GitHub Environments for this implementation.

All deployment configuration is stored at repository scope.

---

# Initial Deployment

The expected first-time deployment sequence is:

```text
1. Create deployment Entra application
        │
        ▼
2. Add GitHub repository variables and secret
        │
        ▼
3. Run Entra bootstrap PowerShell script
        │
        ▼
4. Run infrastructure deployment workflow
        │
        ▼
5. Verify Azure resources
        │
        ▼
6. Run Function application deployment workflow
        │
        ▼
7. Verify Function execution
        │
        ▼
8. Verify Log Analytics ingestion
```

Depending on how responsibilities are ultimately separated, the runtime application may be provisioned either before or as part of the infrastructure deployment.

---

# Validation

After deployment, verify that the Function is running successfully.

Then generate or locate an Endpoint Privilege Management elevation request.

Run:

```kusto
EpmElevationRequests_CL
| sort by TimeGenerated desc
```

Example user-focused query:

```kusto
EpmElevationRequests_CL
| project
    TimeGenerated,
    RequestedByUserPrincipalName,
    DeviceName,
    FileName,
    PublisherName,
    RequestJustification,
    Status
| sort by TimeGenerated desc
```

Example requests by application:

```kusto
EpmElevationRequests_CL
| summarize
    Requests = count()
    by FileName, PublisherName
| sort by Requests desc
```

Example requests by user:

```kusto
EpmElevationRequests_CL
| summarize
    Requests = count()
    by RequestedByUserPrincipalName
| sort by Requests desc
```

---

# Security Considerations

The following principles should be followed.

### Least privilege

Use the minimum required Microsoft Graph permissions.

For reading elevation requests, use:

```text
DeviceManagementConfiguration.Read.All
```

rather than the corresponding `ReadWrite` permission.

### Secrets

Secrets must not be committed to Git.

GitHub deployment credentials must be stored in GitHub Secrets.

Runtime credentials must be stored securely in Azure.

### Logging

Never log:

```text
Access tokens
Client secrets
Authorization headers
GitHub secrets
```

### Azure RBAC

Assign Azure permissions at the smallest practical resource scope.

### Credential rotation

Service principal secrets should have defined expiration and rotation procedures.

A future improvement should evaluate replacing client-secret authentication with:

* GitHub Actions OpenID Connect for deployment authentication
* Azure Managed Identity for Function runtime authentication

---

# Testing Strategy

The repository should contain automated unit tests for the core processing logic.

At minimum, tests should cover:

```text
Graph response deserialization
Graph pagination
Elevation request mapping
Null/optional Graph properties
Checkpoint loading
Checkpoint updates
Timestamp filtering
Batch generation
Logs ingestion request generation
Failure handling
```

External services should be abstracted behind interfaces so they can be mocked during unit tests.

---

# Implementation Principles

The application should follow these principles:

* Keep Azure-specific infrastructure in Bicep.
* Keep deployment logic in PowerShell where practical.
* Keep GitHub workflow YAML thin.
* Separate Graph retrieval from Log Analytics ingestion.
* Separate external API models from internal log models.
* Keep authentication logic reusable.
* Use dependency injection.
* Use structured logging.
* Implement Graph pagination.
* Implement retry handling.
* Never advance the checkpoint after a failed ingestion.
* Make configuration external to application code.
* Never commit credentials.

---

# Implementation Roadmap

## Phase 1 — Repository Foundation

* [x] Create repository directory structure
* [x] Create `.gitignore`
* [x] Create `.gitattributes`
* [x] Create development container
* [x] Create C# Function project
* [x] Create test project
* [x] Verify local Function execution

## Phase 2 — Microsoft Graph

* [x] Create Graph authentication service
* [x] Implement elevation request model
* [x] Implement `/beta/deviceManagement/elevationRequests`
* [x] Implement pagination
* [x] Implement retry handling
* [x] Create Graph unit tests

## Phase 3 — Collection State

* [x] Implement Blob Storage checkpoint service
* [x] Implement timestamp filtering
* [x] Implement collection overlap
* [x] Verify recovery after failed runs

## Phase 4 — Log Analytics

* [x] Define custom table schema
* [x] Define DCR stream
* [x] Implement Log Analytics model
* [x] Implement Logs Ingestion API client
* [x] Implement batching
* [x] Implement ingestion retries

## Phase 5 — Infrastructure

* [x] Function App Bicep module
* [x] Storage Bicep module
* [x] Application Insights integration
* [x] Log Analytics table Bicep module
* [x] Data Collection Rule Bicep module
* [x] RBAC Bicep module
* [ ] Root deployment template
* [ ] Parameter file

## Phase 6 — Identity Bootstrap

* [ ] Create `Initialize-EntraApplication.ps1`
* [ ] Resolve Microsoft Graph service principal
* [ ] Assign Graph application role
* [ ] Validate Graph permission
* [ ] Configure Azure RBAC
* [ ] Add prerequisite validation

## Phase 7 — CI/CD

* [ ] Infrastructure workflow
* [ ] Function deployment workflow
* [ ] PowerShell deployment script
* [ ] Build validation
* [ ] Unit test execution
* [ ] Deployment validation

## Phase 8 — Production Validation

* [ ] Deploy infrastructure
* [ ] Deploy Function
* [ ] Generate EPM request
* [ ] Verify Graph collection
* [ ] Verify Log Analytics ingestion
* [ ] Verify duplicate prevention
* [ ] Verify failure recovery
* [ ] Validate KQL queries

---

# Future Improvements

Potential future improvements include:

* GitHub Actions workload identity federation instead of client secrets
* Function Managed Identity instead of runtime client secrets
* Private networking
* Azure Monitor Private Link
* Key Vault integration
* Deployment environments
* Automated integration tests
* Microsoft Sentinel analytics rules
* Sentinel workbook
* Monitoring and alerting for collector failures
* Dead-letter handling for failed records
* Multiple tenant support
* Graph API migration from `/beta` if the required endpoint becomes available in `v1.0`

---

# Open Design Decisions

The following decisions should be finalized during implementation:

1. Whether the Function runtime uses a dedicated Entra application or Managed Identity.
2. Whether the Log Analytics workspace already exists or is deployed by this repository.
3. Whether the repository creates the runtime Entra application or assumes it already exists.
4. Function hosting model and SKU.
5. Function polling interval.
6. Exact Log Analytics table name.
7. Log retention period.
8. Whether a Data Collection Endpoint is required.
9. Credential storage method for the initial runtime implementation.
10. Whether the Graph elevation request API supports sufficient server-side filtering or filtering should primarily happen inside the Function.

---

# References

The implementation relies on the following Microsoft technologies:

* Microsoft Intune Endpoint Privilege Management
* Microsoft Graph
* Azure Functions
* Azure Monitor Logs Ingestion API
* Azure Monitor Data Collection Rules
* Azure Log Analytics
* Azure Bicep
* Microsoft Entra ID
* GitHub Actions
* Visual Studio Code Dev Containers

## External resources

- https://learn.microsoft.com/en-us/graph/api/intune-epmgraphapiservice-privilegemanagementelevationrequest-list?view=graph-rest-beta


## External notes

One architectural point I deliberately added is the checkpoint/watermark mechanism. Without one, a five-minute polling Function either risks ingesting the same elevation requests repeatedly or missing requests when an execution fails. Using a blob in the Function's existing storage account keeps that mechanism simple without introducing another database.

I would also keep DeviceManagementConfiguration.Read.All, rather than ReadWrite.All, because Microsoft currently lists Read.All as sufficient for listing elevation requests. The response schema documented by Microsoft contains exactly the fields you were interested in, including requestedByUserPrincipalName, requestJustification, applicationDetail, device information, status, and reviewer fields.

The three questions that would most materially affect the next iteration are: (1) should the Function runtime use its own client ID + secret, or would you prefer Managed Identity; (2) does the Log Analytics workspace already exist and should Bicep reference it rather than create it; and (3) which Function hosting plan do you want—Flex Consumption, Premium, or another existing plan? Once those are decided, the README can become even more prescriptive, including exact resource names, application settings, Bicep modules, and workflow variable names.