using ComplexTweaks.Configuration;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.TypeInspectors;
using YamlDotNet.Serialization.TypeResolvers;

namespace ComplexTweaks.Services;

public sealed class ConfigService : IPluginService {
    private static readonly string ConfigFileName = "ezAutomaton.yaml";

    public Config Config { get; private set; }

    private string ConfigPath => Path.Combine(Svc.Interface.GetPluginConfigDirectory(), ConfigFileName);

    public ConfigService() {
        Config = Load();
        RunMigrations();
    }

    public void Save() {
        try {
            var yaml = Serializer.Serialize(Config);
            File.WriteAllText(ConfigPath, yaml);
        }
        catch (Exception ex) {
            IPluginLog.Get().Error(ex, $"[{nameof(ConfigService)}] Failed to save config");
        }
    }

    private Config Load() {
        try {
            if (!File.Exists(ConfigPath))
                return new Config();

            var yaml = File.ReadAllText(ConfigPath);
            return Deserializer.Deserialize<Config>(yaml) ?? new Config();
        }
        catch (Exception ex) {
            IPluginLog.Get().Error(ex, $"[{nameof(ConfigService)}] Failed to load config, using defaults");
            return new Config();
        }
    }

    private void RunMigrations() {
        IMigration[] migrations = [new V3(), new V4()];
        var migrated = false;
        foreach (var migration in migrations) {
            if (Config.Version >= migration.Version)
                continue;

            IPluginLog.Get().Info($"Migrating from config version {Config.Version} to {migration.Version}");
            var c = Config;
            migration.Migrate(ref c);
            Config = c;
            Config.Version = migration.Version;
            migrated = true;
        }

        if (migrated)
            Save();
    }

    // because I need to use fields and not just props
    private static readonly ISerializer Serializer = new SerializerBuilder().WithTypeInspector(inner => new CompositeTypeInspector(new ReadableFieldsTypeInspector(new StaticTypeResolver()), inner)).Build();
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
}
