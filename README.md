# EasyPDF

EasyPDF is a free, open-source, offline-first Windows utility for common PDF tasks. Select files in File Explorer, use an EasyPDF action, or drag files onto the desktop app. Documents stay on the local computer.

Copyright and project credit: **Arjunren Von** (`Arjunren`). Licensed under the MIT License.

> Status: 0.3 development build. Includes a native Windows 11 EasyPDF submenu, a shared app icon, and a refreshed desktop workflow. Public releases require a trusted signing certificate for the modern Explorer menu. Unsigned builds support modern-menu registration only on machines with Developer Mode already enabled; otherwise use **Show more options → EasyPDF**.

## Features

- Right-click supported files and choose **EasyPDF** in the Windows 11 menu, alongside apps such as WinRAR. The submenu shows actions for the current selection: document conversion, PDF merge/extract/rotate, combine images, or open in EasyPDF. Document conversion runs in the background.
- Convert DOCX, XLSX, and PPTX through local LibreOffice, or through an already-installed Microsoft Office application when LibreOffice is unavailable.
- Merge PDFs, extract/split page ranges, and rotate pages.
- Combine PNG, JPEG, BMP, TIFF, and WebP images into a PDF.
- Use the same processing engine from the CLI and desktop app.
- Add files over several selections, remove or reorder them, and see only the controls needed for the selected operation. Progress, actionable errors, and the output location appear in the app.
- Consistent icon in Explorer, the app window, executable, shortcuts, package logos, and installed-app listing.
- Safe output naming: existing files are not overwritten by default.
- No accounts, ads, telemetry, subscriptions, credits, or cloud uploads.
- Local history is disabled by default.

## Requirements

- Windows 10/11 x64.
- A self-contained release build, or the .NET 10 Desktop Runtime for framework-dependent builds.
- LibreOffice or Microsoft Office for DOCX/XLSX/PPTX conversion. EasyPDF prefers LibreOffice, then falls back to an already-installed Word, Excel, or PowerPoint application. Conversion remains local in either case.

## CLI

```powershell
easypdf convert-to-pdf "C:\Documents\Report.docx"
easypdf merge one.pdf two.pdf --output Merged.pdf
easypdf extract-pages Report.pdf --pages "1-3,8" --output Extracted.pdf
easypdf split Report.pdf --pages "1-3,8" --output Split.pdf
easypdf rotate Report.pdf --degrees 90 --pages "1,3-5"
easypdf images-to-pdf one.jpg two.png --output Photos.pdf
```

Run `easypdf help` for the complete command summary. Exit code `0` means success, `2` means invalid input, `3` means processing failed, and `130` means cancelled.

## Build and test

```powershell
dotnet restore EasyPDF.slnx
dotnet build EasyPDF.slnx -c Release --no-restore
dotnet test EasyPDF.slnx -c Release --no-build
```

Publish the app payload before building the MSI:

```powershell
./installer/build.ps1
```

The installer displays the EasyPDF terms and requires acceptance before installation. It registers only EasyPDF verbs; it does not take over the system PDF association.

For a persistent local Windows 11 development installation on a machine with Developer Mode already enabled, build the payload, then run:

```powershell
./installer/Install-Development.ps1
```

This installs the development build in `%LOCALAPPDATA%\Programs\EasyPDF`, registers its shell extension for the current user, and updates that user's desktop and Start menu shortcuts. Close EasyPDF before updating. The existing machine-wide MSI installation is separate and is not upgraded by this script.

To repair a development registration from an installed payload, run its `Register-DevelopmentPackage.ps1`. It validates the payload and registers the manifest **beside the installed binaries**, not in `artifacts` or another temporary build folder. Keep that installation folder in place. Close and reopen Explorer windows after repair; if Windows still caches an older extension, sign out and back in.

The modern menu uses a sparse package because Windows 11 requires package identity for `IExplorerCommand`. With `EASYPDF_SIGNING_CERT_PATH` set, the build signs the shell package and the MSI registers it automatically. Without signing, the MSI registers the development manifest only when Developer Mode is already enabled. It does not change that Windows setting. Set the certificate path and `EASYPDF_SIGNING_CERT_PASSWORD` only in the build environment; certificate files and passwords must never be committed.

The native submenu has an assert-based self-check in `tests/native/ShellExtensionTests.vcxproj`; build it with Visual Studio C++ MSBuild (`Release|x64`) and run `artifacts/native-tests/ShellExtensionTests.exe`. CI runs it along with the .NET and desktop workflow tests.

## Architecture

- `EasyPDF.Core`: input models, file type registry, validation, page ranges, and safe output naming.
- `EasyPDF.Pdf`: PDFsharp-backed PDF and image operations with atomic output.
- `EasyPDF.Converters`: guarded local LibreOffice conversion plus an invisible, macro-disabled Microsoft Office fallback.
- `EasyPDF.Infrastructure`: privacy-preserving settings and rotating diagnostics.
- `EasyPDF.Cli`: independent command-line worker.
- `EasyPDF.Desktop`: accessible WPF drag-and-drop UI.
- `EasyPDF.Shell`: out-of-process Explorer worker; direct conversion runs here and never inside Explorer.
- `EasyPDF.ShellExtension`: native `IExplorerCommand` COM server for Windows 11's first-tier context menu.
- `installer`: WiX MSI authoring, Explorer registration, shortcuts, terms, and uninstall cleanup.

See [Architecture](docs/ARCHITECTURE.md), [Security model](docs/SECURITY-MODEL.md), [dependency evaluation](docs/DEPENDENCIES.md), and [release process](docs/RELEASING.md).

## Privacy

EasyPDF performs document processing locally. It contains no telemetry or network client. Diagnostic logs omit document names and paths. Operation history is disabled by default and never stores document contents.

## Security

Treat documents as untrusted. EasyPDF validates signatures and container structure, rejects reparse-point inputs, limits file/page/image sizes, uses random temporary directories, invokes converters without a shell, imposes timeouts, and writes through a temporary file before moving a result into place. See [SECURITY.md](SECURITY.md) to report a vulnerability.

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md) before opening a change.
