#include "../include/ipc_bridge.h"
#include <iostream>

namespace ModSyncIPC {
    IpcServer g_BridgeInstance;

    IpcServer& GetBridge() {
        return g_BridgeInstance;
    }

    bool IpcServer::Start() {
        if (isRunning) return true;
        isRunning = true;
        workerThread = std::thread(&IpcServer::WorkerLoop, this);
        return true;
    }

    void IpcServer::Stop() {
        if (!isRunning) return;
        isRunning = false;
        if (pipeHandle != INVALID_HANDLE_VALUE) {
            CloseHandle(pipeHandle);
            pipeHandle = INVALID_HANDLE_VALUE;
        }
        if (workerThread.joinable()) {
            workerThread.join();
        }
    }

    void IpcServer::WorkerLoop() {
        while (isRunning) {
            pipeHandle = CreateNamedPipeA(
                PIPE_NAME,
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                PIPE_UNLIMITED_INSTANCES,
                BUFFER_SIZE,
                BUFFER_SIZE,
                0,
                nullptr
            );

            if (pipeHandle == INVALID_HANDLE_VALUE) {
                std::this_thread::sleep_for(std::chrono::milliseconds(500));
                continue;
            }

            BOOL connected = ConnectNamedPipe(pipeHandle, nullptr) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
            if (connected) {
                char buffer[BUFFER_SIZE];
                DWORD bytesRead = 0;

                while (isRunning && ReadFile(pipeHandle, buffer, sizeof(buffer), &bytesRead, nullptr) && bytesRead > 0) {
                    if (bytesRead >= sizeof(ScriptEventMessage) && onScriptMessageReceived) {
                        const auto* msg = reinterpret_cast<const ScriptEventMessage*>(buffer);
                        onScriptMessageReceived(*msg);
                    }
                }
            }

            DisconnectNamedPipe(pipeHandle);
            CloseHandle(pipeHandle);
            pipeHandle = INVALID_HANDLE_VALUE;
        }
    }

    bool IpcServer::BroadcastEventToCleo(const char* eventName, const char* jsonPayload, uint32_t playerId) {
        ScriptEventMessage msg{};
        strncpy_s(msg.eventName, eventName, sizeof(msg.eventName) - 1);
        strncpy_s(msg.payloadJson, jsonPayload, sizeof(msg.payloadJson) - 1);
        msg.senderPlayerId = playerId;
        msg.timestamp = GetTickCount();

        // Push to local in-memory queue for direct CLEO polling
        {
            std::lock_guard<std::mutex> lock(queueMutex);
            if (eventQueue.size() > 500) {
                eventQueue.pop(); // Evict oldest if clogged
            }
            eventQueue.push(msg);
        }

        // Also stream to named pipe if connected
        if (pipeHandle != INVALID_HANDLE_VALUE && isRunning) {
            DWORD written = 0;
            WriteFile(pipeHandle, &msg, sizeof(msg), &written, nullptr);
        }

        return true;
    }

    bool IpcServer::PopNextEvent(char* outName, int maxNameLen, char* outPayload, int maxPayloadLen, uint32_t* outSenderId) {
        std::lock_guard<std::mutex> lock(queueMutex);
        if (eventQueue.empty()) {
            return false;
        }

        const auto& front = eventQueue.front();
        if (outName && maxNameLen > 0) {
            strncpy_s(outName, maxNameLen, front.eventName, _TRUNCATE);
        }
        if (outPayload && maxPayloadLen > 0) {
            strncpy_s(outPayload, maxPayloadLen, front.payloadJson, _TRUNCATE);
        }
        if (outSenderId) {
            *outSenderId = front.senderPlayerId;
        }

        eventQueue.pop();
        return true;
    }
}

extern "C" __declspec(dllexport) int __cdecl ModSync_PollEvent(
    char* outEventName,
    int maxEventLen,
    char* outPayload,
    int maxPayloadLen,
    uint32_t* outSenderId
) {
    bool ok = ModSyncIPC::GetBridge().PopNextEvent(outEventName, maxEventLen, outPayload, maxPayloadLen, outSenderId);
    return ok ? 1 : 0;
}
