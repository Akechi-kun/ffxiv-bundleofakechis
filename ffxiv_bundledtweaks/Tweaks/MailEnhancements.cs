using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using System.Threading.Tasks;

namespace Automaton.Tweaks;

public class MailEnhanacementsConfig {
    [BoolConfig] public bool EnableRetrieveAll = true;
    [BoolConfig(DependsOn = nameof(EnableRetrieveAll))] public bool DeleteAfterRetrieval = true;
    [BoolConfig(DependsOn = nameof(EnableRetrieveAll))] public bool UseAfterRetrieval = true;
    [BoolConfig] public bool EnableRestockMaps = true;
}

#if DEBUG
[Tweak]
public class MailEnhancements : Tweak<MailEnhanacementsConfig> {
    public override string Name => "Mail Enhancments";
    public override string Description => "Adds some buttons to the mailbox addon for inbox management";

    private AddonController<AtkUnitBase>? _controller;
    private readonly List<CircleButtonNode> _btns = [];

    public override unsafe void Enable() {
        _controller = new AddonController<AtkUnitBase> {
            AddonName = "LetterList",
            OnSetup = OnLetterListSetup,
            OnRefresh = OnLetterListRefresh,
            OnFinalize = OnLetterListFinalize
        };
        _controller.Enable();
    }

    public override void Disable() {
        DisposeButtons();
        _controller?.Dispose();
    }

    private unsafe void OnLetterListSetup(AtkUnitBase* addon) {
        DisposeButtons();

        CircleButtonNode? cancelTasks = null;
        if (Config.EnableRetrieveAll) {
            var retrieveAll = new CircleButtonNode {
                Icon = ButtonIcon.ArrowDown,
                TextTooltip = "Retrieve All Mail",
                OnClick = () => {
                    _btns.Where(b => !ReferenceEquals(b, cancelTasks)).ForEach(b => b.IsEnabled = false);
                    Svc.Automation.Start(new RetrieveAllTask(Config.DeleteAfterRetrieval, Config.UseAfterRetrieval));
                }
            };
            _btns.Add(retrieveAll);
        }

        if (Config.EnableRestockMaps) {
            var restockMaps = new CircleButtonNode {
                Icon = ButtonIcon.MagnifyingGlass,
                TextTooltip = "Restock Maps",
                OnClick = () => {
                    _btns.Where(b => !ReferenceEquals(b, cancelTasks)).ForEach(b => b.IsEnabled = false);
                    Svc.Automation.Start(new RestockMapsTask());
                }
            };
            _btns.Add(restockMaps);
        }

        if (_btns.Any()) {
            cancelTasks = new CircleButtonNode {
                Icon = ButtonIcon.Cross,
                TextTooltip = "Cancel Tasks",
                OnClick = () => {
                    Svc.Automation.Stop();
                    _btns.ForEach(x => x.IsEnabled = true);
                }
            };
            _btns.Add(cancelTasks);
        }

        AttachButtons(addon);
    }

    private unsafe void OnLetterListRefresh(AtkUnitBase* addon) {
        AttachButtons(addon);
    }

    private unsafe void AttachButtons(AtkUnitBase* addon) {
        if (_btns.Count is 0) return;

        var anchorNode = addon->UldManager.SearchNodeById(5); // display only unread filter button
        if (anchorNode is null) {
            _btns.ForEach(btn => btn.IsVisible = false);
            return;
        }

        foreach (var (btn, i) in _btns.AsEnumerable().Reverse().WithIndex()) {
            if (btn.Node is null || btn.Node->ParentNode is null) {
                btn.AttachNode(addon);
            }

            btn.Size = anchorNode->Size;
            btn.IsVisible = true;
            btn.Position = new Vector2(anchorNode->X - (i + 1) * 30, anchorNode->Y);
        }
    }

    private unsafe void OnLetterListFinalize(AtkUnitBase* addon) {
        DisposeButtons();
    }

    private void DisposeButtons() {
        _btns.ForEach(x => x.Dispose());
        _btns.Clear();
    }

    private class RetrieveAllTask(bool deleteAfterRetrieval, bool useAfterRetrieval) : TaskBase {
        protected override Task Execute() {
            Svc.Chat.Print($"retrieve all test!");
            return Task.CompletedTask;
        }
    }

    private class RestockMapsTask : AutoTask {
        protected override Task Execute() {
            Svc.Chat.Print($"restock maps test!");
            return Task.CompletedTask;
        }
    }
}
#endif
