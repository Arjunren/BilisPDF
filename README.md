# BilisPDF

BilisPDF is a free, open-source, offline-first Windows utility for common PDF tasks. Select files in File Explorer, use an BilisPDF action, or drag files onto the desktop app. Documents stay on the local computer.

Copyright and project credit: **Arjunren Von** (`Arjunren`). Licensed under the MIT License.

> Status: 0.4 unsigned preview release, formerly EasyPDF. Windows may display **Unknown publisher** or a SmartScreen warning because this build has no trusted code signature. The modern Explorer menu is available on machines with Developer Mode already enabled; otherwise use **Show more options → BilisPDF**. Signing is deferred, not claimed as verified.

Download the Windows x64 installer from [GitHub Releases](https://github.com/Arjunren/BilisPDF/releases). Compare its SHA-256 with `SHA256SUMS.txt` from the same release. A checksum checks file integrity; it is not a publisher certificate.

## Features

- Right-click supported files and choose **BilisPDF** in the Windows 11 menu, alongside apps such as WinRAR. The submenu shows actions for the current selection: document conversion, PDF merge/extract/rotate, combine images, or open in BilisPDF. Document conversion runs in the background.
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
- LibreOffice or Microsoft Office for DOCX/XLSX/PPTX conversion. BilisPDF prefers LibreOffice, then falls back to an already-installed Word, Excel, or PowerPoint application. Conversion remains local in either case.

## CLI

```powershell
bilispdf convert-to-pdf "C:\Documents\Report.docx"
bilispdf merge one.pdf two.pdf --output Merged.pdf
bilispdf extract-pages Report.pdf --pages "1-3,8" --output Extracted.pdf
bilispdf split Report.pdf --pages "1-3,8" --output Split.pdf
bilispdf rotate Report.pdf --degrees 90 --pages "1,3-5"
bilispdf images-to-pdf one.jpg two.png --output Photos.pdf
```

Run `bilispdf help` for the complete command summary. Exit code `0` means success, `2` means invalid input, `3` means processing failed, and `130` means cancelled.

## Build and test

```powershell
dotnet restore BilisPDF.slnx
dotnet build BilisPDF.slnx -c Release --no-restore
dotnet test BilisPDF.slnx -c Release --no-build
```

Publish the app payload before building the MSI:

```powershell
./installer/build.ps1
```

The installer displays the BilisPDF terms and requires acceptance before installation. It registers only BilisPDF verbs; it does not take over the system PDF association.

For a persistent local Windows 11 development installation on a machine with Developer Mode already enabled, build the payload, then run:

```powershell
./installer/Install-Development.ps1
```

This installs the development build in `%LOCALAPPDATA%\Programs\BilisPDF`, registers its shell extension for the current user, and updates that user's desktop and Start menu shortcuts. Close BilisPDF before updating. The existing machine-wide MSI installation is separate and is not upgraded by this script.

To repair a development registration from an installed payload, run its `Register-DevelopmentPackage.ps1`. It validates the payload and registers the manifest **beside the installed binaries**, not in `artifacts` or another temporary build folder. Keep that installation folder in place. Close and reopen Explorer windows after repair; if Windows still caches an older extension, sign out and back in.

The modern menu uses a sparse package because Windows 11 requires package identity for `IExplorerCommand`. The MSI registers the development manifest only when Developer Mode is already enabled; it does not change that Windows setting. A trusted signed release will register its signed shell package automatically. See [release signing](docs/RELEASING.md) for the certificate-store signing workflow.

BilisPDF preserves the original Windows package identity, COM class ID, MSI upgrade code, and settings directory so the rename can upgrade existing registrations and keep user preferences. All displayed product names and executable names use BilisPDF. The MSI upgrades the previous machine-wide EasyPDF installation; the development installer replaces only its old user shortcuts and registration, leaving the old binaries in place.

The native submenu has an assert-based self-check in `tests/native/ShellExtensionTests.vcxproj`; build it with Visual Studio C++ MSBuild (`Release|x64`) and run `artifacts/native-tests/ShellExtensionTests.exe`. CI runs it along with the .NET and desktop workflow tests.

## Architecture

- `BilisPDF.Core`: input models, file type registry, validation, page ranges, and safe output naming.
- `BilisPDF.Pdf`: PDFsharp-backed PDF and image operations with atomic output.
- `BilisPDF.Converters`: guarded local LibreOffice conversion plus an invisible, macro-disabled Microsoft Office fallback.
- `BilisPDF.Infrastructure`: privacy-preserving settings and rotating diagnostics.
- `BilisPDF.Cli`: independent command-line worker.
- `BilisPDF.Desktop`: accessible WPF drag-and-drop UI.
- `BilisPDF.Shell`: out-of-process Explorer worker; direct conversion runs here and never inside Explorer.
- `BilisPDF.ShellExtension`: native `IExplorerCommand` COM server for Windows 11's first-tier context menu.
- `installer`: WiX MSI authoring, Explorer registration, shortcuts, terms, and uninstall cleanup.

See [Architecture](docs/ARCHITECTURE.md), [Security model](docs/SECURITY-MODEL.md), [dependency evaluation](docs/DEPENDENCIES.md), and [release process](docs/RELEASING.md).

## Privacy

BilisPDF performs document processing locally. It contains no telemetry or network client. Diagnostic logs omit document names and paths. Operation history is disabled by default and never stores document contents.

## Security

Treat documents as untrusted. BilisPDF validates signatures and container structure, rejects reparse-point inputs, limits file/page/image sizes, uses random temporary directories, invokes converters without a shell, imposes timeouts, and writes through a temporary file before moving a result into place. See [SECURITY.md](SECURITY.md) to report a vulnerability.

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md) before opening a change.
