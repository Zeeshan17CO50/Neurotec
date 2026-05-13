# Neurotec VFS - Enterprise Biometric Fingerprint Scanning Solution

## Overview

**Neurotec VFS** is a professional-grade biometric fingerprint scanning system developed for modernizing and replacing legacy VFS biometric solutions using the latest **.NET 10.0** and **Neurotec Professional SDK (2025.2)** architecture.

The solution is designed as a scalable, enterprise-ready Windows Service application that provides:

* Real-time fingerprint acquisition
* Automatic biometric device detection (Mantra, CrossMatch, etc.)
* REST API-based biometric operations including **Graceful Cancellation**
* Centralized SDK/plugin management with an **optimized fingerprint-only footprint**
* Modern lightweight "Glassmorphism" dashboard UI
* MSI-based deployment support

The application follows **Clean Architecture principles** and **SOLID patterns** to ensure production-grade reliability and extreme maintainability.

---

# Key Features

* **Graceful Reset Mechanism**: Allows interrupting active hardware scans without destabilizing device drivers.
* **Selection Persistence**: Dashboard remembers your scanner and mode choices even during background hardware polling.
* **Enterprise-grade biometric fingerprint capture**: Powered by Neurotec Professional SDK.
* **RESTful API communication**: Supporting Start, Status, and Cancel operations.
* **Windows Service support**: Native integration with Service Control Manager (SCM).
* **Modern Dashboard UI**: Built with responsive Vanilla CSS and glassmorphism effects.
* **MSI installer support**: Using WiX Toolset for automated silent deployment.
* **Professional Diagnostics**: Structured logging with `ILogger` and diagnostic file output.
* **Neurotec Professional SDK integration**: Centralized SDK configuration management
---

# Technology Stack

| Component     | Technology                       |
| ------------- | -------------------------------- |
| Framework     | .NET 10.0                        |
| Biometric SDK | Neurotec Professional SDK 2025.2 |
| Installer     | WiX Toolset                      |
| Frontend      | HTML5, Vanilla JavaScript, CSS   |
| Communication | REST API                         |
| Hosting       | Windows Service                  |

---

# Solution Architecture

The project follows a layered Clean Architecture implementation with a focus on decoupling the native SDK lifecycle from the API layer.

## User flow:
<img width="1135" height="2197" alt="userflow" src="https://github.com/user-attachments/assets/e800dbb4-aaa0-45e8-8c49-3caaad4aca3f" />

## Logical flow :
<img width="1346" height="894" alt="image" src="https://github.com/user-attachments/assets/5f9e139d-f2a3-4e14-afaf-4103c2e7b574" />

## Process flow : 
<img width="1250" height="950" alt="image" src="https://github.com/user-attachments/assets/838f159b-4437-49df-9047-061646aba71a" />

## Project Structure

```text
Neurotec.API
│
├── Minimal API Endpoints (Capture, Status, Cancel)
├── Dashboard UI Hosting
├── Windows Service Hosting
└── HTTP API Layer

Neurotec.Application
│
├── Application Workflows
├── Business Use Cases
└── Service Orchestration

Neurotec.Domain
│
├── Core Entities
├── Interfaces
├── Enums
└── Domain Models

Neurotec.Infrastructure
│
├── Neurotec SDK Integration
├── Device Management
├── Plugin Loading
├── Fingerprint Capture
└── Hardware Communication

Neurotec.Installer
│
├── WiX Installer
└── MSI Packaging
```

---

# Core Functionalities

## Biometric Scanner Integration

The system integrates directly with the Neurotec SDK using `NBiometricClient` for:

* Fingerprint acquisition
* Finger quality assessment
* Device enumeration
* Plugin management
* Template generation

### Supported Functionalities

* Finger scanner auto-detection
* Plugin-based scanner architecture
* Real-time fingerprint capture
* Base64 image generation
* Quality score evaluation

---

## Plugin & Device Management

The system dynamically loads biometric plugins from the application directory.

### Supported Plugin Categories

* Finger Scanners
* Iris Scanners
* Signature Devices
* Cameras
* Multi-modal Devices

### Example Supported Vendors

* CrossMatch

# REST API Endpoints

### Health Status
`GET /api/status`
Returns SDK status, hardware readiness, and detailed diagnostic metadata.

### Device Detection
`GET /api/devices`
Returns a list of connected biometric devices with automatic display-name mapping.

### Fingerprint Capture
`POST /api/capture`
Initiates hardware scan. Supports multiple modes (Thumb, Slap, etc.).

### Graceful Cancel (NEW)
`POST /api/cancel`
Signals the active scanner to stop acquisition and release the hardware safely.

---

# Configuration

Application configuration is managed via `appsettings.json` using strongly-typed options.

## Example Configuration

```json
{
  "NeurotecSdk": {
    "LicenseServer": "/trial",
    "Components": [
      "FingerExtraction",
      "FingerScanners",
      "FingerQualityAssessment"
    ],
    "CaptureSettings": {
      "TimeoutMs": 40000,
      "QualityThreshold": 30
    }
  }
}
```

---

# Build & Deployment

## MSI Installer Support

The project includes enterprise deployment automation.

### Deployment Scripts

| Script               | Purpose                 |
| -------------------- | ----------------------- |
| `build-msi.bat`      | Builds MSI installer    |
| `bundle-release.ps1` | Creates release package |

### Installer Features

* Single-click installation
* Automatic service registration
* Auto-start Windows Service
* Dependency bundling
* Plugin packaging

---

# Installation Prerequisites

Before installation ensure the client machine satisfies the following requirements.

## Operating System

* Windows 10 (64-bit)
* Windows 11 (64-bit)

## Hardware Requirements

* Minimum 4 GB RAM
* Minimum 2 GB free disk space
* USB 2.0/3.0 port

## Required Permissions

* Administrator access required

## Device Drivers

Install official scanner drivers before setup:

* Mantra MFS100/MFS110
* CrossMatch drivers
* Vendor-specific SDK drivers

## Port Requirement

Ensure Port `3000` is available.

---

# Installation Steps

## Step 1 - Run Installer

Execute:

```bash
Neurotec_Biometric_v1.0.msi
```

## Step 2 - Grant Permissions

Allow administrative permissions when prompted by Windows UAC.

## Step 3 - Complete Setup Wizard

Follow the installation wizard:

```text
Next → Install → Finish
```

## Step 4 - Automatic Service Startup

The installer automatically:

* Registers Windows Service
* Starts the service
* Configures startup behavior

No manual service configuration is required.

---


# Setup Verification

## Dashboard Verification
Open: `http://localhost:3000/home`
*   **Ready State**: Green dot indicates SDK is licensed and hardware is connected.
*   **Capturing State**: Orange pulse indicates hardware is active.
*   **Reset System**: Use the top-right button to clear results or interrupt a scan.

## API Verification
Open: `http://localhost:3000/api/status`
Response Example:
```json
{
  "status": "Ready",
  "isReady": true,
  "sdkVersion": "Neurotec Biometric 2025.2 (Pro)",
  "hardwareDetected": true
}
```

---
# Logging & Diagnostics

The system includes professional diagnostics for:

* SDK initialization
* Plugin loading
* Device enumeration
* Finger capture
* License validation
* API operations
* Service lifecycle

---

# Security & Reliability

* Windows Service isolation
* Controlled SDK access
* Plugin sandboxing
* Automatic resource disposal
* Exception handling
* Capture timeout protection
* Centralized configuration management

---
# Available Features

* Show biometric device listing
* Right 4 finger scan
* Right Thumb
* Left 4 finger scan
* Left Thumb


# Troubleshooting

## "Stopping..." Status Hanging
If the dashboard stays on "STOPPING..." for more than 5 seconds, the native SDK may be waiting for a hardware lock. Ensure the scanner is not being used by another application.

## Scanner Not Detected
Ensure the optimized `FScanners` directory in the build folder contains the correct driver DLLs (e.g., `Mantra.dll`, `CrossMatch.dll`).

## MSI Installer Size
The installer should now be significantly smaller (~700MB) following the modality pruning.

---

# Support & Diagnostics

For technical support:
*   Review `service_debug.log` in the application root.
*   Check Windows Event Viewer (Source: `NeurotecBiometricService`).
*   Ensure Port `3000` is open for API communication.
# License

This project uses:

* Neurotec Professional SDK
* WiX Toolset
* .NET 10.0 Runtime

Ensure valid licensing is configured for production environments.

