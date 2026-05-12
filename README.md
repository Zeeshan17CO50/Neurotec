# Neurotec VFS - Enterprise Biometric Fingerprint Scanning Solution

## Overview

**Neurotec VFS** is a professional-grade biometric fingerprint scanning system developed for modernizing and replacing legacy VFS biometric solutions using the latest **.NET 10.0** and **Neurotec Professional SDK (2025.2)** architecture.

The solution is designed as a scalable, enterprise-ready Windows Service application that provides:

* Real-time fingerprint acquisition
* Automatic biometric device detection
* REST API-based biometric operations
* Centralized SDK/plugin management
* Modern lightweight dashboard UI
* MSI-based deployment support

The application follows **Clean Architecture principles** to ensure maintainability, scalability, and production reliability.

---

# Key Features

* Enterprise-grade biometric fingerprint capture
* Neurotec Professional SDK integration
* CrossMatch, Mantra, Tatvik, and multiple scanner plugin support
* Automatic hardware detection and plugin loading
* RESTful API communication
* Windows Service support
* Lightweight web dashboard UI
* SQLite-based biometric storage
* MSI installer support using WiX Toolset
* Production-ready logging and diagnostics
* Centralized SDK configuration management

---

# Technology Stack

| Component     | Technology                       |
| ------------- | -------------------------------- |
| Framework     | .NET 10.0                        |
| Biometric SDK | Neurotec Professional SDK 2025.2 |
| Database      | SQLite                           |
| Installer     | WiX Toolset                      |
| Frontend      | HTML5, Vanilla JavaScript, CSS   |
| Communication | REST API                         |
| Hosting       | Windows Service                  |

---

# Solution Architecture

The project follows a layered Clean Architecture implementation.

## Project Structure

```text
Neurotec.API
│
├── Minimal API Endpoints
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
* Mantra
* Tatvik
* Suprema
* Futronic
* SecuGen
* Nitgen

---

## REST API Endpoints

### Health Status

```http
GET /api/status
```

Returns:

* SDK status
* Device availability
* Service readiness

### Device Detection

```http
GET /api/devices
```

Returns:

* Connected biometric devices
* Scanner metadata

### Fingerprint Capture

```http
POST /api/capture
```

Returns:

* Captured fingerprint image
* Template data
* Quality score

---

# Configuration

Application configuration is managed centrally using `appsettings.json`.

## Example Configuration

```json
{
  "ServiceSettings": {
    "Port": 3000,
    "ServiceName": "Neurotec Biometric Service"
  },
  "NeurotecSdk": {
    "LicenseServer": "/trial",
    "QualityThreshold": 30,
    "CaptureTimeout": 10
  }
}
```

### Configuration Includes

* HTTP port
* Service name
* License server
* Capture timeout
* Quality threshold
* Database paths

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

Open:

```text
http://localhost:3000/home
```

Expected Result:

* Dashboard loads successfully

## Device Verification

Connect biometric scanner.

Expected Result:

* Scanner name appears on dashboard

## API Verification

Open:

```text
http://localhost:3000/api/status
```

Expected Response:

```json
{
  "isReady": true
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

## Dashboard Not Loading

Verify:

* Windows Service is running
* Port 3000 is free
* Firewall/Antivirus is not blocking `Neurotec.API.exe`

## Scanner Not Detected

Verify:

* Device drivers installed
* USB connection working
* Plugin DLLs available
* Vendor service running

## Capture Hanging

Verify:

* `.ndf` model files exist
* SDK license obtained
* Finger scanner initialized
* Device permissions available

---

# Future Enhancements

Planned extensibility includes:

* Multi-finger slap capture
* Palm scanning
* Iris scanning
* Face biometrics
* Distributed licensing
* Cloud synchronization
* Advanced audit logging

---

# Support

For technical support:

* Contact internal support team
* Verify deployment logs
* Check Windows Event Viewer
* Review SDK/plugin diagnostics

---

# License

This project uses:

* Neurotec Professional SDK
* WiX Toolset
* .NET 10.0 Runtime

Ensure valid licensing is configured for production environments.

