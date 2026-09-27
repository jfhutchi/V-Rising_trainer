using System.Diagnostics;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace HutchVRisingTrainer;

internal sealed class ClientCommandService
{
    internal static ClientCommandService? Active { get; set; }

    private readonly ManualLogSource _log;
    private readonly Queue<string> _pending = new();
    private World? _world;
    private Entity _localUser = Entity.Null;
    private Entity _localCharacter = Entity.Null;
    private bool _localServerRunning;
    private bool _wasReady;
    private DateTime _nextServerCheckUtc;
    private DateTime _nextSendUtc;
    private DateTime _nextCaptureErrorUtc;
    private int _localServerProcessId;
    private int _autoResetSentForServerId;
    private readonly bool _clearBoostsOnJoin;

    internal ClientCommandService(ManualLogSource log, bool clearBoostsOnJoin)
    {
        _log = log;
        _clearBoostsOnJoin = clearBoostsOnJoin;
    }

    internal string Status { get; private set; } = "Enter your private world to use live controls.";
    internal bool Ready => _localServerRunning && _world is not null &&
        _localUser != Entity.Null && _localCharacter != Entity.Null;

    internal void SetStatus(string status) => Status = status;

    internal void Tick()
    {
        if (DateTime.UtcNow >= _nextServerCheckUtc)
        {
            var processes = Process.GetProcessesByName("VRisingServer");
            _localServerRunning = processes.Length > 0;
            _localServerProcessId = processes.Length == 1 ? processes[0].Id : 0;
            foreach (var process in processes) process.Dispose();
            _nextServerCheckUtc = DateTime.UtcNow.AddSeconds(2);
        }

        var ready = Ready;
        if (ready != _wasReady)
        {
            Status = ready ? "Live command link ready. Adminauth in the game console, then use a button." :
                "Live command link unavailable; load your private local world.";
            if (ready && _clearBoostsOnJoin && _localServerProcessId != 0 &&
                _localServerProcessId != _autoResetSentForServerId)
            {
                _autoResetSentForServerId = _localServerProcessId;
                QueueCommand(".mortal");
                _log.LogInfo("Automatic character boost reset queued on local world join.");
            }
            _wasReady = ready;
        }

        if (_pending.Count == 0 || DateTime.UtcNow < _nextSendUtc) return;
        if (!Ready)
        {
            _pending.Clear();
            Status = "Live controls need a loaded private world and local server.";
            return;
        }

        var command = _pending.Dequeue();
        try
        {
            SendChatCommand(command);
            _log.LogInfo("Live trainer command sent: " + command);
            Status = $"Sent {command} to the local server. Check its chat reply.";
            _nextSendUtc = DateTime.UtcNow.AddMilliseconds(300);
        }
        catch (Exception error)
        {
            Status = "Could not send the command; see the BepInEx log.";
            _log.LogError("Live trainer command failed: " + error);
            _pending.Clear();
        }
    }

    internal void QueueCommand(string command)
    {
        if (!Ready)
        {
            Status = "Enter a private local world and wait for its server to load.";
            return;
        }
        if (string.IsNullOrWhiteSpace(command) || !command.StartsWith('.') ||
            command.Length > 200 || command.Contains('\n') || command.Contains('\r'))
        {
            Status = "Invalid trainer command.";
            return;
        }
        _pending.Enqueue(command);
        Status = $"Queued {command}";
    }

    internal void Capture(CommonClientDataSystem system)
    {
        try
        {
            _world = system.World;
            _localUser = FirstEntity(system.__query_1840110770_0);
            _localCharacter = FirstEntity(system.__query_1840110770_1);
        }
        catch (Exception error)
        {
            _world = null;
            _localUser = Entity.Null;
            _localCharacter = Entity.Null;
            if (DateTime.UtcNow < _nextCaptureErrorUtc) return;
            _nextCaptureErrorUtc = DateTime.UtcNow.AddSeconds(10);
            _log.LogWarning("Could not find the local character for live controls: " + error);
        }
    }

    private static Entity FirstEntity(EntityQuery query)
    {
        var entities = query.ToEntityArray(Allocator.Temp);
        try { return entities.Length > 0 ? entities[0] : Entity.Null; }
        finally { entities.Dispose(); }
    }

    private unsafe void SendChatCommand(string command)
    {
        var manager = _world!.EntityManager;
        var receiver = ReadComponent<NetworkId>(manager, _localUser);
        var chat = new ChatMessageEvent
        {
            MessageText = command,
            MessageType = ChatMessageType.Local,
            ReceiverEntity = receiver
        };
        var networkEvent = new NetworkEventType
        {
            IsAdminEvent = false,
            EventId = NetworkEvents.EventId_ChatMessageEvent,
            IsDebugEvent = false
        };
        var entity = manager.CreateEntity(
        [
            ComponentType.ReadOnly(Il2CppType.Of<FromCharacter>()),
            ComponentType.ReadOnly(Il2CppType.Of<NetworkEventType>()),
            ComponentType.ReadOnly(Il2CppType.Of<SendNetworkEventTag>()),
            ComponentType.ReadOnly(Il2CppType.Of<ChatMessageEvent>())
        ]);
        try
        {
            WriteComponent(manager, entity, new FromCharacter
            {
                Character = _localCharacter,
                User = _localUser
            });
            WriteComponent(manager, entity, networkEvent);
            WriteComponent(manager, entity, chat);
        }
        catch
        {
            manager.DestroyEntity(entity);
            throw;
        }
    }

    private static unsafe T ReadComponent<T>(EntityManager manager, Entity entity) where T : struct
    {
        var typeIndex = new ComponentType(Il2CppType.Of<T>()).TypeIndex;
        var pointer = manager.GetComponentDataRawRO(entity, typeIndex);
        return Marshal.PtrToStructure<T>(new IntPtr(pointer));
    }

    private static unsafe void WriteComponent<T>(EntityManager manager, Entity entity, T value) where T : struct
    {
        var typeIndex = new ComponentType(Il2CppType.Of<T>()).TypeIndex;
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        try
        {
            Marshal.StructureToPtr(value, pointer, false);
            manager.SetComponentDataRaw(entity, typeIndex, pointer.ToPointer(), Marshal.SizeOf<T>());
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }
}

[HarmonyPatch(typeof(CommonClientDataSystem), "OnUpdate")]
internal static class LocalCharacterCapturePatch
{
    [HarmonyPostfix]
    private static void Postfix(CommonClientDataSystem __instance) =>
        ClientCommandService.Active?.Capture(__instance);
}
