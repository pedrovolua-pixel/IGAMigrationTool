using SyntheticTaskCsv;

try
{
    using var input = Console.OpenStandardInput();
    using var buffer = new MemoryStream();
    var chunk = new byte[8192];
    while (true)
    {
        var count = await input.ReadAsync(chunk);
        if (count == 0) break;
        if (buffer.Length + count > CsvCodec.MaximumInputBytes) return 2;
        buffer.Write(chunk, 0, count);
    }
    var json = new System.Text.UTF8Encoding(false, true).GetString(buffer.ToArray());
    var parsed = CsvCodec.Parse(json);
    if (!parsed.Succeeded) return 2;
    var bytes = CsvCodec.Render(parsed.Envelope!);
    using var output = Console.OpenStandardOutput();
    await output.WriteAsync(bytes);
    await output.FlushAsync();
    return 0;
}
catch (Exception exception) when (exception is not OutOfMemoryException)
{ return 2; }
