using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitwardenStreamdeckPlugin.Tests;

/// <summary>
/// The Stream Deck payload types only have private setters, so tests build them the same
/// way the library does: by deserializing JSON. InitialPayload additionally requires an
/// AppearancePayload, which is composed here rather than mapped from a flat object.
/// </summary>
internal static class TestPayloads
{
    internal static InitialPayload Initial(object? settings = null)
    {
        string settingsJson = settings == null ? "{}" : JsonConvert.SerializeObject(settings);

        string appearanceJson = $$"""
        {
          "settings": {{settingsJson}},
          "coordinates": { "column": 0, "row": 0 },
          "state": 0,
          "isInMultiAction": false,
          "controller": "Keypad"
        }
        """;

        AppearancePayload appearance = JsonConvert.DeserializeObject<AppearancePayload>(appearanceJson)!;

        return new InitialPayload(appearance, null!);
    }

    /// <summary>
    /// An initial payload with no settings at all, which is what the Stream Deck sends the
    /// first time an action is dropped onto a key.
    /// </summary>
    internal static InitialPayload Empty()
    {
        return Initial();
    }

    internal static ReceivedSettingsPayload Received(object settings)
    {
        string json = $$"""
        {
          "settings": {{JsonConvert.SerializeObject(settings)}},
          "coordinates": { "column": 0, "row": 0 },
          "isInMultiAction": false
        }
        """;

        return JsonConvert.DeserializeObject<ReceivedSettingsPayload>(json)!;
    }

    internal static JObject Settings(object settings)
    {
        return JObject.FromObject(settings);
    }
}
