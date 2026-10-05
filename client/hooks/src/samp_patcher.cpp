#include "../include/samp_hook.h"
#include "../include/gta_sa_offsets.h"
#include "../include/memory_hook.h"
#include "../include/ipc_bridge.h"
#include <cstdio>

namespace {
    HMODULE g_SampModule = nullptr;

    void PatchVehicleModelBoundsCheck() {
        if (!g_SampModule) return;

        // Signature pattern for CVehiclePool::New vehicle ID check in samp.dll:
        // 3D 63 02 00 00 (cmp eax, 611) followed by 7F (jg) or 0F 8F (jg rel32)
        const char* pattern = "\x3D\x63\x02\x00\x00";
        const char* mask    = "xxxxx";

        uintptr_t match = MemoryHook::PatternScan(g_SampModule, pattern, mask);
        if (match) {
            // Replace the upper limit check with EXPANDED_MODEL_LIMIT (65535)
            // cmp eax, 0xFFFF
            uint32_t newLimit = 0xFFFF;
            MemoryHook::Patch<uint32_t>(match + 1, newLimit);
        }
    }
}

namespace SampHook {
    bool Initialize() {
        g_SampModule = GetModuleHandleA("samp.dll");
        if (!g_SampModule) {
            // If samp.dll is not loaded yet, wait or retry
            return false;
        }

        PatchVehicleModelBoundsCheck();
        ModSyncIPC::GetBridge().Start();
        return true;
    }

    bool SetVehicleCustomHandling(int32_t vehicleId, float mass, float maxSpeed, float acceleration) {
        // Direct pointer to cHandlingDataMgr table in GTA SA
        auto handlingTable = *reinterpret_cast<tHandlingData**>(GtaSaOffsets::ADDR_HANDLING_DATA_MGR);
        if (!handlingTable) return false;

        // Target index corresponds to vehicle handling index
        tHandlingData* entry = &handlingTable[vehicleId % 212];
        if (entry) {
            entry->mass = mass;
            entry->transmission[3] = acceleration; // Engine acceleration
            entry->transmission[4] = maxSpeed;     // Max velocity
            return true;
        }
        return false;
    }

    void HandleModSyncPacket(uint8_t packetId, const uint8_t* data, size_t length) {
        if (length == 0 || !data) return;

        switch (packetId) {
            case GtaSaOffsets::PACKET_MODSYNC_VEHICLE_ADD: {
                if (length < sizeof(CustomVehicleDef)) return;
                const auto* def = reinterpret_cast<const CustomVehicleDef*>(data);
                RegisterCustomVehicle(*def);
                break;
            }

            case GtaSaOffsets::PACKET_MODSYNC_WEAPON_ADD: {
                if (length < sizeof(CustomWeaponDef)) return;
                const auto* def = reinterpret_cast<const CustomWeaponDef*>(data);
                RegisterCustomWeapon(*def);
                break;
            }

            case GtaSaOffsets::PACKET_MODSYNC_CLEO_TRIGGER: {
                // Format: [uint32_t playerId][string eventName\0][string payloadJson\0]
                if (length < 8) return;
                uint32_t playerId = *reinterpret_cast<const uint32_t*>(data);
                const char* eventName = reinterpret_cast<const char*>(data + 4);
                size_t nameLen = strnlen(eventName, 64);
                
                if (4 + nameLen + 1 < length) {
                    const char* jsonPayload = eventName + nameLen + 1;
                    ModSyncIPC::GetBridge().BroadcastEventToCleo(eventName, jsonPayload, playerId);
                }
                break;
            }

            case GtaSaOffsets::PACKET_MODSYNC_HANDLING_SET: {
                // Handling packet structure: [int32 vehicleId][float mass][float speed][float accel]
                if (length < 16) return;
                int32_t vehicleId = *reinterpret_cast<const int32_t*>(data);
                float mass = *reinterpret_cast<const float*>(data + 4);
                float speed = *reinterpret_cast<const float*>(data + 8);
                float accel = *reinterpret_cast<const float*>(data + 12);
                SetVehicleCustomHandling(vehicleId, mass, speed, accel);
                break;
            }

            default:
                break;
        }
    }
}
