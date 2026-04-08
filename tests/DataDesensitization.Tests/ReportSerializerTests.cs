using DataDesensitization.Exceptions;
using DataDesensitization.Models;
using DataDesensitization.Services;

namespace DataDesensitization.Tests;

public class ReportSerializerTests
{
    private readonly ReportSerializer _serializer = new();

    private static ExecutionReport MakeReport() => new()
    {
        StartedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CompletedAt = new DateTime(2026, 1, 1, 0, 0, 5, DateTimeKind.Utc),
        TotalElapsed = TimeSpan.FromSeconds(5),
        TableResults =
        [
            new("dbo.Users", 100, TimeSpan.FromSeconds(3), null),
            new("dbo.Orders", 0, TimeSpan.FromSeconds(2), "Timeout")
        ]
    };

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var json = _serializer.Serialize(MakeReport());

        Assert.Contains("dbo.Users", json);
        Assert.Contains("dbo.Orders", json);
        Assert.Contains("Timeout", json);
    }

    [Fact]
    public void Deserialize_ValidJson_ReturnsReport()
    {
        var json = _serializer.Serialize(MakeReport());

        var report = _serializer.Deserialize(json);

        Assert.Equal(2, report.TableResults.Count);
        Assert.Equal(100, report.TotalRowsUpdated);
        Assert.Equal(1, report.TotalErrors);
    }

    [Fact]
    public void RoundTrip_PreservesData()
    {
        var original = MakeReport();

        var json = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize(json);

        Assert.Equal(original.StartedAt, deserialized.StartedAt);
        Assert.Equal(original.TableResults.Count, deserialized.TableResults.Count);
        Assert.Equal(original.TableResults[0].TableName, deserialized.TableResults[0].TableName);
        Assert.Equal(original.TableResults[0].RowsUpdated, deserialized.TableResults[0].RowsUpdated);
        Assert.Equal(original.TableResults[1].Error, deserialized.TableResults[1].Error);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsReportParsingException()
    {
        Assert.Throws<ReportParsingException>(() => _serializer.Deserialize("not json"));
    }

    [Fact]
    public void Deserialize_NullJson_ThrowsReportParsingException()
    {
        // "null" is valid JSON but deserializes to null
        Assert.Throws<ReportParsingException>(() => _serializer.Deserialize("null"));
    }

    [Fact]
    public void Serialize_EmptyReport_ProducesValidJson()
    {
        var report = new ExecutionReport();

        var json = _serializer.Serialize(report);
        var deserialized = _serializer.Deserialize(json);

        Assert.Empty(deserialized.TableResults);
        Assert.Equal(0, deserialized.TotalRowsUpdated);
    }
}
