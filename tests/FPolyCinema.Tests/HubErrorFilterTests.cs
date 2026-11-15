using System.Security.Claims;
using FPolyCinema.Web.Hubs;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FPolyCinema.Tests;

/// <summary>
/// HubErrorFilter: lời từ chối của hub (HubException, ví dụ tab cũ kết nối lại sau khi phiên đã hết hiệu lực) ghi một dòng Warning không kèm stack trace;
/// lỗi khác ghi Error kèm exception; cả hai vẫn được ném lại để client nhận lời từ chối.
/// </summary>
public class HubErrorFilterTests
{
    private sealed class ListLogger : ILogger<HubErrorFilter>
    {
        public readonly List<(LogLevel Level, string Message, Exception? Error)> Entries = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class AnonymousCaller : HubCallerContext
    {
        public override string ConnectionId => "c1";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => new(new ClaimsIdentity());
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static HubInvocationContext Context(string method, params object?[] args) =>
        new(new AnonymousCaller(), new EmptyServices(), new CinemaHub(null!), typeof(CinemaHub).GetMethod(method)!, args);

    [Fact]
    public async Task HubException_LogsOneWarningWithoutStackTrace_AndRethrows()
    {
        var log = new ListLogger();
        var filter = new HubErrorFilter(log);
        var ex = await Assert.ThrowsAsync<HubException>(async () => await filter.InvokeMethodAsync(Context(nameof(CinemaHub.JoinManagerAlerts), 2),
            _ => throw new HubException("Chỉ quản lý của rạp nhận được cảnh báo này.")));
        Assert.Equal("Chỉ quản lý của rạp nhận được cảnh báo này.", ex.Message);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Error);
        Assert.Contains("JoinManagerAlerts(2)", entry.Message);
        Assert.Contains("khách chưa đăng nhập", entry.Message);
    }

    [Fact]
    public async Task OtherException_LogsError_AndRethrows()
    {
        var log = new ListLogger();
        var filter = new HubErrorFilter(log);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await filter.InvokeMethodAsync(Context(nameof(CinemaHub.JoinOps), 1),
            _ => throw new InvalidOperationException("hỏng")));
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Error);
    }

    [Fact]
    public async Task Success_PassesResultThrough_WithoutLogging()
    {
        var log = new ListLogger();
        var result = await new HubErrorFilter(log).InvokeMethodAsync(Context(nameof(CinemaHub.JoinOps), 1), _ => ValueTask.FromResult<object?>("ok"));
        Assert.Equal("ok", result);
        Assert.Empty(log.Entries);
    }
}
