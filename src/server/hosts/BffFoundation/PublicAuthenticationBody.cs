using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace IgaMigration.BffFoundation;

internal static class PublicAuthenticationBody
{
    private const int MaximumBytes = 8192;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static async Task<string?> SignInTokenAsync(HttpRequest request)
    {
        if (!ValidType(request, "application/x-www-form-urlencoded")) return null;
        var body = await ReadAsync(request);
        if (body is null) return null;
        try
        {
            var text = StrictUtf8.GetString(body);
            var fields = text.Split('&');
            if (fields.Length != 1) return null;
            var equals = fields[0].IndexOf('=');
            if (equals < 0 || Decode(fields[0][..equals]) != "__RequestVerificationToken") return null;
            var token = Decode(fields[0][(equals + 1)..]);
            return token.Length is > 0 and <= 4096 ? token : null;
        }
        catch (Exception exception) when (exception is DecoderFallbackException or FormatException)
        {
            return null;
        }
    }

    internal static async Task<bool> EmptyJsonAsync(HttpRequest request)
    {
        if (!ValidType(request, "application/json")) return false;
        var body = await ReadAsync(request);
        if (body is null) return false;
        try
        {
            _ = StrictUtf8.GetString(body);
            using var json = JsonDocument.Parse(body);
            return json.RootElement.ValueKind == JsonValueKind.Object && !json.RootElement.EnumerateObject().Any();
        }
        catch (Exception exception) when (exception is DecoderFallbackException or JsonException)
        {
            return false;
        }
    }

    private static bool ValidType(HttpRequest request, string expected)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var media) ||
            !string.Equals(media.MediaType.Value, expected, StringComparison.OrdinalIgnoreCase) ||
            media.Parameters.Any(parameter => !string.Equals(parameter.Name.Value, "charset", StringComparison.OrdinalIgnoreCase)) ||
            media.Parameters.Count > 1) return false;
        return media.Parameters.Count == 0 ||
            string.Equals(HeaderUtilities.RemoveQuotes(media.Charset).Value, "utf-8", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<byte[]?> ReadAsync(HttpRequest request)
    {
        if (request.ContentLength > MaximumBytes) return null;
        var buffer = new byte[MaximumBytes + 1];
        var total = 0;
        try
        {
            while (total < buffer.Length)
            {
                var count = await request.Body.ReadAsync(buffer.AsMemory(total), request.HttpContext.RequestAborted);
                if (count == 0) return buffer[..total];
                total += count;
            }
            return null;
        }
        catch (Exception exception) when (exception is IOException or BadHttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    private static string Decode(string text)
    {
        using var bytes = new MemoryStream();
        for (var index = 0; index < text.Length;)
        {
            if (text[index] == '%')
            {
                if (index + 2 >= text.Length || !Uri.IsHexDigit(text[index + 1]) || !Uri.IsHexDigit(text[index + 2]) ||
                    !byte.TryParse(text.AsSpan(index + 1, 2),
                        System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
                    throw new FormatException("Invalid form encoding.");
                bytes.WriteByte(value);
                index += 3;
            }
            else if (text[index] == '+')
            {
                bytes.WriteByte((byte)' ');
                index++;
            }
            else
            {
                var end = index + 1;
                while (end < text.Length && text[end] is not ('%' or '+')) end++;
                bytes.Write(StrictUtf8.GetBytes(text[index..end]));
                index = end;
            }
        }
        return StrictUtf8.GetString(bytes.ToArray());
    }
}
