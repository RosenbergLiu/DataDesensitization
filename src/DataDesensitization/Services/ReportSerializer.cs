using System.Text.Json;
using System.Text.Json.Serialization;
using DataDesensitization.Exceptions;
using DataDesensitization.Models;

namespace DataDesensitization.Services;

public class ReportSerializer : IReportSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Serialize(ExecutionReport report)
    {
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public ExecutionReport Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ExecutionReport>(json, JsonOptions)
                ?? throw new ReportParsingException("Deserialization returned null.");
        }
        catch (JsonException ex)
        {
            throw new ReportParsingException($"Failed to parse execution report JSON: {ex.Message}", ex);
        }
    }

    public async Task ExportToFileAsync(ExecutionReport report, string filePath)
    {
        var json = Serialize(report);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<ExecutionReport> ImportFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return Deserialize(json);
    }
}
