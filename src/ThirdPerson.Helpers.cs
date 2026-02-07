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

    // Checks if there's a wall in front of the player within specified distance.
    // Returns true if a wall is detected, preventing third person activation to avoid model invisibility.
    // Uses player origin to handle irregular walls at different heights.
    private bool IsWallInFront(IPlayer player, float minWallDistance = 50f)
    {
        if (player?.Pawn?.AbsOrigin == null)
            return false;

        var pawn = player.Pawn;
        Vector pawnPos = pawn.AbsOrigin ?? Vector.Zero;
        
        // Calculate forward direction from player's view angles (horizontal direction only, no pitch)
        float yaw = pawn.V_angle.Yaw * (float)Math.PI / 180f;
        var forwardDir = new Vector(
            MathF.Cos(yaw),
            MathF.Sin(yaw),
            0  // Keep Z at 0 to trace horizontally (ignores floor/ceiling)
        );
        
        // Use player origin (center of body) to detect walls at any height
        // This handles irregular walls better than eye position alone
        Vector startPos = pawnPos + new Vector(0, 0, 32f); // Mid-body height
        Vector targetPos = startPos + forwardDir * 200f;
        
        // Perform ray trace using SimpleTrace
        CGameTrace trace = new();
        Core.Trace.SimpleTrace(
            startPos,                                   // Start from player body center
            targetPos,                                  // End at check distance forward
            RayType_t.RAY_TYPE_LINE,                   // Use line ray for precise collision
            RnQueryObjectSet.All,                      // Consider all objects
            MaskTrace.Solid | MaskTrace.WorldGeometry | MaskTrace.Window, // Only solid walls, not players
            0,                                         // interactExclude
            0,                                         // interactAs
            CollisionGroup.Default,                    // collision group
            ref trace,                                 // Output trace result
            pawn                                       // Filter out the player itself
        );
        
        // Return true only if hit something VERY close using absolute distance
        if (trace.DidHit)
        {
            float actualDistance = trace.Distance;
            return actualDistance < minWallDistance;
        }
        
        return false;
    }

    // Thread-safe spawn and despawn methods for camera entities
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

    private void SafeDespawn(CPointCamera? camera)
    {
        if (camera != null && camera.IsValid)
        {
            Core.Scheduler.NextWorldUpdate(() => camera.Despawn());
        }
    }
}
