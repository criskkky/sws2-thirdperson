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

            // Check if player is too close to a front wall
            if (IsWallInFront(player, 40f))
            {
                player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.too_close_wall"]}");
                return;
            }

            // Create camera entity using point_camera instead of prop_dynamic
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

            if (player.Pawn?.CameraServices != null)
            {
                var cameraHandle = Core.EntitySystem.GetRefEHandle(camera);
                player.Pawn.CameraServices.ViewEntity.Raw = cameraHandle.Raw;
                player.Pawn.CameraServices.ViewEntityUpdated();
            }
            else
            {
                player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.camera_error"]}");
            }

            _thirdPersonPool.TryAdd(playerIndex, camera);
            
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

            // Check if player is too close to a front wall
            if (IsWallInFront(player, 40f))
            {
                player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.too_close_wall"]}");
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
