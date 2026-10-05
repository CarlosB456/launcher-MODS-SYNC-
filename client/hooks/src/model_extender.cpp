#include "../include/gta_sa_offsets.h"
#include "../include/memory_hook.h"
#include "../include/samp_hook.h"
#include <vector>
#include <cstdio>

namespace {
    // Dynamically allocated expanded array for CBaseModelInfo pointers
    void** g_ExpandedModelInfoTable = nullptr;
    bool g_ExtenderInitialized = false;

    // Typedef matching gta-reversed CModelInfo::AddVehicleModel(int id)
    using tAddVehicleModel = void* (__cdecl*)(int32_t modelId);
    using tAddWeaponModel  = void* (__cdecl*)(int32_t modelId);
    using tRequestModel    = void  (__cdecl*)(int32_t modelId, int32_t flags);
    using tLoadAllModels   = void  (__cdecl*)();
}

bool InitializeModelExtender() {
    if (g_ExtenderInitialized) return true;

    // Allocate 65536 pointers with read/write permissions
    g_ExpandedModelInfoTable = new (std::nothrow) void*[GtaSaOffsets::EXPANDED_MODEL_LIMIT];
    if (!g_ExpandedModelInfoTable) return false;

    std::memset(g_ExpandedModelInfoTable, 0, sizeof(void*) * GtaSaOffsets::EXPANDED_MODEL_LIMIT);

    // Copy existing pointers from original CModelInfo::ms_modelInfoPtrs (0x00A9B0C8)
    void** originalTable = reinterpret_cast<void**>(GtaSaOffsets::ADDR_MODEL_INFO_PTRS);
    std::memcpy(g_ExpandedModelInfoTable, originalTable, sizeof(void*) * GtaSaOffsets::ORIGINAL_MODEL_LIMIT);

    // Patch references in gta_sa.exe pointing to the old table
    // In GTA SA 1.0 US, key instructions dereferencing ms_modelInfoPtrs:
    uintptr_t patchLocations[] = {
        0x00403DA0 + 3, // CModelInfo::GetModelInfo
        0x004C5C80 + 3, // CModelInfo::AddAtomicModel
        0x004C5D40 + 3, // CModelInfo::AddPedModel
        0x004C5DE0 + 3, // CModelInfo::AddWeaponModel
        0x004C5E60 + 3, // CModelInfo::AddVehicleModel
        0x004087E0 + 3  // CStreaming::RequestModel
    };

    uintptr_t newTableAddr = reinterpret_cast<uintptr_t>(g_ExpandedModelInfoTable);
    for (uintptr_t loc : patchLocations) {
        MemoryHook::Patch<uintptr_t>(loc, newTableAddr);
    }

    g_ExtenderInitialized = true;
    return true;
}

namespace SampHook {
    bool RegisterCustomVehicle(const CustomVehicleDef& def) {
        if (!InitializeModelExtender()) return false;
        if (def.modelId < 0 || def.modelId >= static_cast<int32_t>(GtaSaOffsets::EXPANDED_MODEL_LIMIT)) {
            return false;
        }

        // Call reversed CModelInfo::AddVehicleModel(modelId)
        auto addVehicle = reinterpret_cast<tAddVehicleModel>(GtaSaOffsets::FUNC_ADD_VEHICLE_MODEL);
        void* vehicleInfo = addVehicle(def.modelId);
        if (!vehicleInfo) return false;

        // In gta-reversed, CVehicleModelInfo layout has:
        // +0x34: base vehicle type
        // +0x40: handling ID index
        auto basePtr = reinterpret_cast<uint8_t*>(vehicleInfo);
        *reinterpret_cast<int32_t*>(basePtr + 0x34) = def.baseVehicleType;
        *reinterpret_cast<uint8_t*>(basePtr + 0x40) = static_cast<uint8_t>(def.baseVehicleType - 400);

        // Immediately request streaming
        auto requestModel = reinterpret_cast<tRequestModel>(GtaSaOffsets::FUNC_REQUEST_MODEL);
        requestModel(def.modelId, 0x16); // Priority stream flags

        return true;
    }

    bool RegisterCustomWeapon(const CustomWeaponDef& def) {
        if (!InitializeModelExtender()) return false;
        if (def.modelId < 0 || def.modelId >= static_cast<int32_t>(GtaSaOffsets::EXPANDED_MODEL_LIMIT)) {
            return false;
        }

        auto addWeapon = reinterpret_cast<tAddWeaponModel>(GtaSaOffsets::FUNC_ADD_WEAPON_MODEL);
        void* weaponInfo = addWeapon(def.modelId);
        if (!weaponInfo) return false;

        // In gta-reversed, CWeaponModelInfo +0x34 is weaponType slot
        auto basePtr = reinterpret_cast<uint8_t*>(weaponInfo);
        *reinterpret_cast<int32_t*>(basePtr + 0x34) = def.weaponSlot;

        auto requestModel = reinterpret_cast<tRequestModel>(GtaSaOffsets::FUNC_REQUEST_MODEL);
        requestModel(def.modelId, 0x16);

        return true;
    }
}
