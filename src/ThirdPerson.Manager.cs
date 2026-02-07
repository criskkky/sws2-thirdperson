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
                // Cleanup camera using scheduler for safety
                Core.Scheduler.NextWorldUpdate(() =>
                {
                    if (camera != null && camera.IsValid)
                    {
                        camera.Despawn();
                    }
                });
                player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.camera_error"]}");
                return;
            }

            // Store as CHandle for safe long-term reference
            var handle = Core.EntitySystem.GetRefEHandle(camera);
            _thirdPersonPool.TryAdd(playerIndex, handle);
            
            player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.activated"]}");
        }
        else
        {
            if (player.Pawn?.CameraServices != null)
            {
                player.Pawn.CameraServices.ViewEntity.Raw = uint.MaxValue;
                player.Pawn.CameraServices.ViewEntityUpdated();
            }

            if (_thirdPersonPool.TryRemove(playerIndex, out var cameraHandle))
            {
                if (cameraHandle.IsValid && cameraHandle.Value != null)
                {
                    Core.Scheduler.NextWorldUpdate(() =>
                    {
                        // Revalidate before despawning
                        if (cameraHandle.IsValid && cameraHandle.Value != null)
                        {
                            cameraHandle.Value.Despawn();
                        }
                    });
                }
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

            // Store as CHandle for safe long-term reference
            var handle = Core.EntitySystem.GetRefEHandle(camera);
            _smoothThirdPersonPool.TryAdd(playerIndex, handle);

            // Set player's view entity to the camera on next tick
            // CRITICAL: Revalidate all entities inside callback to prevent crashes
            Core.Scheduler.NextTick(() =>
            {
                // Revalidate player, pawn and camera inside callback
                if (player == null || !player.IsValid) return;
                if (player.Pawn == null || !player.Pawn.IsValid) return;
                if (player.Pawn.CameraServices == null) return;
                if (!handle.IsValid || handle.Value == null)
                {
                    _smoothThirdPersonPool.TryRemove(playerIndex, out _);
                    return;
                }

                var cameraHandle = Core.EntitySystem.GetRefEHandle(handle.Value);
                player.Pawn.CameraServices.ViewEntity.Raw = cameraHandle.Raw;
                player.Pawn.CameraServices.ViewEntityUpdated();
            });
            
            player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.activated"]}");

        }
        else
        {
            if (player.Pawn?.CameraServices != null)
            {
                player.Pawn.CameraServices.ViewEntity.Raw = uint.MaxValue;
                player.Pawn.CameraServices.ViewEntityUpdated();
            }

            if (_smoothThirdPersonPool.TryRemove(playerIndex, out var cameraHandle))
            {
                if (cameraHandle.IsValid && cameraHandle.Value != null)
                {
                    Core.Scheduler.NextWorldUpdate(() =>
                    {
                        // Revalidate before despawning
                        if (cameraHandle.IsValid && cameraHandle.Value != null)
                        {
                            cameraHandle.Value.Despawn();
                        }
                    });
                }
            }

            player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.deactivated"]}");

        }
    }
}
