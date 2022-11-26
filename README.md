# FastPcapng
Small, hopefully fast, library with various utilities to handle pcapng file in .NET

Classes that might interest you:
1. `MemoryPcapng` - Store and modify a pcapng file structe in memory (no disk operation)
2. `WiresharkPipeSender` - Provides a quick way to "send" (or "export") a pcapng in memory (e.g. in `MemoryPcapng` or byte array) to wireshark over a named pipe
3. `WeakPcapngFileHandle` - Lazy pcapng file accessor. Avoid all disk operations until they are absolutely required.
