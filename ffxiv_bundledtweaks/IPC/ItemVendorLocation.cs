using ECommons.EzIpcManager;

namespace ComplexTweaks.IPC;

#nullable disable
[Ipc(Ipc.ItemVendorLocation)]
public class ItemVendorLocation : BaseIPC {
    public override string Name => "ItemVendorLocation";
    public override string Repo => Main;
    public ItemVendorLocation() => EzIPC.Init(this, Name);

    [EzIPC] public Func<uint, object> OpenVendorResults;
}
