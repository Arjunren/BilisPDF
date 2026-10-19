// Exercise the actual command implementation without registering it in Explorer.
#include "../../src/EasyPDF.ShellExtension/ShellExtension.cpp"
#include <iostream>
#include <cstdlib>

void Check(bool condition)
{
    if (!condition) { std::cerr << "Shell extension check failed\n"; std::exit(1); }
}

int main()
{
    Check(SupportsAction(Action::Root, { L"report.DOCX" }));
    Check(SupportsAction(Action::Convert, { L"report.docx", L"sheet.XLSX" }));
    Check(!SupportsAction(Action::Convert, { L"photo.png" }));
    Check(SupportsAction(Action::Merge, { L"one.pdf", L"two.PDF" }));
    Check(!SupportsAction(Action::Merge, { L"one.pdf" }));
    Check(SupportsAction(Action::Extract, { L"one.pdf" }));
    Check(!SupportsAction(Action::Rotate, { L"one.pdf", L"two.pdf" }));
    Check(SupportsAction(Action::Images, { L"photo.webp", L"photo.JPG" }));
    Check(!SupportsAction(Action::Images, { L"photo.jpg", L"report.pdf" }));
    Check(!SupportsAction(Action::Root, { L"program.exe" }));
    Check(!SupportsAction(Action::Root, {}));
    Check(!SupportsAction(Action::Root, std::vector<std::wstring>(101, L"one.pdf")));
    Check(QuoteArgument(L"C:\\my files\\report.docx") == L"\"C:\\my files\\report.docx\"");
    Check(QuoteArgument(L"trailing\\") == L"\"trailing\\\\\"");
    Check(QuoteArgument(L"a\"b") == L"\"a\\\"b\"");

    auto* root = new ExplorerCommand();
    EXPCMDFLAGS flags{};
    Check(root->GetFlags(&flags) == S_OK && flags == ECF_HASSUBCOMMANDS);
    IEnumExplorerCommand* commands = nullptr;
    Check(root->EnumSubCommands(&commands) == S_OK);
    Check(commands->Next(2, nullptr, nullptr) == E_POINTER);
    IExplorerCommand* child = nullptr;
    Check(commands->Next(1, &child, nullptr) == S_OK);
    PWSTR title = nullptr;
    Check(child->GetTitle(nullptr, &title) == S_OK && std::wstring(title) == L"Convert to PDF");
    CoTaskMemFree(title);
    Check(child->GetFlags(&flags) == S_OK && flags == ECF_DEFAULT);
    child->Release();
    IEnumExplorerCommand* clone = nullptr;
    Check(commands->Clone(&clone) == S_OK);
    Check(clone->Skip(5) == S_OK);
    Check(clone->Next(1, &child, nullptr) == S_FALSE);
    Check(clone->Reset() == S_OK);
    Check(clone->Skip(100) == S_FALSE);
    clone->Release();
    Check(commands->Reset() == S_OK);
    IExplorerCommand* all[7]{};
    ULONG fetched = 0;
    Check(commands->Next(7, all, &fetched) == S_FALSE && fetched == 6);
    for (ULONG i = 0; i < fetched; ++i) { all[i]->Release(); }
    commands->Release();
    root->Release();
    Check(DllCanUnloadNow() == S_OK);
    std::cout << "Native shell checks passed: selection, quoting, submenu enumeration, COM lifetime.\n";
}
