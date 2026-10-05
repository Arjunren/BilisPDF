# Security model

## Trust boundaries

All selected files, filenames, PDFs, Office ZIP containers, images, settings, and converter output are untrusted. Explorer is a high-stability boundary: EasyPDF performs no document parsing there. LibreOffice is a separate child process operating on a private temporary profile. The optional Microsoft Office fallback runs invisibly on a dedicated STA thread in the out-of-process worker, with macros disabled and sources opened read-only.

## Controls

- Normalize paths with `Path.GetFullPath`; require ordinary local files and reject reparse points.
- Validate signatures/container members in addition to extensions.
- Default limits: 512 MiB per input, 10,000 PDF pages per operation, 100 megapixels per image, 100 files per command.
- Use `ProcessStartInfo.ArgumentList` with `UseShellExecute=false`; never concatenate a command line or invoke a shell.
- Resolve LibreOffice to an absolute existing executable and use a unique user profile.
- Disable Office automation macros, alerts, recent-file insertion, editing, and visible windows.
- Impose conversion timeout and cancellation; kill the process tree on failure.
- Write to a random temporary file in the destination directory, then atomically move without overwriting.
- Omit filenames, paths, metadata, text, and passwords from diagnostics.
- Never store passwords. Password cracking or bypass is out of scope.

## Residual risk

Complex native parsers can contain vulnerabilities. Keep .NET, PDFsharp, and LibreOffice updated; process risky formats with least privilege; and code-sign production binaries. A future hardening milestone should add AppContainer/Windows Sandbox isolation for office conversion and fuzzing corpora for PDF/page-range/image validators.
