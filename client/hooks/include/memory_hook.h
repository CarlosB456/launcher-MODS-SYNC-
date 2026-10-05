#pragma once
#include <windows.h>
#include <psapi.h>
#include <cstdint>
#include <vector>
#include <cstring>

namespace MemoryHook {
    class MemoryProtectionGuard {
    private:
        void* address;
        size_t size;
        DWORD oldProtect;
    public:
        MemoryProtectionGuard(void* addr, size_t sz) : address(addr), size(sz) {
            VirtualProtect(address, size, PAGE_EXECUTE_READWRITE, &oldProtect);
        }
        ~MemoryProtectionGuard() {
            VirtualProtect(address, size, oldProtect, &oldProtect);
        }
    };

    template <typename T>
    inline void Patch(uintptr_t address, T value) {
        MemoryProtectionGuard guard(reinterpret_cast<void*>(address), sizeof(T));
        *reinterpret_cast<T*>(address) = value;
    }

    inline void PatchBytes(uintptr_t address, const uint8_t* data, size_t size) {
        MemoryProtectionGuard guard(reinterpret_cast<void*>(address), size);
        std::memcpy(reinterpret_cast<void*>(address), data, size);
    }

    inline void Nop(uintptr_t address, size_t count) {
        MemoryProtectionGuard guard(reinterpret_cast<void*>(address), count);
        std::memset(reinterpret_cast<void*>(address), 0x90, count);
    }

    inline void InstallJmp(uintptr_t from, uintptr_t to) {
        MemoryProtectionGuard guard(reinterpret_cast<void*>(from), 5);
        *reinterpret_cast<uint8_t*>(from) = 0xE9; // JMP rel32
        *reinterpret_cast<int32_t*>(from + 1) = static_cast<int32_t>(to - from - 5);
    }

    inline void InstallCall(uintptr_t from, uintptr_t to) {
        MemoryProtectionGuard guard(reinterpret_cast<void*>(from), 5);
        *reinterpret_cast<uint8_t*>(from) = 0xE8; // CALL rel32
        *reinterpret_cast<int32_t*>(from + 1) = static_cast<int32_t>(to - from - 5);
    }

    inline uintptr_t PatternScan(HMODULE module, const char* pattern, const char* mask) {
        if (!module) return 0;

        MODULEINFO moduleInfo{};
        GetModuleInformation(GetCurrentProcess(), module, &moduleInfo, sizeof(MODULEINFO));

        auto base = reinterpret_cast<const uint8_t*>(moduleInfo.lpBaseOfDll);
        size_t size = moduleInfo.SizeOfImage;
        size_t patternLen = std::strlen(mask);

        for (size_t i = 0; i <= size - patternLen; ++i) {
            bool found = true;
            for (size_t j = 0; j < patternLen; ++j) {
                if (mask[j] != '?' && pattern[j] != static_cast<char>(base[i + j])) {
                    found = false;
                    break;
                }
            }
            if (found) {
                return reinterpret_cast<uintptr_t>(base + i);
            }
        }
        return 0;
    }
}
