using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;

namespace ThirdPerson;

public partial class ThirdPerson
{
    [Command("thirdperson", registerRaw: false)]
    public void OnThirdPersonCommand(ICommandContext context)
    {
        HandleThirdPersonToggle(context);
    }

    private void RegisterCustomCommand()
    {
        // Unregister previous command if exists
        if (_customCommandGuid.HasValue)
        {
            Core.Command.UnregisterCommand(_customCommandGuid.Value);
            _customCommandGuid = null;
        }

        if (!string.IsNullOrEmpty(Config.CustomTPCommand) && 
            Config.CustomTPCommand != "thirdperson")
        {
            _customCommandGuid = Core.Command.RegisterCommand(Config.CustomTPCommand, (context) =>
            {
                HandleThirdPersonToggle(context);
            }, registerRaw: false, permission: Config.UseTpPermission);
        }
    }

    private void HandleThirdPersonToggle(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null) return;

        if (Core.ConVar.Find<bool>("thirdperson_enabled")?.Value != true)
        {
            player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.disabled"]}");
            return;
        }

        // Check permissions if permission is set
        if (!string.IsNullOrEmpty(Config.UseTpPermission) && !Core.Permission.PlayerHasPermission(player.SteamID, Config.UseTpPermission))
        {
            player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.no_permission"]}");
            return;
        }

        // Check if player is alive
        if (!player.Controller.PawnIsAlive)
        {
            player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.must_be_alive"]}");
            return;
        }

        // Toggle third person based on config
        if (Config.UseSmoothCam)
        {
            ToggleSmoothThirdPerson(player);
        }
        else
        {
            ToggleDefaultThirdPerson(player);
        }
    }
}
