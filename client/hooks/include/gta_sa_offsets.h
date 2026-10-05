#pragma once
#include <cstdint>

// Memory addresses and structures identified from gta-reversed (GTA San Andreas v1.0 US)
namespace GtaSaOffsets {
    // Array of CBaseModelInfo pointers (original capacity: 20000)
    constexpr uintptr_t ADDR_MODEL_INFO_PTRS = 0x00A9B0C8;
    constexpr size_t ORIGINAL_MODEL_LIMIT = 20000;
    constexpr size_t EXPANDED_MODEL_LIMIT = 65536;

    // CModelInfo registration methods
    constexpr uintptr_t FUNC_ADD_VEHICLE_MODEL = 0x004C5E60;
    constexpr uintptr_t FUNC_ADD_PED_MODEL     = 0x004C5D40;
    constexpr uintptr_t FUNC_ADD_WEAPON_MODEL  = 0x004C5DE0;
    constexpr uintptr_t FUNC_ADD_ATOMIC_MODEL  = 0x004C5C80;
    constexpr uintptr_t FUNC_GET_MODEL_INFO    = 0x00403DA0;

    // CStreaming methods
    constexpr uintptr_t FUNC_REQUEST_MODEL               = 0x004087E0;
    constexpr uintptr_t FUNC_LOAD_ALL_REQUESTED_MODELS   = 0x0040EA10;
    constexpr uintptr_t FUNC_SET_MODEL_IS_DELETABLE      = 0x00409C10;
    constexpr uintptr_t FUNC_HAS_MODEL_LOADED            = 0x00407800;

    // Streaming memory budget, usage, and component pool limits
    constexpr uintptr_t ADDR_STREAMING_MEMORY_BUDGET    = 0x008A5A80; // CStreaming::ms_memoryAvailable (budget limit)
    constexpr uintptr_t ADDR_STREAMING_MEMORY_USED      = 0x008E4CB4; // CStreaming::ms_memoryUsed (actively tracked by game engine)
    constexpr uintptr_t ADDR_STREAMING_DEF_BUDGET_INIT  = 0x005B8E6A; // Imm32 operand in CStreaming::InitFileList for default 50MB budget
    constexpr uintptr_t ADDR_STREAMING_DEF_VEH_INIT     = 0x005B8E74; // Imm32 operand in CStreaming::InitFileList for default 22 vehicles
    constexpr uint32_t  STREAMING_MEMORY_2GB_BUDGET     = 2047u * 1024u * 1024u; // 2047MB (0x7FE00000 = 2146435072 bytes)
    constexpr uint32_t  STREAMING_VEHICLES_POOL_LIMIT   = 96;         // Matches stream.ini vehicles pool
    constexpr uintptr_t ADDR_MATRIX_LINK_LIST_COUNT     = 0x0054F3A1; // Imm32 operand in CPlaceable::InitMatrixArray
    constexpr uint32_t  EXPANDED_MATRIX_LINK_LIMIT      = 8000;       // Increased from vanilla 900 to support extreme HD vehicle component hierarchies

    // Handling Manager
    constexpr uintptr_t ADDR_HANDLING_DATA_MGR = 0x00C2B9C8;
    constexpr uintptr_t FUNC_GET_HANDLING_DATA = 0x006F1100;

    // CheckForDuplicateProcess bypass addresses
    constexpr uintptr_t ADDR_CHECK_FOR_DUPLICATE_PROCESS_10US = 0x00745CE0; // GTA SA 1.0 US
    constexpr uintptr_t ADDR_CHECK_FOR_DUPLICATE_PROCESS      = 0x007468E0; // GTA SA 1.01 US / compact

    // Custom ModSync network packet identification
    constexpr uint8_t PACKET_MODSYNC_BASE         = 240;
    constexpr uint8_t PACKET_MODSYNC_VEHICLE_ADD  = 241;
    constexpr uint8_t PACKET_MODSYNC_WEAPON_ADD   = 242;
    constexpr uint8_t PACKET_MODSYNC_CLEO_TRIGGER = 243;
    constexpr uint8_t PACKET_MODSYNC_HANDLING_SET = 244;
}

// Forward declarations of core RenderWare / GTA classes matching gta-reversed ABI
#pragma pack(push, 1)
struct tHandlingData {
    int32_t   vehicleId;
    float     mass;
    float     turnMass;
    float     dragMult;
    float     centreOfMass[3];
    uint8_t   nPercentSubmerged;
    float     tractionMultiplier;
    float     transmission[18]; // Engine acceleration, max speed, gears
    float     brakeDeceleration;
    float     brakeBias;
    int8_t    steeringLock;
    float     tractionLoss;
    float     tractionBias;
    float     suspension[5];
    float     seatOffsetDistance;
    float     collisionDamageMultiplier;
    uint32_t  modelFlags;
    uint32_t  handlingFlags;
    float     monetaryValue;
    uint8_t   modelLightFlags;
    uint8_t   handlingType;
};
#pragma pack(pop)
