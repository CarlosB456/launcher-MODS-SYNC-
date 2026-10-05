#include <windows.h>
#include <thread>
#include <chrono>
#include "../include/samp_hook.h"
#include "../include/gta_sa_offsets.h"
#include "../include/memory_hook.h"

void ApplyStreamingAndPoolFixes() {
    // 1. Expand CMatrixLinkList pool from vanilla 900 to 8000
    // Address 0x0054F3A1 is the push operand in CPlaceable::InitMatrixArray
    // This provides dedicated matrix memory for all vehicle doors, hoods, boots, steering, and components.
    MemoryHook::Patch<uint32_t>(GtaSaOffsets::ADDR_MATRIX_LINK_LIST_COUNT, GtaSaOffsets::EXPANDED_MATRIX_LINK_LIMIT);

    // 2. Patch default streaming budget and vehicle pool at CStreaming::InitFileList
    MemoryHook::Patch<uint32_t>(GtaSaOffsets::ADDR_STREAMING_DEF_BUDGET_INIT, GtaSaOffsets::STREAMING_MEMORY_2GB_BUDGET);
    MemoryHook::Patch<uint32_t>(GtaSaOffsets::ADDR_STREAMING_DEF_VEH_INIT, GtaSaOffsets::STREAMING_VEHICLES_POOL_LIMIT);

    // 3. Patch signed comparisons in CStreaming to unsigned comparisons (jb/jae)
    // Prevents negative integer overflow when memory is set to 2GB or above
    MemoryHook::Patch<uint8_t>(0x004099C0, 0x72); // jl -> jb
    MemoryHook::Patch<uint8_t>(0x00409A69, 0x72); // jl -> jb
    MemoryHook::Patch<uint8_t>(0x0040D2C8, 0x72); // jl -> jb
    MemoryHook::Patch<uint8_t>(0x0040D3A7, 0x72); // jl -> jb
    MemoryHook::Patch<uint8_t>(0x0040E0FF, 0x72); // jl -> jb
    MemoryHook::Patch<uint8_t>(0x0040E115, 0x73); // jge -> jae

    // 4. Set CStreaming::ms_memoryAvailable (budget limit) to 2047 MB (0x7FE00000 = 2146435072 bytes)
    MemoryHook::Patch<uint32_t>(GtaSaOffsets::ADDR_STREAMING_MEMORY_BUDGET, GtaSaOffsets::STREAMING_MEMORY_2GB_BUDGET);

    // 5. CRITICAL FIX: Heal CStreaming::ms_memoryUsed (0x008E4CB4)
    // If a script or previous hook erroneously wrote 2GB to ms_memoryUsed, reset it to 0
    // so the streaming engine does not believe memory is exhausted.
    uint32_t currentUsed = *reinterpret_cast<const volatile uint32_t*>(GtaSaOffsets::ADDR_STREAMING_MEMORY_USED);
    if (currentUsed >= GtaSaOffsets::STREAMING_MEMORY_2GB_BUDGET) {
        MemoryHook::Patch<uint32_t>(GtaSaOffsets::ADDR_STREAMING_MEMORY_USED, 0);
    }

    // 6. CRITICAL FIX: Bypass CheckForDuplicateProcess (0x007468E0)
    // Prevents crash 0x00746929 when a lingering zombie gta_sa process exists.
    // Overwrite start of CheckForDuplicateProcess with: xor eax, eax; ret; nop; nop (31 C0 C3 90 90)
    const uint8_t bypassDupCheck[] = { 0x31, 0xC0, 0xC3, 0x90, 0x90 };
    MemoryHook::PatchBytes(GtaSaOffsets::ADDR_CHECK_FOR_DUPLICATE_PROCESS, bypassDupCheck, sizeof(bypassDupCheck));
}

DWORD WINAPI InitializationThread(LPVOID) {
    // Re-verify streaming memory and pool patches after thread starts
    ApplyStreamingAndPoolFixes();

    // Wait until samp.dll is mapped into GTA SA process memory
    while (!GetModuleHandleA("samp.dll")) {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }

    // Allow samp.dll to finish its imports and base relocations
    std::this_thread::sleep_for(std::chrono::milliseconds(500));

    // Install dynamic model extender and SA-MP network packet interceptor
    SampHook::Initialize();

    return 0;
}

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved) {
    if (fdwReason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hinstDLL);
        // Synchronously patch streaming budget and CMatrixLinkList BEFORE CGame::Initialise runs
        ApplyStreamingAndPoolFixes();
        CreateThread(nullptr, 0, InitializationThread, nullptr, 0, nullptr);
    }
    return TRUE;
}
