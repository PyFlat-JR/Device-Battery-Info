using MacroDeck.Sdk.Widgets;

namespace DeviceBatteryInfo.Ui;

internal static class BatteryWidgetTypes
{
    public const string PanelId = "panel";
    public const string TileId = "tile";

    public static bool Matches(string widgetTypeAttribute, string localId) =>
        string.Equals(widgetTypeAttribute, localId, StringComparison.Ordinal)
        || widgetTypeAttribute.EndsWith("::" + localId, StringComparison.Ordinal);

    public static IReadOnlyList<WidgetTypeDescriptor> All =>
        [
            new WidgetTypeDescriptor(
                PanelId,
                Strings.Widgets.Panel.Name(),
                Strings.Widgets.Panel.Description(),
                DefaultData: DefaultData,
                DataSchema: Schema,
                HasConfiguration: true
            ),
            new WidgetTypeDescriptor(
                TileId,
                Strings.Widgets.Tile.Name(),
                Strings.Widgets.Tile.Description(),
                DefaultData: DefaultData,
                DataSchema: Schema,
                HasConfiguration: true
            ),
        ];

    private const string DefaultData =
        """{"sourceIds":[],"showBar":true,"showPercent":true,"showCharging":true,"showTimeToFull":true,"showTrend":true,"lowThreshold":20,"sort":"manual","title":""}""";

    private const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "sourceIds": {
              "type": "array",
              "items": { "type": "string" },
              "description": "Battery source ids to show, in display order. Empty means every device the plugin sees."
            },
            "showBar": { "type": "boolean", "default": true },
            "showPercent": { "type": "boolean", "default": true },
            "showCharging": { "type": "boolean", "default": true },
            "showTimeToFull": { "type": "boolean", "default": true },
            "showTrend": {
              "type": "boolean",
              "default": true,
              "description": "Show the recent charge/drain rate (for example -13%/1h) when there is no time-to-full to show instead."
            },
            "lowThreshold": { "type": "integer", "minimum": 1, "maximum": 99, "default": 20 },
            "sort": {
              "type": "string",
              "enum": ["manual", "lowest-first", "alphabetical", "charging-first"],
              "default": "manual",
              "description": "Row order. 'manual' keeps the order the devices are listed in above."
            },
            "title": {
              "type": "string",
              "default": "",
              "description": "Optional heading shown above the rows. Empty means no heading."
            }
          }
        }
        """;
}
