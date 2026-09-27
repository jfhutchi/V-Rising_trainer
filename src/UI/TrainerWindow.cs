using BepInEx.Configuration;
using UnityEngine;

namespace HutchVRisingTrainer.UI;

internal sealed class TrainerWindow : IDisposable
{
    private static readonly string[] Tabs = ["Trainer", "Achievements", "Guide", "Diagnostics"];
    private readonly AchievementChecklist _checklist;
    private readonly ClientCommandService _commands;
    private readonly LocalWorldSettingsService _worldSettings;
    private readonly ConfigEntry<KeyCode> _menuKey;
    private readonly ConfigEntry<KeyCode> _instantConvertKey;
    private readonly GUI.WindowFunction _drawContents;
    private readonly List<Texture2D> _textures = [];
    private Action? _closeAction;
    private Rect _bounds = new(42, 42, 730, 705);
    private int _tab;
    private int _trainerPage;
    private int _achievementPage;
    private int _guidePage;
    private int _itemPage;
    private string _search = "";
    private string _itemQuery = "";
    private string _itemAmount = "100";
    private IReadOnlyList<ItemCatalogEntry> _itemMatches;
    private ItemCatalogEntry? _selectedItem;
    private int _activeField;
    private bool _remainingOnly = true;
    private GUISkin? _skin;
    private GUIStyle? _body;

    internal TrainerWindow(AchievementChecklist checklist, ClientCommandService commands,
        LocalWorldSettingsService worldSettings,
        ConfigEntry<KeyCode> menuKey, ConfigEntry<KeyCode> instantConvertKey)
    {
        _checklist = checklist;
        _commands = commands;
        _worldSettings = worldSettings;
        _menuKey = menuKey;
        _instantConvertKey = instantConvertKey;
        _drawContents = (Action<int>)DrawContents;
        _itemMatches = ItemCatalog.All;
    }

    internal void SetCloseAction(Action closeAction) => _closeAction = closeAction;

    internal void Draw()
    {
        if (_skin is null) CreateSkin();
        HandleKeyboard();
        var originalSkin = GUI.skin;
        var originalMatrix = GUI.matrix;
        var originalColor = GUI.color;
        var originalContentColor = GUI.contentColor;
        var originalBackgroundColor = GUI.backgroundColor;
        var originalEnabled = GUI.enabled;
        var originalDepth = GUI.depth;
        var scale = Mathf.Max(0.45f, Mathf.Min(1.15f,
            Mathf.Min(Screen.width / 780f, Screen.height / 760f)));

        try
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.skin = _skin;
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            GUI.depth = -10000;
            _bounds.x = Mathf.Clamp(_bounds.x, 0, Mathf.Max(0, Screen.width / scale - _bounds.width));
            _bounds.y = Mathf.Clamp(_bounds.y, 0, Mathf.Max(0, Screen.height / scale - _bounds.height));
            _bounds = GUILayout.Window(160403081, _bounds, _drawContents,
                $"HUTCH / V RISING / Achievement Trainer {Plugin.Version}",
                GUILayout.Width(730), GUILayout.Height(705));
        }
        finally
        {
            GUI.skin = originalSkin;
            GUI.matrix = originalMatrix;
            GUI.color = originalColor;
            GUI.contentColor = originalContentColor;
            GUI.backgroundColor = originalBackgroundColor;
            GUI.enabled = originalEnabled;
            GUI.depth = originalDepth;
        }
    }

    private void DrawContents(int id)
    {
        GUILayout.Space(8);
        var nextTab = _tab;
        GUILayout.BeginHorizontal();
        for (var index = 0; index < Tabs.Length; index++)
        {
            var label = index == _tab ? $"[ {Tabs[index]} ]" : Tabs[index];
            if (GUILayout.Button(label, GUILayout.Height(34))) nextTab = index;
        }
        GUILayout.EndHorizontal();
        if (_tab != nextTab)
        {
            _tab = nextTab;
            _activeField = 0;
        }
        GUILayout.Space(8);
        if (_tab == 0) DrawTrainer();
        else if (_tab == 1) DrawAchievements();
        else if (_tab == 2) DrawGuide();
        else DrawDiagnostics();
        GUILayout.FlexibleSpace();
        GUILayout.Space(8);
        Text(_checklist.Status);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Steam", GUILayout.Height(30))) _checklist.RequestRefresh();
        if (GUILayout.Button($"Close ({_menuKey.Value})", GUILayout.Height(30))) _closeAction?.Invoke();
        GUILayout.EndHorizontal();
        GUI.DragWindow(new Rect(0, 0, 700, 25));
    }

    private void DrawTrainer()
    {
        Heading("Live controls / local private world");
        Text(_commands.Status);
        PageControls(ref _trainerPage, 6, ["Combat", "Blood", "Items", "Build", "Travel", "Servants"]);

        var wasEnabled = GUI.enabled;
        if (_trainerPage == 0)
        {
            GUI.enabled = wasEnabled && _commands.Ready;
            Heading("Combat and survival");
            Text("God mode includes no cooldown and keeps enemies aggressive. The individual boost buttons toggle their effects on or off; check the chat reply or Show boost state.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("God mode ON (bosses aggro)", GUILayout.Height(32))) QueueCleanGodMode();
            ActionButton("Mortal / clear boosts", ".mortal");
            GUILayout.EndHorizontal();
            ActionRow("Toggle invulnerable", ".boost invincible", "Toggle no blood drain", ".boost noblooddrain");
            ActionRow("Damage boost 100", ".boost damage 100", "Remove damage boost", ".boost removedamage");
            ActionRow("Toggle no cooldown", ".boost nocooldown", "Toggle sun protection", ".boost suninvulnerable");
            ActionRow("Repair gear", ".gear repair", "Show boost state", ".boost state");
        }
        else if (_trainerPage == 1)
        {
            GUI.enabled = wasEnabled && _commands.Ready;
            Heading("100% blood achievements");
            Text("Spawn a 100% human to feed from or charm. Spawn a 99% human for the Irradiant Gruel achievement. Charm and imprison the 99% human before using gruel.");
            Text("To charm: activate the game's Dominate vampire power, aim its spell at a unit with an active blood type, and keep channeling. The spell lowers its health and takes control below 30%. V Blood targets cannot be dominated.");
            Text("Almost all blood-bearing units can go into an empty Prison Cell. Servant Coffins accept humans and some draculin units. Remove the trainer's damage boost if attacks kill your target too quickly.");
            ActionRow("Spawn 100% human", ".customspawn CHAR_ChurchOfLight_Lightweaver scholar 100 true -1 1",
                "Spawn 99% human", ".customspawn CHAR_ChurchOfLight_Lightweaver scholar 99 true -1 1");
            ActionButton("Set gruel to +1%, no mutation", ".prisoner gruel 0 1 1");
        }
        else if (_trainerPage == 2)
        {
            GUI.enabled = wasEnabled;
            DrawItems();
        }
        else if (_trainerPage == 3)
        {
            GUI.enabled = wasEnabled && _commands.Ready;
            Heading("Building and research");
            Text("Ignore material costs for building, crafting, research, and repairs in this private world.");
            ActionButton("Toggle free costs", ".build free");
            Text("This is a toggle. Read the chat reply for enabled or disabled before pressing it again.");
            Text("Placement rules and Castle Heart requirements still apply.");
        }
        else if (_trainerPage == 4)
        {
            DrawTravel();
        }
        else
        {
            DrawServants();
        }
        GUI.enabled = wasEnabled;
        Text(_trainerPage switch
        {
            2 => "Choose an item, set the amount, then Give selected. The server chat confirms the grant or explains a failure.",
            3 => "Use adminauth in the game console first. Free costs apply to everyone on the local server.",
            4 => "The travel setting applies to this private world after you leave and reload it.",
            5 => "The conversion speed applies after a world reload; instant conversion uses the local admin command.",
            _ => "Use adminauth in the game console first. Read the chat reply after each command. Master of All still requires feeding on every V Blood."
        });
    }

    private void DrawTravel()
    {
        Heading("Waygate travel");
        Text("Allow travel through waygates with any inventory items in this private world.");
        Text(_worldSettings.TravelStatus);
        var wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && _worldSettings.Available;
        var label = _worldSettings.UnrestrictedTravel
            ? "Turn inventory restriction ON"
            : "Allow travel with all items";
        if (GUILayout.Button(label, GUILayout.Height(34)))
            _worldSettings.SetUnrestrictedTravel(!_worldSettings.UnrestrictedTravel);
        GUI.enabled = wasEnabled;
        Text("This changes only the active world's TeleportBoundItems setting. The original file is backed up beside it. Reload the world to apply it.");
    }

    private void DrawServants()
    {
        Heading("Servant coffin conversion");
        Text("Speed up all servant conversions in this private world. The game's normal rate is 1x; 6x turns its 20-minute base time into roughly 3 minutes 20 seconds.");
        Text(_worldSettings.ServantStatus);
        var wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && _worldSettings.Available;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save normal 1x", GUILayout.Height(32))) _worldSettings.SetServantConvertRate(1f);
        if (GUILayout.Button("Save faster 3x", GUILayout.Height(32))) _worldSettings.SetServantConvertRate(3f);
        if (GUILayout.Button("Save fastest 6x", GUILayout.Height(32))) _worldSettings.SetServantConvertRate(6f);
        GUILayout.EndHorizontal();
        GUI.enabled = wasEnabled;
        Text("The new world speed starts after you leave and reload the world. It does not instantly finish coffins already converting.");
        Heading("Finish one coffin now");
        Text($"Close this menu, aim directly at a converting servant coffin, and press {_instantConvertKey.Value}. The F6 menu's cursor prevents reliable targeting from a button click. Requires adminauth.");
    }

    private void ActionRow(string leftLabel, string leftCommand, string rightLabel, string rightCommand)
    {
        GUILayout.BeginHorizontal();
        ActionButton(leftLabel, leftCommand);
        ActionButton(rightLabel, rightCommand);
        GUILayout.EndHorizontal();
    }

    private void ActionButton(string label, string command)
    {
        if (GUILayout.Button(label, GUILayout.Height(32))) _commands.QueueCommand(command);
    }

    private void QueueCleanGodMode()
    {
        // Upstream .god enables No Aggro, which makes V Bloods disengage and heal.
        foreach (var command in new[]
        {
            ".god",
            ".boost noaggro",
            ".boost immaterial",
            ".boost shrouded"
        }) _commands.QueueCommand(command);
        _commands.SetStatus("Queued god mode with No Aggro and ghost effects removed. Bosses can keep targeting you.");
    }

    private void DrawItems()
    {
        Heading("Item browser");
        Text("Search the bundled item list by ordinary words. Selecting a result shows its exact prefab ID.");
        InputButton("Search items", _itemQuery, 2);
        const int pageSize = 6;
        var pageCount = Math.Max(1, (_itemMatches.Count + pageSize - 1) / pageSize);
        _itemPage = Math.Clamp(_itemPage, 0, pageCount - 1);
        PageControls(ref _itemPage, pageCount, [$"{_itemMatches.Count} matching items"]);
        if (_itemMatches.Count == 0) Text("No items match. Try a broader word such as sword, blood, or stone.");
        foreach (var entry in _itemMatches.Skip(_itemPage * pageSize).Take(pageSize))
        {
            var label = (_selectedItem?.Id == entry.Id ? "[Selected] " : "") + entry.DisplayName;
            if (GUILayout.Button(label, GUILayout.Height(31))) _selectedItem = entry;
        }
        Text(_selectedItem is null
            ? "Select an item from the list."
            : $"Selected: {_selectedItem.DisplayName} / prefab ID {_selectedItem.Id}");
        GUILayout.BeginHorizontal();
        InputButton("Amount", _itemAmount, 3);
        var wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && _commands.Ready && _selectedItem is not null;
        if (GUILayout.Button("Give selected", GUILayout.Height(30))) GiveSelectedItem();
        GUI.enabled = wasEnabled;
        GUILayout.EndHorizontal();
    }

    private void GiveSelectedItem()
    {
        if (_selectedItem is null)
        {
            _commands.SetStatus("Select an item before giving it.");
            return;
        }
        if (!int.TryParse(_itemAmount, out var amount) || amount < 1 || amount > 9999)
        {
            _commands.SetStatus("Item amount must be between 1 and 9999.");
            return;
        }
        _commands.QueueCommand($".give {_selectedItem.Id} {amount}");
    }

    private void DrawAchievements()
    {
        Heading($"Steam achievements - {_checklist.EarnedCount}/{_checklist.Entries.Count}");
        _remainingOnly = GUILayout.Toggle(_remainingOnly, "Show remaining only", GUILayout.Height(27));
        InputButton("Search name, requirement, or group", _search, 1);
        var matches = _checklist.Entries.Where(entry =>
            (!_remainingOnly || !entry.Earned) &&
            (_search.Length == 0 ||
             (entry.Name + " " + entry.Description + " " + entry.Category)
                 .IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        const int pageSize = 4;
        var pageCount = Math.Max(1, (matches.Count + pageSize - 1) / pageSize);
        _achievementPage = Math.Clamp(_achievementPage, 0, pageCount - 1);
        PageControls(ref _achievementPage, pageCount, [$"{matches.Count} matching achievements"]);
        foreach (var entry in matches.Skip(_achievementPage * pageSize).Take(pageSize))
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<color=#93e3ca><b>" + (entry.Earned ? "[EARNED] " : "[REMAINING] ") + entry.Name + "</b></color>", _body);
            Text(entry.Category + " | " + entry.Description);
            GUILayout.EndVertical();
        }
    }

    private void DrawGuide()
    {
        PageControls(ref _guidePage, 3, ["Bosses", "Blood", "Other"]);
        if (_guidePage == 0)
        {
            Heading("Brutal boss milestones");
            Text("Defeat Quincey, Octavian, Cyril, Solarus, and Dracula on Brutal. The ordinary Act and Dracula achievements can unlock on this same run. Keep the world on Brutal and make a save before each milestone.");
            Heading("V Blood map navigation");
            Text("Open the game's V Blood menu, select the boss you want, and choose Track. The game shows its current direction and distance; use the map's own marker to route toward the target. Roaming bosses move, so refresh your target as you travel.");
        }
        else if (_guidePage == 1)
        {
            Heading("Blood and prisoners");
            Text("Exquisite Blood needs drinking from a living 100% source. An Eye for Quality needs charming a 100% human. A Perfect Test Subject needs Irradiant Gruel to raise a prisoner to 100%, so preserve a 99% human for that attempt.");
            Text("Dominate is the vampire power for capturing ordinary units with active blood types, including many nonhumans. Channel its spell until the target falls below 30% health, then bring it to an empty Prison Cell or use a Dusk Caller. V Bloods are excluded.");
            Text("Blood Donor requires extracting blood from a prisoner. Feeding from a potion is not a substitute for drinking from a live source.");
            Text("The Trainer tab has live buttons for 100% and 99% humans and for safe +1% gruel. You still need to feed, charm, imprison, and use the gruel in game.");
        }
        else
        {
            Heading("Research and crafting");
            Text("The three Forgotten Lore achievements trigger when you interact with a completely researched Research Desk, Study, and Athenaeum. The Build page can remove research costs, but you still unlock every technology and interact with each station.");
            Text("For A Weapon From a More Civilized Age, reforge a shattered weapon at the Ancestral Forge. Riftslayer needs an actual rift closure, and Gone Fishing needs a caught fish.");
            Heading("Easy to overlook");
            Text("Trade once with a Farbane Shady Merchant and once in Human Form. Place a staircase, plant a seed, equip a bag and cosmetic, ride a horse, use Rat Form through a small hole, eat the wrong mushroom, interact with a music box, and fly.");
            Text("Visit all eight named regions for Every Corner of the World. Fully equip a servant, use an ultimate ability, and close a rift before the final boss.");
        }
    }

    private void PageControls(ref int page, int pageCount, string[] labels)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(42), GUILayout.Height(28)))
            page = (page + pageCount - 1) % pageCount;
        GUILayout.Label(labels.Length == pageCount ? labels[page] :
            $"{labels[0]} - page {page + 1} of {pageCount}", _body);
        if (GUILayout.Button(">", GUILayout.Width(42), GUILayout.Height(28)))
            page = (page + 1) % pageCount;
        GUILayout.EndHorizontal();
    }

    private void InputButton(string label, string value, int field)
    {
        var shown = value.Length == 0 ? "click to type" : value;
        if (_activeField == field) shown += " |";
        if (GUILayout.Button(label + ": " + shown, GUILayout.Height(30))) _activeField = field;
    }

    private void HandleKeyboard()
    {
        var current = Event.current;
        if (_activeField == 0 || current is null || current.type != EventType.KeyDown) return;
        if (current.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Tab)
        {
            _activeField = 0;
            return;
        }

        var value = _activeField switch
        {
            1 => _search,
            2 => _itemQuery,
            _ => _itemAmount
        };
        if (current.keyCode == KeyCode.Backspace)
        {
            if (value.Length > 0) value = value[..^1];
        }
        else if (value.Length < (_activeField == 3 ? 6 : 120))
        {
            var character = current.character;
            var allowed = _activeField switch
            {
                1 => character is >= ' ' and <= '~',
                2 => character is >= ' ' and <= '~',
                _ => character is >= '0' and <= '9'
            };
            if (allowed) value += character;
        }

        if (_activeField == 1) _search = value;
        else if (_activeField == 2)
        {
            if (value != _itemQuery)
            {
                _itemQuery = value;
                _itemMatches = ItemCatalog.Search(value);
                _itemPage = 0;
                _selectedItem = null;
            }
        }
        else _itemAmount = value;
    }

    private void DrawDiagnostics()
    {
        Heading("Installed target");
        Text("V Rising 1.1.13 / BepInEx 6 IL2CPP / Steam App 1604030");
        Text("The checklist reads the local Steam client cache for profile " + _checklist.SteamId + " when available. For another profile it reads Steam's public achievement page. It never unlocks or clears achievements.");
        Text("Menu key: " + _menuKey.Value + ". Instant servant conversion key: " + _instantConvertKey.Value + ". Change either in BepInEx/config/hutch.vrising.achievementtrainer.cfg after closing the game.");
        Text("Status: " + _checklist.Status);
        Heading("Live command link");
        Text(_commands.Status);
        Text("Ready: " + _commands.Ready + ". Controls only send while VRisingServer.exe is running locally and the client character is loaded. Use adminauth in the game console before sending admin commands.");
        Text("Refresh Steam names its data source. The local cache status shows when Steam last saved it; the BepInEx log records refresh errors.");
    }

    private void Heading(string content)
    {
        GUILayout.Space(11);
        GUILayout.Label("<color=#93e3ca><b>" + content + "</b></color>", _body);
    }

    private void Text(string content) => GUILayout.Label(content, _body);

    private void CreateSkin()
    {
        _skin = UnityEngine.Object.Instantiate(GUI.skin);
        _skin.hideFlags = HideFlags.HideAndDontSave;
        var backdrop = Solid(new Color32(20, 26, 33, 255));
        var card = Solid(new Color32(31, 41, 51, 255));
        var button = Solid(new Color32(47, 62, 76, 255));
        var hover = Solid(new Color32(64, 85, 101, 255));
        var active = Solid(new Color32(33, 107, 92, 255));
        _skin.window = Surface(_skin.window, backdrop, backdrop, backdrop);
        _skin.window.padding = Spacing(14, 14, 30, 14);
        _skin.window.fontSize = 16;
        _skin.window.fontStyle = FontStyle.Bold;
        _skin.box = Surface(_skin.box, card, card, card);
        _skin.box.padding = Spacing(12, 12, 10, 10);
        _skin.button = Surface(_skin.button, button, hover, active);
        _skin.button.fontSize = 14;
        _skin.button.padding = Spacing(7, 7, 4, 4);
        _skin.toggle = Surface(_skin.toggle, button, hover, active);
        _skin.toggle.alignment = TextAnchor.MiddleLeft;
        _skin.textField = Surface(_skin.textField, card, hover, card);
        _skin.textField.fontSize = 16;
        _skin.label.fontSize = 16;
        _skin.label.wordWrap = true;
        _skin.label.richText = true;
        _skin.label.normal.textColor = new Color32(239, 245, 250, 255);
        _skin.verticalScrollbar = Surface(_skin.verticalScrollbar, card, card, card);
        _skin.verticalScrollbarThumb = Surface(_skin.verticalScrollbarThumb, hover, active, active);
        _body = _skin.label;
    }

    private Texture2D Solid(Color32 color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply(false, true);
        _textures.Add(texture);
        return texture;
    }

    private static GUIStyle Surface(GUIStyle source, Texture2D normal, Texture2D hover, Texture2D selected)
    {
        var style = source;
        style.border = Spacing(0, 0, 0, 0);
        Paint(style.normal, normal);
        Paint(style.hover, hover);
        Paint(style.active, selected);
        Paint(style.focused, hover);
        Paint(style.onNormal, selected);
        Paint(style.onHover, selected);
        Paint(style.onActive, selected);
        Paint(style.onFocused, selected);
        return style;
    }

    private static RectOffset Spacing(int left, int right, int top, int bottom)
    {
        return new RectOffset { left = left, right = right, top = top, bottom = bottom };
    }

    private static void Paint(GUIStyleState state, Texture2D texture)
    {
        state.background = texture;
        state.textColor = Color.white;
    }

    public void Dispose()
    {
        if (_skin is not null) UnityEngine.Object.Destroy(_skin);
        foreach (var texture in _textures) UnityEngine.Object.Destroy(texture);
        _textures.Clear();
    }
}
