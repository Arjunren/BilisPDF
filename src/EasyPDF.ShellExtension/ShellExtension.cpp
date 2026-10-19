#include <windows.h>
#include <shobjidl_core.h>
#include <shellapi.h>
#include <wrl.h>

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

enum class Action { Root, Convert, Merge, Extract, Rotate, Images, Open };

enum class FileKind { Unknown, Document, Pdf, Image };

FileKind GetFileKind(const std::wstring& path)
{
    const auto separator = path.find_last_of(L'.');
    if (separator == std::wstring::npos)
    {
        return FileKind::Unknown;
    }

    const auto extension = ToLower(path.substr(separator));
    if (extension == L".pdf") { return FileKind::Pdf; }
    if (extension == L".docx" || extension == L".xlsx" || extension == L".pptx" ||
        extension == L".txt" || extension == L".rtf" || extension == L".html" || extension == L".htm") { return FileKind::Document; }
    if (extension == L".png" || extension == L".jpg" || extension == L".jpeg" || extension == L".bmp" ||
        extension == L".tif" || extension == L".tiff" || extension == L".webp") { return FileKind::Image; }
    return FileKind::Unknown;
}

bool SupportsAction(Action action, const std::vector<std::wstring>& paths)
{
    if (paths.empty() || paths.size() > 100) { return false; }
    const auto allOfKind = [&](FileKind kind)
    {
        return std::ranges::all_of(paths, [kind](const auto& path) { return GetFileKind(path) == kind; });
    };
    switch (action)
    {
    case Action::Convert: return allOfKind(FileKind::Document);
    case Action::Merge: return paths.size() > 1 && allOfKind(FileKind::Pdf);
    case Action::Extract:
    case Action::Rotate: return paths.size() == 1 && allOfKind(FileKind::Pdf);
    case Action::Images: return allOfKind(FileKind::Image);
    default: return std::ranges::all_of(paths, [](const auto& path) { return GetFileKind(path) != FileKind::Unknown; });
    }
}

const wchar_t* ActionArgument(Action action)
{
    switch (action)
    {
    case Action::Convert: return L"convert-to-pdf";
    case Action::Merge: return L"merge";
    case Action::Extract: return L"extract-pages";
    case Action::Rotate: return L"rotate";
    case Action::Images: return L"images-to-pdf";
    default: return L"open";
    }
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
        SFGAOF attributes = SFGAO_FOLDER;
        const HRESULT attributeResult = item->GetAttributes(SFGAO_FOLDER, &attributes);
        if (FAILED(attributeResult) || (attributes & SFGAO_FOLDER) != 0)
        {
            item->Release();
            return E_INVALIDARG;
        }
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

HRESULT GetSiblingPath(const wchar_t* filename, std::wstring& workerPath)
{
    std::vector<wchar_t> modulePath(32768);
    const DWORD length = GetModuleFileNameW(moduleHandle, modulePath.data(), static_cast<DWORD>(modulePath.size()));
    if (length == 0 || length >= modulePath.size())
    {
        return HRESULT_FROM_WIN32(length == 0 ? GetLastError() : ERROR_INSUFFICIENT_BUFFER);
    }

    workerPath.assign(modulePath.data(), length);
    const auto separator = workerPath.find_last_of(L"\\/");
    if (separator == std::wstring::npos)
    {
        return E_UNEXPECTED;
    }

    workerPath.resize(separator + 1);
    workerPath.append(filename);

    return S_OK;
}

class ExplorerCommand final : public IExplorerCommand
{
public:
    explicit ExplorerCommand(Action action = Action::Root) : action_(action) { ++objectCount; }

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
        const wchar_t* label = L"EasyPDF";
        switch (action_)
        {
        case Action::Convert: label = L"Convert to PDF"; break;
        case Action::Merge: label = L"Merge PDFs..."; break;
        case Action::Extract: label = L"Extract pages..."; break;
        case Action::Rotate: label = L"Rotate pages..."; break;
        case Action::Images: label = L"Combine images to PDF..."; break;
        case Action::Open: label = L"Open in EasyPDF"; break;
        default: break;
        }
        return DuplicateString(label, title);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
    {
        std::wstring path;
        RETURN_IF_FAILED(GetSiblingPath(L"EasyPDF.ico", path));
        return DuplicateString(path.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tooltip) override
    {
        return DuplicateString(L"Private, offline PDF tools. Originals are kept unchanged.", tooltip);
    }

    IFACEMETHODIMP GetCanonicalName(GUID* canonicalName) override
    {
        if (canonicalName == nullptr)
        {
            return E_POINTER;
        }
        *canonicalName = CLSID_EasyPdfConvert;
        canonicalName->Data1 += static_cast<unsigned long>(action_);
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override
    {
        if (state == nullptr)
        {
            return E_POINTER;
        }

        std::vector<std::wstring> paths;
        if (FAILED(GetSelectedPaths(items, paths)) || !SupportsAction(action_, paths))
        {
            *state = ECS_HIDDEN;
            return S_OK;
        }

        *state = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        if (action_ == Action::Root) { return E_NOTIMPL; }
        std::vector<std::wstring> paths;
        RETURN_IF_FAILED(GetSelectedPaths(items, paths));
        if (!SupportsAction(action_, paths))
        {
            return E_INVALIDARG;
        }

        std::wstring workerPath;
        RETURN_IF_FAILED(GetSiblingPath(L"EasyPDF.Shell.exe", workerPath));

        std::wstring commandLine = QuoteArgument(workerPath) + L" " + ActionArgument(action_);
        for (const auto& path : paths)
        {
            commandLine.push_back(L' ');
            commandLine.append(QuoteArgument(path));
        }

        if (commandLine.size() >= 32767) { return HRESULT_FROM_WIN32(ERROR_FILENAME_EXCED_RANGE); }

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
        *flags = action_ == Action::Root ? ECF_HASSUBCOMMANDS : ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override;

private:
    ~ExplorerCommand() { --objectCount; }
    std::atomic<ULONG> referenceCount_{1};
    Action action_;
};

class CommandEnumerator final : public Microsoft::WRL::RuntimeClass<Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>, IEnumExplorerCommand>
{
public:
    CommandEnumerator() { ++objectCount; }
    ~CommandEnumerator() { --objectCount; }

    IFACEMETHODIMP Next(ULONG requested, IExplorerCommand** commands, ULONG* fetched) override
    {
        if (commands == nullptr || (fetched == nullptr && requested != 1)) { return E_POINTER; }
        if (fetched != nullptr) { *fetched = 0; }
        ULONG copied = 0;
        while (copied < requested && position_ < 6)
        {
            commands[copied] = new (std::nothrow) ExplorerCommand(static_cast<Action>(position_ + 1));
            if (commands[copied] == nullptr)
            {
                if (fetched != nullptr) { *fetched = copied; }
                return E_OUTOFMEMORY;
            }
            ++copied;
            ++position_;
        }
        if (fetched != nullptr) { *fetched = copied; }
        return copied == requested ? S_OK : S_FALSE;
    }

    IFACEMETHODIMP Skip(ULONG count) override
    {
        const auto skipped = std::min(count, 6UL - position_);
        position_ += skipped;
        return skipped == count ? S_OK : S_FALSE;
    }

    IFACEMETHODIMP Reset() override { position_ = 0; return S_OK; }

    IFACEMETHODIMP Clone(IEnumExplorerCommand** clone) override
    {
        if (clone == nullptr) { return E_POINTER; }
        *clone = nullptr;
        auto copy = Microsoft::WRL::Make<CommandEnumerator>();
        if (!copy) { return E_OUTOFMEMORY; }
        copy->position_ = position_;
        return copy.CopyTo(clone);
    }

private:
    ULONG position_ = 0;
};

HRESULT ExplorerCommand::EnumSubCommands(IEnumExplorerCommand** commands)
{
    if (commands == nullptr) { return E_POINTER; }
    *commands = nullptr;
    if (action_ != Action::Root) { return E_NOTIMPL; }
    const auto enumerator = Microsoft::WRL::Make<CommandEnumerator>();
    return enumerator ? enumerator.CopyTo(commands) : E_OUTOFMEMORY;
}

class ClassFactory final : public IClassFactory
{
public:
    ClassFactory() { ++objectCount; }
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
    ~ClassFactory() { --objectCount; }
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
