# Neurotec SDK 2025.2 Integration Guide (Trial)

The project is now configured to use the **Neurotec_Biometric_2025_2_SDK_2026-04-03.zip** package.

## ⚠️ Important: Manual Extraction Required
Due to system group policy restrictions, I cannot extract the ZIP file automatically. Please perform the following steps:

1.  **Locate the SDK**: Go to `c:\Users\user\Desktop\Zeeshan\Vibe Coding\POC\Neurotec\sdk\Neurotec_Biometric_2025_2_SDK_2026-04-03.zip`.
2.  **Extract the Binaries**: Open the ZIP and navigate to `Bin\Win64_x64`.
3.  **Copy to Libs**: Copy all contents of that `Bin\Win64_x64` folder into this directory:
    `c:\Users\user\Desktop\Zeeshan\Vibe Coding\POC\Neurotec\src\libs\Neurotec\`

## Files Required in this Directory:
- **.NET Assemblies**: `Neurotec.dll`, `Neurotec.Biometrics.dll`, `Neurotec.Biometrics.Client.dll`, `Neurotec.Devices.dll`, `Neurotec.Licensing.dll`, `Neurotec.Media.dll`.
- **Native DLLs**: All other `.dll` files from the `Bin\Win64_x64` folder.
- **Activation**: Run `Activation\pgm\Activation.exe` from the ZIP to activate your machine.
