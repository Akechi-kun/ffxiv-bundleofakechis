using ComplexTweaks.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace ComplexTweaks.Tweaks;

[Tweak]
public unsafe class AetherCurrentAutomation : Tweak {
    public override string Name => "Aether Current Automation";
    public override string Description => !Enabled ? "Enable to access this tweak." : "Automatically pathfind and attune to Aether Currents.";

    private readonly Dictionary<(Type, uint, ClientLanguage), string> _rowName = [];
    private readonly Dictionary<uint, EObj> _eobj = [];
    private readonly Dictionary<uint, Level> _level = [];

    private static readonly Vector4 Green = new(0f, 0.6f, 0.1f, 1f);
    private static readonly Vector4 LightGold = new(1f, 0.98f, 0.8f, 1f);

    public override void DrawConfig() {

        ImGui.Spacing();
        ImGui.Separator();

        if (!Svc.ClientState.IsLoggedIn) {
            ImGui.TextDisabled("Log in to access this tweak.");
            return;
        }

        if (!Enabled) {
            ImGui.TextDisabled("Enable to access this tweak.");
            return;
        }

        var playerState = PlayerState.Instance();
        var sheet = Svc.Data.GetExcelSheet<AetherCurrentCompFlgSet>();
        if (playerState == null || sheet == null) {
            ImGui.TextDisabled("Failed to load core files.");
            return;
        }

        var expansions = sheet
            .Where(row => row.Territory.RowId != 0)
            .GroupBy(row => row.Territory.Value.ExVersion.RowId)
            .OrderBy(g => g.Key);

        foreach (var group in expansions) {
            DrawExpansion(group, playerState);
        }
    }
    private void DrawExpansion(IEnumerable<AetherCurrentCompFlgSet> group, PlayerState* playerState) {
        var name = group.First().Territory.Value.ExVersion.Value.Name.ToString();
        if (!ImGui.TreeNodeEx(name, ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var zones = group
            .GroupBy(row => row.Territory.RowId)
            .OrderBy(g => g.Key);

        foreach (var set in zones) {
            DrawZone(set, playerState);
        }

        ImGui.TreePop();
    }
    private void DrawZone(IEnumerable<AetherCurrentCompFlgSet> group, PlayerState* playerState) {
        var zone = group.First().Territory.Value;
        var name = zone.PlaceName.Value.Name.ToString();

        if (!ImGui.TreeNode(name))
            return;

        var currents = group
            .SelectMany(set => set.AetherCurrents)
            .Where(ac => ac.RowId != 0)
            .Select(ac => ac.Value)
            .ToList();

        var total = currents.Count;
        var current = currents.Count(c => playerState->IsAetherCurrentUnlocked(c.RowId));
        var progress = total > 0 ? (float)current / total : 0f;

        ImGui.DrawProgressBar(current, total, current == total ? Green : new(1f, 0.55f, 0f, 1f));
        if (Service.Automation.Running) {
            ImGui.SameLine();
            if (ImGui.IconButton(FontAwesomeIcon.Stop, "Stop", "If you can see this, that means you already have a task currently under automation. Click here to stop it.")) {
                Service.Automation.Stop();
            }
        }
        ImGui.Spacing();

        var quests = currents.Where(c => c.Quest.RowId != 0).ToList();
        var fields = currents.Where(c => c.Quest.RowId == 0).ToList();

        void DrawCurrents(List<AetherCurrent> list, ImU8String label) {
            if (list.Count > 0) {
                if (ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen)) {
                    DrawTable(list, playerState);
                    ImGui.Spacing();
                }
            }
        }

        DrawCurrents(quests, $"Quest Currents ({quests.Count})");
        DrawCurrents(fields, $"Field Currents ({fields.Count})");

        ImGui.TreePop();
    }
    private void DrawTable(List<AetherCurrent> currents, PlayerState* playerState) {
        if (!ImGui.BeginTable("##currents", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 70);

        foreach (var current in currents) {
            var unlocked = playerState->IsAetherCurrentUnlocked(current.RowId);
            var label = AetherCurrentLabel(current, out var coords, out _);
            var isQuest = current.Quest.RowId != 0;

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            using (ImRaii.PushFont(UiBuilder.IconFont)) {
                ImGui.TextColored(unlocked ? Green : new(1f, 0f, 0f, 1f),
                    unlocked ? FontAwesomeIcon.Check.ToIconString() : FontAwesomeIcon.Times.ToIconString());
            }

            ImGui.TableSetColumnIndex(1);
            if (isQuest) {
                var questName = GetQuestName(current.Quest.RowId);
                ImGui.TextColored(new Vector4(0.8f, 1f, 1f, 1f), questName);

                ImGui.SameLine();
                ImGui.TextUnformatted("-");
                ImGui.SameLine();
                ImGui.TextColored(LightGold, $"X: {coords.X:0.0}, Y: {coords.Y:0.0}");
            }
            else {
                ImGui.TextColored(LightGold, $"X: {coords.X:0.0}, Y: {coords.Y:0.0}");
            }

            ImGui.TableSetColumnIndex(2);
            if (!unlocked) {
                if (ImGui.SmallButton($"Go##{current.RowId}")) {
                    if (isQuest) {
                        var questSheet = Svc.Data.GetExcelSheet<Quest>();
                        var quest = questSheet?.GetRow(current.Quest.RowId);

                        if (quest != null)
                            Service.Automation.Start(new GoToAetherCurrentQuest(quest.Value));
                    }
                    else if (TryGetLevel(current.RowId, out var level)) {
                        Service.Automation.Start(new GoToAetherCurrent(level));
                    }
                }
            }
            else {
                ImGui.TextColored(Green, "Attuned!");
            }
        }

        ImGui.EndTable();
    }
    private string AetherCurrentLabel(AetherCurrent current, out Vector3 coords, out uint territory) {
        coords = Vector3.Zero;
        territory = 0;

        if (current.Quest.RowId != 0) {
            var name = GetQuestName(current.Quest.RowId);
            var sheet = Svc.Data.GetExcelSheet<Quest>();
            if (sheet != null) {
                var quest = sheet.GetRow(current.Quest.RowId);
                if (quest.IssuerLocation.RowId != 0) {
                    var level = quest.IssuerLocation.Value;
                    coords = Coordinates(level);
                    territory = level.Territory.RowId;
                    return $"{name} ({coords.X:0.0}, {coords.Y:0.0})";
                }
            }

            return $"{name} (Unknown Position)";
        }

        if (TryGetLevel(current.RowId, out var levelField)) {
            coords = Coordinates(levelField);
            territory = levelField.Territory.RowId;
            return $"X: {coords.X:0.0}, Y: {coords.Y:0.0}";
        }

        return "X: Unknown, Y: Unknown";
    }

    //not sure if any of these methods have been implemented already before
    private bool TryGetLevel(uint aetherCurrentRowId, out Level level) {
        level = default;

        if (!_eobj.TryGetValue(aetherCurrentRowId, out var eobj)) {
            if (!Svc.Data.TryFindRow(row => row.Data.RowId == aetherCurrentRowId, out eobj))
                return false;

            _eobj[aetherCurrentRowId] = eobj;
        }

        if (!_level.TryGetValue(eobj.RowId, out level)) {
            if (!Svc.Data.TryFindRow(row => row.Object.RowId == eobj.RowId, out level))
                return false;

            _level[eobj.RowId] = level;
        }

        return true;
    }
    private string GetQuestName(uint id, ClientLanguage? language = null) => GetOrCreatedText<Quest>(id, language, row => row.Name);
    private string GetOrCreatedText<T>(uint rowId, ClientLanguage? language, Func<T, ReadOnlySeString> getText)
        where T : struct, IExcelRow<T> {
        var lang = language ?? Svc.Data.Language;
        var key = (typeof(T), rowId, lang);

        if (_rowName.TryGetValue(key, out var text))
            return text;

        if (!Svc.Data.TryGetRow<T>(rowId, lang, out var row)) {
            text = $"{typeof(T).Name}#{rowId}";
            _rowName[key] = text;
            return text;
        }

        var seText = getText(row);
        text = seText.IsEmpty ? $"{typeof(T).Name}#{rowId}" : seText.ToString();

        _rowName[key] = text;
        return text;
    }
    public Vector3 Coordinates(Level level) {
        var map = level.Map.Value;
        var c = map.SizeFactor / 100.0f;
        var x = 41.0f / c * (((level.X + map.OffsetX) * c + 1024.0f) / 2048.0f) + 1f;
        var y = 41.0f / c * (((level.Z + map.OffsetY) * c + 1024.0f) / 2048.0f) + 1f;

        return new Vector3(x, y, 0);
    }
}
