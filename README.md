# EasyPDF

EasyPDF is a free, open-source, offline-first Windows utility for common PDF tasks. Select files in File Explorer, use an EasyPDF action, or drag files onto the desktop app. Documents stay on the local computer.

Copyright and project credit: **Arjunren Von** (`Arjunren`). Licensed under the MIT License.

> Status: Phase 1 foundation. The CLI, desktop drag-and-drop shell, guarded local LibreOffice conversion, PDF merge/split/extract/rotate, images-to-PDF, tests, and MSI authoring are implemented. The Windows 11 first-tier signed shell package remains a release-engineering item; the MSI installs lightweight file-type-specific classic Explorer commands that are available directly on Windows 10 and under **Show more options** on Windows 11.

## Features

- Convert DOCX, XLSX, and PPTX to PDF through a local LibreOffice installation.
- Merge PDFs, extract/split page ranges, and rotate pages.
- Combine PNG, JPEG, BMP, TIFF, and WebP images into a PDF.
- Use the same processing engine from the CLI and desktop app.
- Safe output naming: existing files are not overwritten by default.
- No accounts, ads, telemetry, subscriptions, credits, or cloud uploads.
- Local history is disabled by default.

## Requirements

- Windows 10/11 x64.
- A self-contained release build, or the .NET 10 Desktop Runtime for framework-dependent builds.
- LibreOffice for DOCX/XLSX/PPTX conversion. EasyPDF locates the normal 64-bit or 32-bit installation, or uses `EASYPDF_LIBREOFFICE_PATH` when explicitly configured.

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

## Architecture

- `EasyPDF.Core`: input models, file type registry, validation, page ranges, and safe output naming.
- `EasyPDF.Pdf`: PDFsharp-backed PDF and image operations with atomic output.
- `EasyPDF.Converters`: isolated, time-bounded LibreOffice worker invocation.
- `EasyPDF.Infrastructure`: privacy-preserving settings and rotating diagnostics.
- `EasyPDF.Cli`: independent command-line worker.
- `EasyPDF.Desktop`: accessible WPF drag-and-drop UI.
- `EasyPDF.Shell`: tiny Explorer-to-desktop dispatcher; it never processes documents inside Explorer.
- `installer`: WiX MSI authoring, Explorer registration, shortcuts, terms, and uninstall cleanup.

See [Architecture](docs/ARCHITECTURE.md), [Security model](docs/SECURITY-MODEL.md), [dependency evaluation](docs/DEPENDENCIES.md), and [release process](docs/RELEASING.md).

## Privacy

EasyPDF performs document processing locally. It contains no telemetry or network client. Diagnostic logs omit document names and paths. Operation history is disabled by default and never stores document contents.

## Security

Treat documents as untrusted. EasyPDF validates signatures and container structure, rejects reparse-point inputs, limits file/page/image sizes, uses random temporary directories, invokes converters without a shell, imposes timeouts, and writes through a temporary file before moving a result into place. See [SECURITY.md](SECURITY.md) to report a vulnerability.

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md) before opening a change.
