#pragma once
#include <windows.h>
#include <string>
#include <functional>
#include <thread>
#include <atomic>
#include <vector>
#include <queue>
#include <mutex>

namespace ModSyncIPC {
    constexpr const char* PIPE_NAME = "\\\\.\\pipe\\OpenMpModSyncBridge";
    constexpr size_t BUFFER_SIZE = 8192;

    struct ScriptEventMessage {
        char eventName[64];
        char payloadJson[4096];
        uint32_t senderPlayerId;
        uint32_t timestamp;
    };

    class IpcServer {
    private:
        HANDLE pipeHandle = INVALID_HANDLE_VALUE;
        std::atomic<bool> isRunning{false};
        std::thread workerThread;
        std::function<void(const ScriptEventMessage&)> onScriptMessageReceived;
        
        std::mutex queueMutex;
        std::queue<ScriptEventMessage> eventQueue;

    public:
        IpcServer() = default;
        ~IpcServer() {
            Stop();
        }

        void SetMessageCallback(std::function<void(const ScriptEventMessage&)> callback) {
            onScriptMessageReceived = callback;
        }

        bool Start();
        void Stop();
        bool BroadcastEventToCleo(const char* eventName, const char* jsonPayload, uint32_t playerId);
        bool PopNextEvent(char* outName, int maxNameLen, char* outPayload, int maxPayloadLen, uint32_t* outSenderId);

    private:
        void WorkerLoop();
    };

    IpcServer& GetBridge();
}

extern "C" __declspec(dllexport) int __cdecl ModSync_PollEvent(
    char* outEventName,
    int maxEventLen,
    char* outPayload,
    int maxPayloadLen,
    uint32_t* outSenderId
);
