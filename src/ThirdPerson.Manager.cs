using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;

namespace ThirdPerson;

public partial class ThirdPerson
{
    private bool IsSafeToCreateCamera(IPlayer player)
    {
        if (player == null || !player.IsValid) return false;
        if (player.Pawn == null || !player.Pawn.IsValid) return false;
        if (player.Pawn.CameraServices == null) return false;
        return true;
    }

    private void ToggleDefaultThirdPerson(IPlayer player)
    {
        int playerIndex = player.PlayerID;

        if (!_thirdPersonPool.ContainsKey(playerIndex))
        {
            if (!IsSafeToCreateCamera(player))
            {
                player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.not_ready"]}");
                return;
            }

            // Create camera entity using designer name (like AdminESP does)
            var cameraProp = SafeSpawnDynamicProp("prop_dynamic");
            
            if (cameraProp == null)
            {
                player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.camera_error"]}");
                return;
            }

            // Make it invisible by setting alpha to 0
            cameraProp.Render = new SwiftlyS2.Shared.Natives.Color(255, 255, 255, 0);

            // Calculate initial position
            Vector initialPos = CalculatePositionInFront(player, -Config.ThirdPersonDistance, Config.ThirdPersonHeight);
            QAngle viewAngle = player.Pawn?.V_angle ?? new QAngle();

            cameraProp.Teleport(initialPos, viewAngle, Vector.Zero);

            if (player.Pawn?.CameraServices != null)
            {
                var cameraHandle = Core.EntitySystem.GetRefEHandle(cameraProp);
                player.Pawn.CameraServices.ViewEntity.Raw = cameraHandle.Raw;
                player.Pawn.CameraServices.ViewEntityUpdated();
            }
            else
            {
                player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.camera_error"]}");
            }

            _thirdPersonPool.TryAdd(playerIndex, cameraProp);
            
            player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.activated"]}");
        }
        else
        {
            if (player.Pawn?.CameraServices != null)
            {
                player.Pawn.CameraServices.ViewEntity.Raw = uint.MaxValue;
                player.Pawn.CameraServices.ViewEntityUpdated();
            }

            if (_thirdPersonPool.TryRemove(playerIndex, out var camera))
            {
                camera.Despawn();
            }

            player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.deactivated"]}");

        }
    }

    private void ToggleSmoothThirdPerson(IPlayer player)
    {
        int playerIndex = player.PlayerID;

        if (!_smoothThirdPersonPool.ContainsKey(playerIndex))
        {
            if (!IsSafeToCreateCamera(player))
            {
                player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.not_ready"]}");
                return;
            }

            // Activate smooth third person
            var camera = SafeSpawnPointCamera("point_camera");

            if (camera == null)
            {
                player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.camera_error"]}");
                return;
            }

            // Calculate initial position
            Vector initialPos = CalculatePositionInFront(player, -Config.ThirdPersonDistance, Config.ThirdPersonHeight);
            QAngle viewAngle = player.Pawn?.V_angle ?? new QAngle();

            camera.Teleport(initialPos, viewAngle, Vector.Zero);

            // Set player's view entity to the camera on next tick
            Core.Scheduler.NextTick(() =>
            {
                if (player?.Pawn?.CameraServices != null)
                {
                    var cameraHandle = Core.EntitySystem.GetRefEHandle(camera);
                    player.Pawn.CameraServices.ViewEntity.Raw = cameraHandle.Raw;
                    player.Pawn.CameraServices.ViewEntityUpdated();
                }
            });

            _smoothThirdPersonPool.TryAdd(playerIndex, camera);
            
            player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.activated"]}");

        }
        else
        {
            if (player.Pawn?.CameraServices != null)
            {
                player.Pawn.CameraServices.ViewEntity.Raw = uint.MaxValue;
                player.Pawn.CameraServices.ViewEntityUpdated();
            }

            if (_smoothThirdPersonPool.TryRemove(playerIndex, out var camera))
            {
                camera.Despawn();
            }

            player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.deactivated"]}");

        }
    }
}
