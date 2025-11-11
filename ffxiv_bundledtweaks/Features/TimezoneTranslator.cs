using Automaton.Utilities.Extensions;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Globalization;
using System.Text.RegularExpressions;
using TimeZoneNames;

namespace Automaton.Features;

[Tweak]
public class TimezoneTranslator : Tweak
{
    public override string Name => "Timezone Translator";
    public override string Description => "Translates system message timestamps in chat to your time zone";

    // the server times are relative to the server associated with a given language, not whatever you log in to. fun.
    private readonly Dictionary<ClientLanguage, LanguageConfig> _kvp = new()
    {
        { ClientLanguage.Japanese, new LanguageConfig(
            new Func<CultureInfo>(() => {
                var c = (CultureInfo)new CultureInfo("ja-JP").Clone();
                c.DateTimeFormat.FullDateTimePattern = "MMMdd'日''（'ddd'）'HH:mm";
                return c;
            })(),
            "Asia/Tokyo") },
        { ClientLanguage.English, new LanguageConfig(
            new Func<CultureInfo>(() => {
                var c = (CultureInfo)new CultureInfo("en-US").Clone();
                c.DateTimeFormat.FullDateTimePattern = "MMM. dd, yyyy %h:mm tt";
                c.DateTimeFormat.AMDesignator = "a.m.";
                c.DateTimeFormat.PMDesignator = "p.m.";
                return c;
            })(),
            "America/Los_Angeles") },
        { ClientLanguage.German, new LanguageConfig(
            new Func<CultureInfo>(() => {
                var c = (CultureInfo)new CultureInfo("de-DE").Clone();
                c.DateTimeFormat.FullDateTimePattern = "dd. MMM yyyy 'um' HH:mm 'Uhr'";
                return c;
            })(),
            "Europe/Berlin") },
        { ClientLanguage.French, new LanguageConfig(
            new Func<CultureInfo>(() => {
                var c = (CultureInfo)new CultureInfo("en-US").Clone();
                c.DateTimeFormat.FullDateTimePattern = "dd MMMM yyyy 'à' HH'h'mm";
                return c;
            })(),
            "Europe/Paris") },
    };

    public override void Enable() => Svc.Chat.ChatMessage += OnChatMessage;
    public override void Disable() => Svc.Chat.ChatMessage -= OnChatMessage;

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (type is not XivChatType.Notice) return;
        if (message.TextValue.IsNullOrEmpty()) return;

        if (_kvp.TryGetValue(Svc.ClientState.ClientLanguage, out var conf))
        {
            if (conf.Culture.GetFullDateTimeRegexPattern().Match(message.TextValue) is not { Success: true } match) return;

            Log($"Detected timestamp [{match.Value}] in message {message.TextValue}");
            if (DateTime.TryParse(match.Value, conf.Culture, out var serverTime))
            {
                var localTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(serverTime, conf.ServerTimeZone, TimeZoneInfo.Local.Id).ToString(conf.Culture.DateTimeFormat.FullDateTimePattern, conf.Culture);
                var sb = new SeStringBuilder();
                foreach (var item in message.Payloads)
                {
                    if (item is TextPayload tp)
                    {
                        var text = string.Concat(tp.Text.AsSpan(0, match.Index), localTime, tp.Text.AsSpan(match.Index + match.Length));
                        var abbrPattern = $@"\({Regex.Escape(_kvp.FindKeysByValue(conf).First() is ClientLanguage.French ? conf.LongName : conf.Abbreviation)}\)";
                        text = Regex.Replace(text, abbrPattern, $"({LocalTzAbbreviation})", RegexOptions.IgnoreCase);
                        sb.Add(new TextPayload(text));
                    }
                    else
                        sb.Add(item);
                }
                message = sb.Build();
            }
        }
    }

    private string LocalTzAbbreviation
        => TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now)
        ? TZNames.GetAbbreviationsForTimeZone(TimeZoneInfo.Local.Id, CultureInfo.CurrentCulture.Name).Daylight ?? "null"
        : TZNames.GetAbbreviationsForTimeZone(TimeZoneInfo.Local.Id, CultureInfo.CurrentCulture.Name).Standard ?? "null";

    private sealed class LanguageConfig(CultureInfo cultureInfo, string serverTimezone)
    {
        public CultureInfo Culture { get; } = cultureInfo;
        public string ServerTimeZone { get; } = serverTimezone;
        public TimeZoneInfo Id => TimeZoneInfo.FindSystemTimeZoneById(ServerTimeZone);
        public string Abbreviation => TimeZoneInfo.FindSystemTimeZoneById(ServerTimeZone) is var tz
            ? TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now)
                ? TZNames.GetAbbreviationsForTimeZone(tz.Id, Culture.Name).Daylight ?? "null"
                : TZNames.GetAbbreviationsForTimeZone(tz.Id, Culture.Name).Standard ?? "null"
            : "null";

        public string LongName
        {
            get
            {
                if (TZNames.GetNamesForTimeZone(Id.Id, Culture.Name) is { Generic: var gen } && !string.IsNullOrEmpty(gen))
                {
                    if (!gen.StartsWith("heure de ", StringComparison.OrdinalIgnoreCase))
                        return $"heure de {(Id.Id.Contains('/') ? Id.Id.Split('/').Last() : Id.Id)}";
                    return gen;
                }
                return Abbreviation;
            }
        }
    }
}
