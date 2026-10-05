#include <windows.h>
#include <shobjidl_core.h>
#include <shellapi.h>

#include <algorithm>
#include <atomic>
#include <cwctype>
#include <new>
#include <string>
#include <vector>

#define RETURN_IF_FAILED(expression) do { const HRESULT status = (expression); if (FAILED(status)) return status; } while (false)

namespace
{
constexpr CLSID CLSID_EasyPdfConvert =
{0xac712ee0, 0x68e1, 0x42ae, {0xa1, 0x5a, 0xe8, 0x41, 0x17, 0x7e, 0x98, 0x29}};

HMODULE moduleHandle = nullptr;
std::atomic<long> objectCount = 0;
std::atomic<long> lockCount = 0;

HRESULT DuplicateString(const wchar_t* source, PWSTR* destination)
{
    if (destination == nullptr)
    {
        return E_POINTER;
    }

    *destination = nullptr;
    const auto bytes = (wcslen(source) + 1) * sizeof(wchar_t);
    auto* copy = static_cast<PWSTR>(CoTaskMemAlloc(bytes));
    if (copy == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    memcpy(copy, source, bytes);
    *destination = copy;
    return S_OK;
}

std::wstring ToLower(std::wstring value)
{
    std::ranges::transform(value, value.begin(), [](wchar_t character)
    {
        return static_cast<wchar_t>(std::towlower(character));
    });
    return value;
}

bool IsSupportedDocument(const std::wstring& path)
{
    const auto separator = path.find_last_of(L'.');
    if (separator == std::wstring::npos)
    {
        return false;
    }

    const auto extension = ToLower(path.substr(separator));
    return extension == L".docx" || extension == L".xlsx" || extension == L".pptx" ||
        extension == L".txt" || extension == L".rtf" || extension == L".html" || extension == L".htm";
}

HRESULT GetSelectedPaths(IShellItemArray* items, std::vector<std::wstring>& paths)
{
    if (items == nullptr)
    {
        return E_INVALIDARG;
    }

    DWORD count = 0;
    RETURN_IF_FAILED(items->GetCount(&count));
    if (count == 0 || count > 100)
    {
        return E_INVALIDARG;
    }

    paths.reserve(count);
    for (DWORD index = 0; index < count; ++index)
    {
        IShellItem* item = nullptr;
        RETURN_IF_FAILED(items->GetItemAt(index, &item));
        PWSTR path = nullptr;
        const HRESULT result = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
        item->Release();
        if (FAILED(result))
        {
            CoTaskMemFree(path);
            return result;
        }

        paths.emplace_back(path);
        CoTaskMemFree(path);
    }

    return S_OK;
}

std::wstring QuoteArgument(const std::wstring& value)
{
    std::wstring result = L"\"";
    std::size_t slashCount = 0;
    for (const wchar_t character : value)
    {
        if (character == L'\\')
        {
            ++slashCount;
            continue;
        }

        if (character == L'\"')
        {
            result.append(slashCount * 2 + 1, L'\\');
            result.push_back(character);
            slashCount = 0;
            continue;
        }

        result.append(slashCount, L'\\');
        slashCount = 0;
        result.push_back(character);
    }

    result.append(slashCount * 2, L'\\');
    result.push_back(L'\"');
    return result;
}

HRESULT GetWorkerPath(std::wstring& workerPath)
{
    std::vector<wchar_t> modulePath(32768);
    const DWORD length = GetModuleFileNameW(moduleHandle, modulePath.data(), static_cast<DWORD>(modulePath.size()));
    if (length == 0 || length >= modulePath.size())
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    workerPath.assign(modulePath.data(), length);
    const auto separator = workerPath.find_last_of(L"\\/");
    if (separator == std::wstring::npos)
    {
        return E_UNEXPECTED;
    }

    workerPath.resize(separator + 1);
    workerPath.append(L"EasyPDF.Shell.exe");
    if (GetFileAttributesW(workerPath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
    }

    return S_OK;
}

class ExplorerCommand final : public IExplorerCommand
{
public:
    ExplorerCommand() { ++objectCount; }

    IFACEMETHODIMP QueryInterface(REFIID interfaceId, void** result) override
    {
        if (result == nullptr)
        {
            return E_POINTER;
        }

        *result = nullptr;
        if (interfaceId == IID_IUnknown || interfaceId == __uuidof(IExplorerCommand))
        {
            *result = static_cast<IExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override { return ++referenceCount_; }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG remaining = --referenceCount_;
        if (remaining == 0)
        {
            delete this;
        }
        return remaining;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
    {
        return DuplicateString(L"Convert to PDF with EasyPDF", title);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
    {
        if (icon != nullptr)
        {
            *icon = nullptr;
        }
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tooltip) override
    {
        return DuplicateString(L"Create a PDF beside the selected document without opening EasyPDF", tooltip);
    }

    IFACEMETHODIMP GetCanonicalName(GUID* canonicalName) override
    {
        if (canonicalName == nullptr)
        {
            return E_POINTER;
        }
        *canonicalName = CLSID_EasyPdfConvert;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override
    {
        if (state == nullptr)
        {
            return E_POINTER;
        }

        std::vector<std::wstring> paths;
        if (FAILED(GetSelectedPaths(items, paths)) ||
            !std::ranges::all_of(paths, [](const std::wstring& path) { return IsSupportedDocument(path); }))
        {
            *state = ECS_HIDDEN;
            return S_OK;
        }

        *state = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        std::vector<std::wstring> paths;
        RETURN_IF_FAILED(GetSelectedPaths(items, paths));
        if (!std::ranges::all_of(paths, [](const std::wstring& path) { return IsSupportedDocument(path); }))
        {
            return E_INVALIDARG;
        }

        std::wstring workerPath;
        RETURN_IF_FAILED(GetWorkerPath(workerPath));

        std::wstring commandLine = QuoteArgument(workerPath) + L" convert-to-pdf";
        for (const auto& path : paths)
        {
            commandLine.push_back(L' ');
            commandLine.append(QuoteArgument(path));
        }

        STARTUPINFOW startupInfo{sizeof(startupInfo)};
        PROCESS_INFORMATION processInfo{};
        if (!CreateProcessW(
            workerPath.c_str(), commandLine.data(), nullptr, nullptr, FALSE,
            CREATE_UNICODE_ENVIRONMENT, nullptr, nullptr, &startupInfo, &processInfo))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        CloseHandle(processInfo.hThread);
        CloseHandle(processInfo.hProcess);
        return S_OK;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (flags == nullptr)
        {
            return E_POINTER;
        }
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
    {
        if (commands != nullptr)
        {
            *commands = nullptr;
        }
        return E_NOTIMPL;
    }

private:
    ~ExplorerCommand() { --objectCount; }
    std::atomic<ULONG> referenceCount_{1};
};

class ClassFactory final : public IClassFactory
{
public:
    IFACEMETHODIMP QueryInterface(REFIID interfaceId, void** result) override
    {
        if (result == nullptr)
        {
            return E_POINTER;
        }

        *result = nullptr;
        if (interfaceId == IID_IUnknown || interfaceId == IID_IClassFactory)
        {
            *result = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override { return ++referenceCount_; }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG remaining = --referenceCount_;
        if (remaining == 0)
        {
            delete this;
        }
        return remaining;
    }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID interfaceId, void** result) override
    {
        if (outer != nullptr)
        {
            return CLASS_E_NOAGGREGATION;
        }

        auto* command = new (std::nothrow) ExplorerCommand();
        if (command == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        const HRESULT status = command->QueryInterface(interfaceId, result);
        command->Release();
        return status;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        lock ? ++lockCount : --lockCount;
        return S_OK;
    }

private:
    std::atomic<ULONG> referenceCount_{1};
};
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        moduleHandle = module;
        DisableThreadLibraryCalls(module);
    }
    return TRUE;
}

STDAPI DllCanUnloadNow()
{
    return objectCount == 0 && lockCount == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID classId, REFIID interfaceId, void** result)
{
    if (classId != CLSID_EasyPdfConvert)
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    auto* factory = new (std::nothrow) ClassFactory();
    if (factory == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    const HRESULT status = factory->QueryInterface(interfaceId, result);
    factory->Release();
    return status;
}
