using System.Text.Json.Serialization;

namespace MoneyTracker.Api.Common;

public record ApiError(
    [property: JsonPropertyName("error")]
    string Error,
    [property: JsonPropertyName("fields"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Dictionary<string, string>? Fields = null);
