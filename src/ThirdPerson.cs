using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace ThirdPerson;

public partial class ThirdPerson : BasePlugin {
  // Configuration loaded from config.jsonc
  private ThirdPersonConfig Config = new();

  // Command registration ID for cleanup
  private Guid? _customCommandGuid = null;

  // Camera entity pools - thread-safe collections for managing active cameras
  // Using CHandle for safe long-term entity references
  private readonly ConcurrentDictionary<int, CHandle<CPointCamera>> _thirdPersonPool = new();
  private readonly ConcurrentDictionary<int, CHandle<CPointCamera>> _smoothThirdPersonPool = new();

  // Knife warning counter per player (limit to 3 warnings per player)
  private readonly ConcurrentDictionary<ulong, int> _knifeWarningCount = new();

  public ThirdPerson(ISwiftlyCore core) : base(core)
  {
  }

  // Plugin initialization - called when plugin loads or hot-reloads.
  // Sets up configuration, dependency injection, commands, and event listeners.
  public override void Load(bool hotReload) {
    // Create convar for enabling/disabling the plugin
    Core.ConVar.CreateOrFind<bool>("thirdperson_enabled", "Enable/Disable ThirdPerson Plugin", true, SwiftlyS2.Shared.Convars.ConvarFlags.NONE);

    // Initialize configuration
    Core.Configuration
        .InitializeWithTemplate("config.jsonc", "config.template.jsonc")
        .Configure(builder => {
            builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true);
        });

    // Setup dependency injection
    ServiceCollection services = new();
    services.AddSwiftly(Core)
        .AddOptionsWithValidateOnStart<ThirdPersonConfig>()
        .BindConfiguration("ThirdPerson");

    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<ThirdPersonConfig>>();
    
    // Initial load
    Config = options.CurrentValue;

    // Hot Reload
    options.OnChange(newConfig => {
        string oldCommand = Config.CustomTPCommand;
        string oldPermission = Config.UseTpPermission;
        Config = newConfig;
        Console.WriteLine($"[ThirdPerson] Configuration updated.");
        if (oldCommand != newConfig.CustomTPCommand || oldPermission != newConfig.UseTpPermission) {
            RegisterCustomCommand();
        }
    });

    // Register custom command alias
    RegisterCustomCommand();

    // Register OnTick listener for camera updates
    Core.Event.OnTick += OnTick;

    // Register OnEntityTakeDamage listener
    Core.Event.OnEntityTakeDamage += OnEntityTakeDamage;

    // Register OnMapUnload listener
    Core.Event.OnMapUnload += OnMapUnload;
  }

  // Plugin cleanup - called when plugin unloads.
  // Removes all cameras, unregisters commands and event listeners.
  public override void Unload() {
    // Unregister tick listener first to stop all ongoing updates
    Core.Event.OnTick -= OnTick;

    // Unregister other event listeners
    Core.Event.OnEntityTakeDamage -= OnEntityTakeDamage;
    Core.Event.OnMapUnload -= OnMapUnload;

    // Cleanup all cameras immediately to prevent memory corruption
    foreach (var kvp in _thirdPersonPool)
    {
      if (kvp.Value.IsValid && kvp.Value.Value != null)
      {
        kvp.Value.Value.Despawn();
      }
    }
    foreach (var kvp in _smoothThirdPersonPool)
    {
      if (kvp.Value.IsValid && kvp.Value.Value != null)
      {
        kvp.Value.Value.Despawn();
      }
    }

    _thirdPersonPool.Clear();
    _smoothThirdPersonPool.Clear();

    // Unregister custom command last
    if (_customCommandGuid.HasValue)
    {
        Core.Command.UnregisterCommand(_customCommandGuid.Value);
        _customCommandGuid = null;
    }
  }

  // Game tick handler - called every server frame.
  // Updates camera positions for all players in third-person mode.
  private void OnTick()
  {
    bool pluginEnabled = Core.ConVar.Find<bool>("thirdperson_enabled")?.Value == true;
    
    // If plugin is disabled but there are active cameras, clean them up
    if (!pluginEnabled && (_thirdPersonPool.Count > 0 || _smoothThirdPersonPool.Count > 0))
    {
      CleanupAllCameras();
      return;
    }
    
    if (!pluginEnabled) return;

    // Update smooth cameras
    foreach (var kvp in _smoothThirdPersonPool)
    {
      var player = Core.PlayerManager.GetPlayer(kvp.Key); // kvp.Key is now player index
      var cameraHandle = kvp.Value;

      // Validate handle and entity before accessing
      if (player == null || !player.IsValid || !cameraHandle.IsValid || cameraHandle.Value == null)
      {
        SafeDespawn(cameraHandle);
        _smoothThirdPersonPool.TryRemove(kvp.Key, out _);
        continue;
      }

      UpdateCameraSmooth(cameraHandle.Value, player, Core, Config.ThirdPersonDistance, Config.ThirdPersonHeight, Config.SmoothCameraSpeed);
    }

    // Update default cameras
    foreach (var kvp in _thirdPersonPool)
    {
      var player = Core.PlayerManager.GetPlayer(kvp.Key); // kvp.Key is now player index
      var cameraHandle = kvp.Value;

      // Validate handle and entity before accessing
      if (player == null || !player.IsValid || !cameraHandle.IsValid || cameraHandle.Value == null)
      {
        SafeDespawn(cameraHandle);
        _thirdPersonPool.TryRemove(kvp.Key, out _);
        continue;
      }

      UpdateCamera(cameraHandle.Value, player, Core, Config.ThirdPersonDistance, Config.ThirdPersonHeight);
    }
  }

  // Cleanup camera entities for a specific player.
  // Removes both regular and smooth third-person cameras and resets view.
  // Called when player disconnects or map ends.
  private void CleanupPlayer(IPlayer player)
  {
    if (player == null)
      return;

    int playerIndex = player.PlayerID;

    // Remove smooth camera using scheduler for safety
    if (_smoothThirdPersonPool.TryRemove(playerIndex, out var smoothCameraHandle))
    {
      if (smoothCameraHandle.IsValid && smoothCameraHandle.Value != null)
      {
        Core.Scheduler.NextWorldUpdate(() =>
        {
          // Revalidate before despawning
          if (smoothCameraHandle.IsValid && smoothCameraHandle.Value != null)
          {
            smoothCameraHandle.Value.Despawn();
          }
        });
      }
    }

    // Remove default camera using scheduler for safety
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

    // Reset camera services to first person
    if (player.Pawn?.CameraServices != null)
    {
      player.Pawn.CameraServices.ViewEntity.Raw = uint.MaxValue;
    }

    // Reset knife warning counter for this player
    _knifeWarningCount.TryRemove(player.SteamID, out _);
  }
} 
 