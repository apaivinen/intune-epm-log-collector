# Intune EPM Elevation Request Log Collector

[![Infrastructure workflow](https://github.com/apaivinen/intune-epm-log-collector/actions/workflows/deploy-infrastructure.yml/badge.svg)](https://github.com/apaivinen/intune-epm-log-collector/actions/workflows/deploy-infrastructure.yml)
[![Function deployment workflow](https://github.com/apaivinen/intune-epm-log-collector/actions/workflows/deploy-function.yml/badge.svg)](https://github.com/apaivinen/intune-epm-log-collector/actions/workflows/deploy-function.yml)

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

## Table of Contents

- [Solution Overview](#solution-overview)
- [Architecture](#architecture)
- [Microsoft Graph Source](#microsoft-graph-source)
- [Function Processing Flow](#function-processing-flow)
- [Azure Function](#azure-function)
- [Authentication](#authentication)
- [Microsoft Entra Bootstrap](#microsoft-entra-bootstrap)
- [Log Analytics Ingestion](#log-analytics-ingestion)
- [Log Analytics Table](#log-analytics-table)
- [Duplicate Prevention and Collection Watermark](#duplicate-prevention-and-collection-watermark)
- [Error Handling](#error-handling)
- [Logging](#logging)
- [Configuration](#configuration)
- [Repository Structure](#repository-structure)
- [Infrastructure as Code](#infrastructure-as-code)
- [Infrastructure Deployment Workflow](#infrastructure-deployment-workflow)
- [Function Application Deployment Workflow](#function-application-deployment-workflow)
- [PowerShell Function Deployment](#powershell-function-deployment)
- [Development Container](#development-container)
- [Local Development](#local-development)
- [GitHub Repository Configuration](#github-repository-configuration)
- [Initial Deployment](#initial-deployment)
- [Validation](#validation)
- [Security Considerations](#security-considerations)
- [Testing Strategy](#testing-strategy)
- [Implementation Principles](#implementation-principles)
- [Implementation Roadmap](#implementation-roadmap)
- [Future Improvements](#future-improvements)
- [Open Design Decisions](#open-design-decisions)
- [References](#references)

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
