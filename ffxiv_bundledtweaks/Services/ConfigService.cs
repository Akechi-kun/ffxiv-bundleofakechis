using ComplexTweaks.Configuration;
using System.IO;
using YamlDotNet.Serialization;

namespace ComplexTweaks.Services;

public sealed class ConfigService : IPluginService {
    public int InitOrder => 0;

    private static readonly string ConfigFileName = "ezAutomaton.yaml";

    public Config Config { get; private set; }

    private string ConfigPath => Path.Combine(Svc.Interface.GetPluginConfigDirectory(), ConfigFileName);

    public ConfigService() {
        Config = Load();
        RunMigrations();
    }

    public void Save() {
        try {
            var yaml = new SerializerBuilder().Build().Serialize(Config);
            File.WriteAllText(ConfigPath, yaml);
        }
        catch (Exception ex) {
            Svc.Log.Error(ex, $"[{nameof(ConfigService)}] Failed to save config");
        }
    }

    private Config Load() {
        try {
            if (!File.Exists(ConfigPath))
                return new Config();

            var yaml = File.ReadAllText(ConfigPath);
            return new DeserializerBuilder()
                       .IgnoreUnmatchedProperties()
                       .Build()
                       .Deserialize<Config>(yaml)
                   ?? new Config();
        }
        catch (Exception ex) {
            Svc.Log.Error(ex, $"[{nameof(ConfigService)}] Failed to load config, using defaults");
            return new Config();
        }
    }

    private void RunMigrations() {
        IMigration[] migrations = [new V3(), new V4()];
        var migrated = false;
        foreach (var migration in migrations) {
            if (Config.Version >= migration.Version)
                continue;

            Svc.Log.Info($"Migrating from config version {Config.Version} to {migration.Version}");
            var c = Config;
            migration.Migrate(ref c);
            Config = c;
            Config.Version = migration.Version;
            migrated = true;
        }

        if (migrated)
            Save();
    }
}
