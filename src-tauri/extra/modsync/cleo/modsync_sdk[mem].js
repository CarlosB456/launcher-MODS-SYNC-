/// <reference path=".config/sa.d.ts" />
/**
 * ModSync SDK for CLEO-Redux
 * Exposes globalThis.ModSync for inter-script synchronization.
 */

class ModSyncBridge {
    constructor() {
        this.listeners = new Map();
        this.pollFunc = null;
        this.nameBuffer = null;
        this.payloadBuffer = null;
        this.senderIdBuffer = null;
        this.init();
    }

    init() {
        try {
            if (typeof Memory !== 'undefined' && Memory.Alloc) {
                this.nameBuffer = Memory.Alloc(64);
                this.payloadBuffer = Memory.Alloc(4096);
                this.senderIdBuffer = Memory.Alloc(4);

                const hMod = Memory.GetModuleHandle("modsync_hook.asi");
                if (hMod) {
                    this.pollFunc = Memory.GetProcAddress(hMod, "ModSync_PollEvent");
                }
            }
        } catch (e) {}
    }

    on(eventName, callback) {
        if (!this.listeners.has(eventName)) {
            this.listeners.set(eventName, []);
        }
        this.listeners.get(eventName).push(callback);
    }

    dispatchEvent(eventName, payloadJson, senderId) {
        let parsed = {};
        try {
            parsed = JSON.parse(payloadJson);
        } catch (e) {
            parsed = payloadJson;
        }

        const handlers = this.listeners.get(eventName);
        if (handlers) {
            for (const handler of handlers) {
                try {
                    handler(parsed, senderId);
                } catch (err) {}
            }
        }
    }

    pollEvents() {
        if (typeof Memory === 'undefined' || !this.pollFunc) return;

        for (let i = 0; i < 10; i++) {
            const hasEvent = Memory.CallFunction(
                this.pollFunc,
                this.nameBuffer,
                64,
                this.payloadBuffer,
                4096,
                this.senderIdBuffer
            );

            if (hasEvent === 1) {
                const eventName = Memory.ReadString(this.nameBuffer);
                const payload = Memory.ReadString(this.payloadBuffer);
                const senderId = Memory.ReadU32(this.senderIdBuffer);
                this.dispatchEvent(eventName, payload, senderId);
            } else {
                break;
            }
        }
    }
}

globalThis.ModSync = new ModSyncBridge();

// Clean CLEO Redux coroutine loop (prevents script evaluation spam in cleo_redux.log)
while (true) {
    wait(40);
    globalThis.ModSync.pollEvents();
}
