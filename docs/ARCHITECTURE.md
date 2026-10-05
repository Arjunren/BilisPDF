# Architecture and Phase 1 plan

## Decision summary

EasyPDF targets .NET 10 LTS and Windows 10/11 x64. WPF is used for a small, fast desktop UI; processing stays in reusable libraries and the CLI. A shell entry launches the app/worker and returns immediately. No heavy dependency is loaded in Explorer.

```text
Explorer / WPF / CLI
        |
        v
 EasyPDF.Core contracts and validation
    |                         |
    v                         v
EasyPDF.Converters       EasyPDF.Pdf
 (LibreOffice process)   (PDFsharp)
    \                         /
     v                       v
       atomic local outputs
```

## Repository structure

The source is split by responsibility rather than feature microservices. `Core` has no UI dependency. `Pdf` and `Converters` implement narrow interfaces. `Cli`, `Desktop`, and `Shell` are adapters. Test assemblies separate deterministic unit tests, process/file integration tests, and hostile-input regression tests.

## Explorer design

The MSI registers verbs only for supported extensions under `SystemFileAssociations`; it does not change default applications. The tiny `EasyPDF.Shell.exe` validates the verb and starts `EasyPDF.Desktop.exe`, which owns all UI and processing. Uninstall removes every registration.

Windows 11's first-tier context menu requires a signed package identity plus a native `IExplorerCommand` COM server. Production releases should add the documented sparse-package shell component, keep the same out-of-process dispatcher boundary, and sign the package in CI. Until then the MSI commands are available on Windows 10 and in Windows 11's classic **Show more options** menu. This limitation is explicit so unsigned development builds do not pretend to provide a modern registration they cannot install safely.

## Phase 1 implementation sequence

1. Core validation, routing, naming, cancellation contracts, and tests.
2. PDF merge/extract/split/rotate and images-to-PDF.
3. Isolated LibreOffice conversion for DOCX/XLSX/PPTX.
4. CLI composition and exit-code contract.
5. WPF drag-and-drop UI and settings.
6. Shell dispatcher and MSI with mandatory terms acceptance.
7. CI, release artifacts, dependency review, and signing documentation.

## Technical risks

- LibreOffice fidelity can differ from Microsoft Office for complex documents and unavailable fonts. Releases must disclose this and test representative files.
- LibreOffice redistribution includes many component notices. The current installer detects an existing installation; bundling it requires copying its full license/notice set and substantially increases installer size.
- PDFsharp does not render PDFs and cannot compress arbitrary existing page content. Phase 2 rendering/compression needs a separately reviewed engine.
- Windows 11 first-tier Explorer integration cannot be shipped unsigned. CI must sign both the application package and installer; secrets never belong in the repository.
- WPF is mature and accessible but not WinUI 3. The tradeoff avoids a large runtime and packaging surface in the MVP while retaining a native Windows UI.
