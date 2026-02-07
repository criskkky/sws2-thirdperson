using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace ThirdPerson;

public partial class ThirdPerson
{
    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundStart(EventRoundStart @event)
    {
        if (Core.ConVar.Find<bool>("thirdperson_enabled")?.Value != true) return HookResult.Continue;

        // Get all currently connected players
        var connectedPlayers = Core.PlayerManager.GetAllPlayers();
        var connectedPlayerIds = new HashSet<int>(connectedPlayers.Select(p => p.PlayerID));

        // Cleanup cameras only for disconnected players
        var disconnectedKeys = _thirdPersonPool.Keys.Where(id => !connectedPlayerIds.Contains(id)).ToList();
        foreach (var playerId in disconnectedKeys)
        {
            if (_thirdPersonPool.TryRemove(playerId, out var camera))
            {
                camera.Despawn();
            }
        }

        var disconnectedSmoothKeys = _smoothThirdPersonPool.Keys.Where(id => !connectedPlayerIds.Contains(id)).ToList();
        foreach (var playerId in disconnectedSmoothKeys)
        {
            if (_smoothThirdPersonPool.TryRemove(playerId, out var camera))
            {
                camera.Despawn();
            }
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        if (Core.ConVar.Find<bool>("thirdperson_enabled")?.Value != true) return HookResult.Continue;

        // Get the victim player
        var victim = @event.Accessor.GetPlayer("userid");
        
        if (victim != null)
        {
            CleanupPlayer(victim);
        }

        return HookResult.Continue;
    }

    private void OnMapUnload(IOnMapUnloadEvent @event)
    {
        // Cleanup all cameras when map unloads (map change destroys all entities)
        foreach (var camera in _thirdPersonPool.Values)
        {
            if (camera.IsValid)
            {
                camera.Despawn();
            }
        }
        foreach (var camera in _smoothThirdPersonPool.Values)
        {
            if (camera.IsValid)
            {
                camera.Despawn();
            }
        }

        _thirdPersonPool.Clear();
        _smoothThirdPersonPool.Clear();
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event)
    {
        if (Core.ConVar.Find<bool>("thirdperson_enabled")?.Value != true) return HookResult.Continue;

        // Get the disconnected player directly from the event
        var player = @event.UserIdPlayer;
        
        if (player != null)
        {
            CleanupPlayer(player);
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnWeaponFire(EventWeaponFire @event)
    {
        if (Core.ConVar.Find<bool>("thirdperson_enabled")?.Value != true) return HookResult.Continue;

        // Check if the weapon fired is a knife
        if (!@event.Weapon.Contains("knife", StringComparison.OrdinalIgnoreCase))
            return HookResult.Continue;

        // Get the player who fired
        var player = @event.UserIdPlayer;
        if (player == null)
            return HookResult.Continue;

        // Check if player is in third person
        bool isInThirdPerson = _thirdPersonPool.ContainsKey(player.PlayerID) || 
                              _smoothThirdPersonPool.ContainsKey(player.PlayerID);
        
        if (!isInThirdPerson)
            return HookResult.Continue;

        // Check if knife warnings are enabled
        if (!Config.EnableKnifeWarnings)
            return HookResult.Continue;

        // Check knife warning limit (max 3 warnings per player)
        int currentWarnings = _knifeWarningCount.GetOrAdd(player.SteamID, 0);
        if (currentWarnings >= 3)
            return HookResult.Continue;

        // Send warning message about knife attacks in third person
        player.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.knife_warning"]}");

        // Increment warning counter
        _knifeWarningCount[player.SteamID] = currentWarnings + 1;

        return HookResult.Continue;
    }

    private void OnEntityTakeDamage(IOnEntityTakeDamageEvent @event)
    {
        if (Core.ConVar.Find<bool>("thirdperson_enabled")?.Value != true) return;

        // Get victim pawn
        var victimPawn = @event.Entity.As<CCSPlayerPawn>();
        if (victimPawn == null) return;

        if (victimPawn.Controller.Value == null) return;
        var victim = victimPawn.Controller.Value.As<CCSPlayerController>();
        if (victim == null) return;

        // Get attacker entity
        var attackerEntity = @event.Info.Attacker.Value;
        if (attackerEntity == null) return;

        var attackerPawn = attackerEntity.As<CCSPlayerPawn>();
        if (attackerPawn == null) return;

        if (attackerPawn.Controller.Value == null) return;
        var attacker = attackerPawn.Controller.Value.As<CCSPlayerController>();
        if (attacker == null) return;

        // Get players
        var victimPlayer = Core.PlayerManager.GetPlayer((int)victimPawn.Controller.EntityIndex - 1);
        var attackerPlayer = Core.PlayerManager.GetPlayer((int)attackerPawn.Controller.EntityIndex - 1);
        if (victimPlayer == null || attackerPlayer == null) return;

        // Check if attacker is using third person
        bool attackerInThirdPerson = _thirdPersonPool.ContainsKey(attackerPlayer.PlayerID) || 
                                      _smoothThirdPersonPool.ContainsKey(attackerPlayer.PlayerID);

        if (attackerInThirdPerson)
        {
            if (Config.DamageMode == "none")
            {
                // Completely disable damage in third person and send message
                attackerPlayer.SendChat($"{Helper.ChatColors.Red}{Core.Localizer["tp.prefix"]}{Helper.ChatColors.Default} {Core.Localizer["tp.damage_disabled"]}");
                @event.Result = HookResult.Stop;
            }
            else if (Config.DamageMode == "back")
            {
                // Check if victim is in front of attacker
                bool isInfront = IsInfrontOfPlayer(attackerPlayer, victimPlayer);
                
                if (isInfront)
                {
                    // Cancel damage from behind when in third person
                    @event.Result = HookResult.Stop;
                }
            }
        }
    }
}

