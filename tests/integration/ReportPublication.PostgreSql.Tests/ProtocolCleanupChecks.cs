using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Npgsql;
using ReportPublication.PostgreSql;

// Synthetic loopback protocol only. It is not PostgreSQL or physical-backend release evidence.
internal static class ProtocolCleanupChecks
{
    internal sealed record Observation(bool StartupCompleted, bool QueryReceived, bool CancelRequestReceived,
        bool LogicalWaitCanceledWhileCancelResponseHeld, bool DriverPendingWhileCancelResponseHeld,
        bool PublicDisposePendingWhilePeerHeld, bool TestOwnedPeersReleased, bool ObservedDriverTasksEnded,
        string? DriverFailureType, string? DisposalFailureType);
    internal static async Task<Observation> RunAsync()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = watchdog.Token;
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        TcpClient? primary = null, cancel = null; Task<int>? driver = null; Task? disposal = null;
        NpgsqlConnection? connection = null;
        var query = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gotCancel = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queryServer = Task.Run(async () =>
        {
            primary = await listener.AcceptTcpClientAsync(ct); var stream = primary.GetStream();
            var length = new byte[4]; await stream.ReadExactlyAsync(length, ct);
            var startup = new byte[BinaryPrimitives.ReadInt32BigEndian(length) - 4]; await stream.ReadExactlyAsync(startup, ct);
            Require(BinaryPrimitives.ReadInt32BigEndian(startup) == 196608);
            await Frame(stream, 'R', Int(0), ct);
            foreach (var pair in new[] { ("server_version", "18.4"), ("client_encoding", "UTF8"), ("integer_datetimes", "on"), ("standard_conforming_strings", "on"), ("TimeZone", "UTC") })
                await Frame(stream, 'S', Encoding.UTF8.GetBytes(pair.Item1 + "\0" + pair.Item2 + "\0"), ct);
            await Frame(stream, 'K', Int(42001).Concat(Int(42002)).ToArray(), ct); await Frame(stream, 'Z', [(byte)'I'], ct);
            await stream.FlushAsync(ct);
            // Withhold every extended query response. The client cancellation has its own connection.
            var type = new byte[1]; await stream.ReadExactlyAsync(type, ct); Require(type[0] is (byte)'P' or (byte)'Q'); query.TrySetResult();
            await released.Task.WaitAsync(ct);
        }, ct);
        var cancelServer = Task.Run(async () =>
        {
            await query.Task.WaitAsync(ct); cancel = await listener.AcceptTcpClientAsync(ct);
            var request = new byte[16]; await cancel.GetStream().ReadExactlyAsync(request, ct);
            Require(BinaryPrimitives.ReadInt32BigEndian(request) == 16 && BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4)) == 80877102);
            Require(BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(8)) == 42001 && BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(12)) == 42002);
            gotCancel.TrySetResult(); await released.Task.WaitAsync(ct);
        }, ct);
        try
        {
            var options = new NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1",
                Port = ((IPEndPoint)listener.LocalEndpoint).Port,
                Username = "fictional_protocol_only",
                Database = "iga_synthetic_protocol_only",
                Pooling = false,
                Multiplexing = false,
                Enlist = false,
                CancellationTimeout = -1,
                SslMode = SslMode.Disable,
                GssEncryptionMode = GssEncryptionMode.Disable,
                Timeout = 3,
                CommandTimeout = 0,
                IncludeErrorDetail = false
            };
            var builder = new NpgsqlSlimDataSourceBuilder(options.ConnectionString);
            builder.ConfigureTypeLoading(x => x.EnableTypeLoading(false));
            await using var dataSource = builder.Build(); connection = await dataSource.OpenConnectionAsync(ct);
            var clock = new FixtureClock(new DateTimeOffset(2026, 10, 4, 1, 0, 0, TimeSpan.Zero));
            using var lease = new OriginalValidityLeaseV1(clock, clock.GetUtcNow() + TimeSpan.FromSeconds(2), default);
            using var command = new NpgsqlCommand("SELECT 1", connection);
            var logical = lease.InvokeAsync(token =>
            {
                driver = command.ExecuteNonQueryAsync(token); return new ValueTask<int>(driver);
            }, default).AsTask();
            await query.Task.WaitAsync(ct); clock.Advance(TimeSpan.FromSeconds(2));
            await gotCancel.Task.WaitAsync(ct);
            var logicalCanceled = false;
            try { _ = await logical.WaitAsync(TimeSpan.FromSeconds(2), ct); }
            catch (OperationCanceledException) { logicalCanceled = true; }
            Require(logicalCanceled && driver is not null && !driver.IsCompleted);
            // Public disposal may do synchronous work before producing its ValueTask; run the test observation on an owned worker.
            disposal = Task.Run(async () => await connection.DisposeAsync(), ct);
            var disposePending = await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromMilliseconds(200), ct)) != disposal;
            Require(disposePending);
            released.TrySetResult(); cancel?.Dispose(); primary?.Dispose(); listener.Stop();
            await queryServer.WaitAsync(ct); await cancelServer.WaitAsync(ct);
            string? driverFailure = null, disposalFailure = null;
            try { _ = await driver!.WaitAsync(ct); } catch (Exception ex) { driverFailure = ex.GetType().Name; }
            try { await disposal.WaitAsync(ct); } catch (Exception ex) { disposalFailure = ex.GetType().Name; }
            connection = null;
            Console.WriteLine("PASS: synthetic cancel-response stall: original logical wait ended; driver/public disposal remained pending until owned protocol peers released. No PostgreSQL backend proof.");
            return new(true, true, true, logicalCanceled, true, disposePending, true, driver!.IsCompleted && disposal.IsCompleted, driverFailure, disposalFailure);
        }
        finally
        {
            released.TrySetResult(); cancel?.Dispose(); primary?.Dispose(); listener.Stop();
            if (connection is not null)
            {
                try { await Task.Run(async () => await connection.DisposeAsync()).WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) { }
            }
            try { await queryServer.WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) { }
            try { await cancelServer.WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) { }
        }
    }
    private static byte[] Int(int value) { var bytes = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(bytes, value); return bytes; }
    private static async Task Frame(NetworkStream stream, char type, byte[] body, CancellationToken ct)
    {
        await stream.WriteAsync(new[] { (byte)type }, ct); await stream.WriteAsync(Int(body.Length + 4), ct); await stream.WriteAsync(body, ct);
    }
    private static void Require(bool value) { if (!value) throw new InvalidOperationException("Synthetic protocol cleanup assertion failed."); }
}
