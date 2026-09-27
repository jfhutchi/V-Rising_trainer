using BepInEx.Configuration;
using BepInEx.Logging;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace HutchVRisingTrainer.UI;

public sealed class TrainerBehaviour(IntPtr pointer) : MonoBehaviour(pointer)
{
    private TrainerWindow? _window;
    private AchievementChecklist? _checklist;
    private ClientCommandService? _commands;
    private LocalWorldSettingsService? _worldSettings;
    private ConfigEntry<KeyCode>? _menuKey;
    private ConfigEntry<KeyCode>? _instantConvertKey;
    private ManualLogSource? _log;
    private bool _visible;
    private bool _previousCursorVisible;
    private CursorLockMode _previousCursorLock;

    [HideFromIl2Cpp]
    internal void Initialize(TrainerWindow window, AchievementChecklist checklist, ClientCommandService commands,
        LocalWorldSettingsService worldSettings,
        ConfigEntry<KeyCode> menuKey, ConfigEntry<KeyCode> instantConvertKey,
        ManualLogSource log)
    {
        _window = window;
        _checklist = checklist;
        _commands = commands;
        _worldSettings = worldSettings;
        _menuKey = menuKey;
        _instantConvertKey = instantConvertKey;
        _log = log;
        window.SetCloseAction(() =>
        {
            log.LogInfo("Trainer close button clicked.");
            SetVisible(false);
        });
    }

    public void Update()
    {
        if (_menuKey is not null && Input.GetKeyDown(_menuKey.Value)) SetVisible(!_visible);
        if (_visible && Input.GetKeyDown(KeyCode.Escape)) SetVisible(false);
        if (!_visible && _instantConvertKey is not null &&
            _instantConvertKey.Value != _menuKey?.Value && Input.GetKeyDown(_instantConvertKey.Value))
            _commands?.QueueCommand(".servant convert");
        _checklist?.Tick(_visible);
        _commands?.Tick();
        _worldSettings?.Tick(_visible);
    }

    public void LateUpdate()
    {
        if (!_visible) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OnGUI()
    {
        if (!_visible || _window is null) return;
        try
        {
            _window.Draw();
        }
        catch (Exception error)
        {
            _log?.LogError("Trainer UI failed and was closed: " + error);
            SetVisible(false);
        }
    }

    public void OnDisable() => SetVisible(false);

    public void OnDestroy()
    {
        SetVisible(false);
        _window?.Dispose();
        _checklist?.Dispose();
    }

    [HideFromIl2Cpp]
    private void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        if (visible)
        {
            _previousCursorVisible = Cursor.visible;
            _previousCursorLock = Cursor.lockState;
            _checklist?.RequestRefresh();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }
    }
}
