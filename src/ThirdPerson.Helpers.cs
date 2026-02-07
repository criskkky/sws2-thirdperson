using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace ThirdPerson;

public partial class ThirdPerson
{
    // Updates the position of a point camera (instant positioning).
    // Used for the default third-person camera mode.
    private static void UpdateCamera(
        CPointCamera camera,
        IPlayer player,
        ISwiftlyCore core,
        float desiredDistance,
        float verticalOffset)
    {
        if (player?.Pawn == null || camera == null)
            return;

        var pawn = player.Pawn;
        if (pawn.AbsOrigin == null)
            return;

        Vector cameraPos = CalculateSafeCameraPosition(player, core, desiredDistance, verticalOffset);
        QAngle cameraAngle = pawn.V_angle;

        camera.Teleport(cameraPos, cameraAngle, Vector.Zero);
    }

    // Updates the position of a point camera with smooth interpolation.
    // Provides smoother camera movement but may have slight delay.
    private static void UpdateCameraSmooth(
        CPointCamera camera,
        IPlayer player,
        ISwiftlyCore core,
        float desiredDistance,
        float verticalOffset,
        float smoothSpeed)
    {
        if (player?.Pawn == null || camera == null)
            return;

        var pawn = player.Pawn;
        if (pawn.AbsOrigin == null)
            return;

        Vector targetPos = CalculateSafeCameraPosition(player, core, desiredDistance, verticalOffset);
        QAngle targetAngle = pawn.V_angle;

        Vector currentPos = camera.AbsOrigin ?? Vector.Zero;

        // Smooth interpolation factor
        float lerpFactor = smoothSpeed;

        Vector smoothedPos = Lerp(currentPos, targetPos, lerpFactor);

        camera.Teleport(smoothedPos, targetAngle, Vector.Zero);
    }

    // Calculates a position in front of the player at a specified offset.
    // Used for initial camera positioning and other calculations.
    private static Vector CalculatePositionInFront(
        IPlayer player,
        float offsetXY,
        float offsetZ = 0)
    {
        var pawn = player?.Pawn;
        if (pawn?.AbsOrigin == null)
            return Vector.Zero;

        float yawAngleRadians = (float)(pawn.V_angle.Y * Math.PI / 180.0);
        float offsetX = offsetXY * (float)Math.Cos(yawAngleRadians);
        float offsetY = offsetXY * (float)Math.Sin(yawAngleRadians);

        return new Vector
        {
            X = pawn.AbsOrigin.Value.X + offsetX,
            Y = pawn.AbsOrigin.Value.Y + offsetY,
            Z = pawn.AbsOrigin.Value.Z + offsetZ,
        };
    }

    // Calculates a safe camera position that avoids collisions with walls/obstacles.
    // Uses raycasting to detect geometry between player and desired camera position.
    // If collision is detected, moves camera closer to player to avoid clipping.
    private static Vector CalculateSafeCameraPosition(
        IPlayer player,
        ISwiftlyCore core,
        float desiredDistance,
        float verticalOffset = 70f)
    {
        if (player?.Pawn?.AbsOrigin == null)
            return Vector.Zero;

        var pawn = player.Pawn;
        Vector pawnPos = pawn.AbsOrigin ?? Vector.Zero;

        float yawRadians = pawn.V_angle.Y * (float)Math.PI / 180f;
        var backwardDir = new Vector(-MathF.Cos(yawRadians), -MathF.Sin(yawRadians), 0);
        var eyePos = pawnPos + new Vector(0, 0, verticalOffset);
        var targetCamPos = eyePos + backwardDir * desiredDistance;

        // Perform ray trace to check for wall collisions
        CGameTrace trace = new();
            core.Trace.SimpleTrace(
            eyePos,                          // Start from player eye position
            targetCamPos,                     // End at desired camera position
            RayType_t.RAY_TYPE_LINE,           // Use line ray for precise collision
            RnQueryObjectSet.All,                // objectQuery - consider all objects
            MaskTrace.Player | MaskTrace.Solid | MaskTrace.WorldGeometry | MaskTrace.Window, // interactWith - block line of sight, solid objects, world geometry
            0,                                // interactExclude  
            0,                                // interactAs
            CollisionGroup.Default,             // collision group
            ref trace,                        // Output trace result
            pawn                             // Filter out the player itself
        );

        Vector candidatePos;
        if (trace.DidHit)
        {
            // Move camera slightly forward from hit point to avoid z-fighting
            candidatePos = trace.HitPoint - backwardDir * 5f;
        }
        else
        {
            candidatePos = targetCamPos;
        }

        return candidatePos;
    }

    // Determines if player1 is in front of player2 (within their field of view).
    // Used for damage blocking logic in third-person mode.
    // A negative dot product means player2 is behind player1.
    private static bool IsInfrontOfPlayer(
        IPlayer player1,
        IPlayer player2)
    {
        if (player1?.Pawn == null || player2?.Pawn == null)
            return false;

        var player1Pawn = player1.Pawn;
        var player2Pawn = player2.Pawn;

        if (player1Pawn.AbsOrigin == null || player2Pawn.AbsOrigin == null)
            return false;

        var yawAngleRadians = (float)(player1Pawn.V_angle.Y * Math.PI / 180.0);

        Vector player1Direction = new(MathF.Cos(yawAngleRadians), MathF.Sin(yawAngleRadians), 0);
        Vector player1ToPlayer2 = (player2Pawn.AbsOrigin ?? Vector.Zero) - (player1Pawn.AbsOrigin ?? Vector.Zero);

        float dotProduct = Vector.Dot(player1ToPlayer2, player1Direction);

        return dotProduct < 0;
    }

    // Linear interpolation between two vectors.
    // Used for smooth camera movement transitions in smooth camera mode.
    // Formula: result = from + (to - from) * t
    private static Vector Lerp(Vector from, Vector to, float t)
    {
        return new Vector
        {
            X = from.X + (to.X - from.X) * t,
            Y = from.Y + (to.Y - from.Y) * t,
            Z = from.Z + (to.Z - from.Z) * t
        };
    }

    // Thread-safe spawn method for camera entities
    private CPointCamera? SafeSpawnPointCamera(string designerName)
    {
        var entity = Core.EntitySystem.CreateEntityByDesignerName<CEntityInstance>(designerName);
        if (entity != null)
        {
            entity.DispatchSpawn();
            return entity as CPointCamera;
        }
        return null;
    }
    // Returns true if a wall is detected, preventing third person activation to avoid model invisibility.
    // Uses bounding box traces to detect walls and obstacles in the direction the player is looking
    private bool IsWallInFront(IPlayer player, float minWallDistance = 50f)
    {
        if (player?.PlayerPawn?.AbsOrigin == null)
            return false;

        var pawn = player.PlayerPawn;
        Vector pawnPos = pawn.AbsOrigin ?? Vector.Zero;
        
        // Calculate direction from player's eye angles (where they're actually looking)
        float yaw = pawn.EyeAngles.Yaw * (float)Math.PI / 180f;
        float pitch = pawn.EyeAngles.Pitch * (float)Math.PI / 180f;
        
        // Calculate 3D forward direction based on eye angles
        var forwardDir = new Vector(
            MathF.Cos(yaw) * MathF.Cos(pitch),
            MathF.Sin(yaw) * MathF.Cos(pitch),
            -MathF.Sin(pitch)
        );
        
        Vector startPos = pawnPos + new Vector(0, 0, 64f); // Eye level height
        Vector endPos = startPos + forwardDir * 40f;
        
        // Define a bounding box matching standard player hull size (32 units total, ±16 from center)
        // This accurately represents player width for wall detection
        BBox_t bounds = new()
        {
            Mins = new Vector(-16f, -16f, -10f), // Standard player width and small height tolerance
            Maxs = new Vector(16f, 16f, 10f)
        };
        
        // Configure trace filter
        var filter = new CTraceFilter(checkIgnoredEntities: true)
        {
            QueryShapeAttributes = new RnQueryShapeAttr_t
            {
                InteractsWith = MaskTrace.Solid | MaskTrace.WorldGeometry | MaskTrace.Window,
                InteractsExclude = 0,
                InteractsAs = 0,
                CollisionGroup = CollisionGroup.Default,
                ObjectSetMask = RnQueryObjectSet.All
            }
        };
        
        CGameTrace trace = new();
        Core.Trace.TracePlayerBBox(startPos, endPos, bounds, filter, ref trace);
        
        if (trace.DidHit && trace.Distance < minWallDistance)
            return true;
        
        // Additional check when looking up to detect low ceilings/obstacles above
        if (pawn.EyeAngles.Pitch < -10f)
        {
            Vector upStart = pawnPos + new Vector(0, 0, 64f); // Eye level
            Vector upEnd = upStart + new Vector(0, 0, 10f); // 10 units straight up
            
            BBox_t upBounds = new()
            {
                Mins = new Vector(-16f, -16f, 0),
                Maxs = new Vector(16f, 16f, 32f)
            };
            
            CGameTrace upTrace = new();
            Core.Trace.TracePlayerBBox(upStart, upEnd, upBounds, filter, ref upTrace);
            
            if (upTrace.DidHit && upTrace.Distance < minWallDistance)
                return true;
        }
        
        return false;
    }

    // Thread-safe despawn method for camera entities using CHandle
    // Schedules despawn in next world update and revalidates handle
    private void SafeDespawn(CHandle<CPointCamera> cameraHandle)
    {
        if (cameraHandle.IsValid && cameraHandle.Value != null)
        {
            Core.Scheduler.NextWorldUpdate(() =>
            {
                // Revalidate handle before despawning
                if (cameraHandle.IsValid && cameraHandle.Value != null)
                {
                    cameraHandle.Value.Despawn();
                }
            });
        }
    }

    // Cleanup all active cameras and restore normal view for all players
    // Used when the plugin is disabled via convar
    // Uses scheduler for safe entity operations
    private void CleanupAllCameras()
    {
        // Cleanup smooth cameras
        foreach (var kvp in _smoothThirdPersonPool.ToList())
        {
            var player = Core.PlayerManager.GetPlayer(kvp.Key);
            var cameraHandle = kvp.Value;
            
            // Restore player view
            if (player?.IsValid == true && player.Pawn?.CameraServices != null)
            {
                player.Pawn.CameraServices.ViewEntity.Raw = uint.MaxValue;
                player.Pawn.CameraServices.ViewEntityUpdated();
            }
            
            // Remove camera entity using scheduler for safety
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
            
            _smoothThirdPersonPool.TryRemove(kvp.Key, out _);
        }
        
        // Cleanup default cameras
        foreach (var kvp in _thirdPersonPool.ToList())
        {
            var player = Core.PlayerManager.GetPlayer(kvp.Key);
            var cameraHandle = kvp.Value;
            
            // Restore player view
            if (player?.IsValid == true && player.Pawn?.CameraServices != null)
            {
                player.Pawn.CameraServices.ViewEntity.Raw = uint.MaxValue;
                player.Pawn.CameraServices.ViewEntityUpdated();
            }
            
            // Remove camera entity using scheduler for safety
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
            
            _thirdPersonPool.TryRemove(kvp.Key, out _);
        }
    }
}
