using System.Text.RegularExpressions;

namespace HackerNews.Api.Configuration.Conventions;

public partial class KebabCaseParameterTransformer : IOutboundParameterTransformer
{
    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex Regex();

    public string TransformOutbound(object value)
    {
        if (value == null)
            return null;

        return Regex()
            .Replace(value.ToString()!, "$1-$2")
            .ToLowerInvariant();
    }
}