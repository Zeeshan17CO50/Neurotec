# Neurotec VFS - Enterprise Biometric Fingerprint Scanning Solution

## Overview

**Neurotec VFS** is a professional-grade biometric fingerprint scanning system developed for modernizing and replacing legacy VFS biometric solutions using the latest **.NET 10.0** and **Neurotec Professional SDK (2025.2)** architecture.

The solution is designed as a scalable, enterprise-ready Windows Service application that provides:

* **Professional Sequential Capture Workflow**: Specialized 4-4-2 enrollment process.
* **Per-Finger Quality Assessment**: Granular scoring for individual fingers within slap captures.
* **Automatic Progression**: Smart stage advancement based on configurable quality thresholds.
* **Real-time Biometric Acquisition**: Powered by optimized native SDK plugins.
* **Modern Dashboard UI**: A state-of-the-art "Glassmorphism" interface with full-screen error isolation.
* **Graceful Lifecycle Management**: REST API-based operations including hardware-safe cancellation and system resets.

The application follows **Clean Architecture principles** and **SOLID patterns** to ensure production-grade reliability and extreme maintainability.

---

# Key Features

* **Sequential 4-4-2 Enrollment**: Implements the industry-standard workflow: Right 4 Fingers → Left 4 Fingers → Both Thumbs.
* **Granular Biometric Metadata**: Extracts and displays individual quality scores for every finger in a slap (Index, Middle, Ring, Little).
* **Automatic Stage Advancement**: Automatically progresses to the next capture stage once the quality threshold (default: 70) is met.
* **Graceful Reset Mechanism**: Allows interrupting active hardware scans or wiping the current enrollment session safely.
* **Full-Screen Error Isolation**: Robust error handling that masks the dashboard during hardware failures to prevent UI state corruption.
* **Consolidated JSON Output**: Generates a unified biometric data structure across all capture stages for easy integration.
* **RESTful API Architecture**: Standardized communication for Status, Detection, Capture, and Cancellation.
* **Windows Service Support**: Native integration for background execution and automatic startup.
* **Modern Dashboard UI**: Built with responsive Vanilla CSS and glassmorphism effects.
* **MSI Deployment**: Automated silent installation using WiX Toolset.
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
├── Minimal API Endpoints (Sequential Capture Logic)
├── Dashboard UI Hosting (index.html with State Machine)
├── Windows Service Hosting
└── HTTP API Layer

Neurotec.Application
│
├── Application Workflows (Enrollment Orchestration)
├── Business Use Cases
└── Service Orchestration

Neurotec.Domain
│
├── Core Entities (BiometricData with FingerScores)
├── Interfaces (IBiometricScanner)
├── Enums (TwoThumbs support)
└── Domain Models

Neurotec.Infrastructure
│
├── Neurotec SDK Integration (NBiometricClient)
├── Slap Segmentation (Individual Finger Extraction)
├── Plugin Loading & Device Management
└── Fingerprint Capture & Quality Normalization
└── Hardware Communication

Neurotec.Installer
│
├── WiX Installer
└── MSI Packaging
```

---

# Core Functionalities

## Sequential Biometric Workflow

The system implements a professional enrollment sequence that ensures all required biometrics are captured with high quality before completion.

* **Stage 1**: Right 4 Fingers (Slap)
* **Stage 2**: Left 4 Fingers (Slap)
* **Stage 3**: Both Thumbs (4-4-2 Workflow)

### Per-Finger Extraction
During slap captures (4 fingers), the infrastructure layer automatically segments the image and extracts individual quality scores (0-100) for:
*   `Index`, `Middle`, `Ring`, `Little`
*   `Left Thumb`, `Right Thumb` (in Two-Thumb mode)

### Supported Functionalities

* Finger scanner auto-detection
* Plugin-based scanner architecture
* Real-time fingerprint capture
* Base64 image generation
* Quality score evaluation

---

## Plugin & Device Management

The system dynamically loads biometric plugins from the application directory, supporting a wide range of hardware through a unified interface.

### Supported Plugin Categories

* Finger Scanners (Optimized Footprint)
* Multi-finger Slap Scanners
* Two-Thumb Capture Devices

### Example Supported Vendors

* CrossMatch (Patrol/Guardian series)
* Mantra (MFS series)

# REST API Endpoints

### Health Status
`GET /api/status`
Returns SDK status, hardware readiness, version info, and hardware detection state.

### Device Detection
`GET /api/devices`
Returns a list of connected biometric devices with automatic display-name mapping.

### Fingerprint Capture
`POST /api/capture`
Initiates hardware scan. Supports mode mapping:
*   `3`: Right 4-Finger
*   `2`: Left 4-Finger
*   `4`: Two-Thumbs (Plain Thumbs)

### Graceful Cancel
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
      "QualityThreshold": 70
    }
  }
}
```

---

# Build & Deployment

## MSI Installer Support

The project includes enterprise deployment automation for Windows Service environments.

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
* Windows 10/11 (64-bit)

## Hardware Requirements
* Minimum 4 GB RAM
* Minimum 2 GB free disk space
* USB 2.0/3.0 port

## Device Drivers
Install official scanner drivers before setup:
* Mantra MFS100/MFS110
* CrossMatch Patrol ID / Guardian
* Vendor-specific HID drivers

## Port Requirement
Ensure Port `3000` is available for the API and Dashboard.

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
*   **Workflow steps**: Top buttons indicate capture progress (R4 -> L4 -> Thumbs).
*   **Ready State**: Green dot indicates hardware is connected and SDK is active.
*   **Reset System**: Located in the control panel to clear enrollment data or cancel a scan.
*   **Capturing State**: Orange pulse indicates hardware is active.
*   **Completion**: "Capture Complete (Click to Reset)" button appears after Stage 3.

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
* SDK initialization & Plugin loading
* Device enumeration & License validation
* Finger capture & API operations
* Slap segmentation & Quality extraction
* Hardware connectivity & Service lifecycle

---

# Security & Reliability
* **Capture Timeout Protection**: Prevents hardware from locking up indefinitely.
* **Error Isolation**: Dashboard-level error masking during biometric failures.
* **Graceful Resource Disposal**: Ensures the Neurotec SDK client is properly disposed on service stop.

---

# Available Capture Modes
* **Right 4 finger scan** (Slap)
* **Left 4 finger scan** (Slap)
* **Both Thumbs** (4-4-2 mode)
* **Single Finger** (Thumb/Index/etc.)

---

# Troubleshooting

## "Stopping..." Status Hanging
If the dashboard stays on "STOPPING..." for more than 5 seconds, the native SDK may be waiting for a hardware lock. Ensure the scanner is not being used by another application.

## Scanner Not Detected
Ensure the `FScanners` directory contains the correct driver DLLs for your vendor. Verify the service is running in `services.msc`.

## MSI Installer Size
The installer should now be significantly smaller (~700MB) following the modality pruning.

## Low Quality Scores
Ensure the `QualityThreshold` in `appsettings.json` is set correctly (default: 70). Lower thresholds may be required for older scanners.

---

# Support & Diagnostics
* Review `service_debug.log` in the application root.
* Check Windows Event Viewer (Source: `NeurotecBiometricService`).
* Ensure Port `3000` is open for API communication.

# License

This project uses:
* Neurotec Professional SDK
* WiX Toolset
* .NET 10.0 Runtime

Ensure valid licensing is configured for production environments.
