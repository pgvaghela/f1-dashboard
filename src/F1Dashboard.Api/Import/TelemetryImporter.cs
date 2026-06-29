using System.Diagnostics;
using Npgsql;

namespace F1Dashboard.Api.Import;

/// <summary>
/// Launches the Python FastF1 batch ingestion script (tools/ingest_all_telemetry.py).
/// Intended for local/dev use — the process runs in the background and logs to a file.
/// </summary>
public sealed class TelemetryImporter
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<TelemetryImporter> _logger;

    public TelemetryImporter(
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<TelemetryImporter> logger)
    {
        _env = env;
        _config = config;
        _logger = logger;
    }

    public TelemetryImportStartResult StartBatchImport(string? seasons, bool force)
    {
        var repoRoot = ResolveRepoRoot();
        var script = Path.Combine(repoRoot, "tools", "ingest_all_telemetry.py");
        if (!File.Exists(script))
        {
            throw new FileNotFoundException($"Telemetry ingest script not found at {script}");
        }

        var python = ResolvePython(repoRoot);
        var logPath = Path.Combine(repoRoot, "tools", "telemetry_ingest.log");

        var psi = new ProcessStartInfo
        {
            FileName = python,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.Combine(repoRoot, "tools"),
        };
        psi.ArgumentList.Add(script);
        if (!string.IsNullOrWhiteSpace(seasons))
        {
            psi.ArgumentList.Add("--seasons");
            psi.ArgumentList.Add(seasons);
        }
        if (force)
        {
            psi.ArgumentList.Add("--force");
        }

        ApplyDatabaseEnvironment(psi);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var logStream = new StreamWriter(logPath, append: true) { AutoFlush = true };
        logStream.WriteLine($"[{DateTimeOffset.UtcNow:u}] Starting: {python} {string.Join(' ', psi.ArgumentList)}");

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                logStream.WriteLine(e.Data);
                _logger.LogInformation("telemetry-ingest: {Line}", e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                logStream.WriteLine(e.Data);
                _logger.LogWarning("telemetry-ingest: {Line}", e.Data);
            }
        };
        process.Exited += (_, _) =>
        {
            logStream.WriteLine($"[{DateTimeOffset.UtcNow:u}] Exit code {process.ExitCode}");
            logStream.Dispose();
            _logger.LogInformation("Telemetry ingest finished with exit code {Code}", process.ExitCode);
        };

        if (!process.Start())
        {
            logStream.Dispose();
            throw new InvalidOperationException("Failed to start telemetry ingest process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return new TelemetryImportStartResult(process.Id, logPath, seasons, force);
    }

    private static string ResolvePython(string repoRoot)
    {
        if (OperatingSystem.IsWindows())
        {
            var winVenv = Path.Combine(repoRoot, "tools", ".venv", "Scripts", "python.exe");
            if (File.Exists(winVenv))
            {
                return winVenv;
            }
        }
        else
        {
            var unixVenv = Path.Combine(repoRoot, "tools", ".venv", "bin", "python");
            if (File.Exists(unixVenv))
            {
                return unixVenv;
            }
        }

        return "python3";
    }

    /// <summary>
    /// Gives the Python ingest scripts the same Postgres target as the API.
    /// Render/Neon expose <c>ConnectionStrings__F1Database</c>, not PGHOST/PGUSER.
    /// </summary>
    private void ApplyDatabaseEnvironment(ProcessStartInfo psi)
    {
        var hasPgEnv = new[] { "PGHOST", "PGDATABASE", "PGUSER" }
            .Any(key => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)));

        if (hasPgEnv)
        {
            foreach (var key in new[] { "PGHOST", "PGPORT", "PGDATABASE", "PGUSER", "PGPASSWORD", "PGSSLMODE" })
            {
                var value = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrEmpty(value))
                {
                    psi.Environment[key] = value;
                }
            }

            return;
        }

        var connectionString = _config.GetConnectionString("F1Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("No F1Database connection string found for telemetry ingest.");
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        psi.Environment["PGHOST"] = builder.Host ?? "localhost";
        psi.Environment["PGPORT"] = builder.Port.ToString();
        psi.Environment["PGDATABASE"] = builder.Database ?? "";
        psi.Environment["PGUSER"] = builder.Username ?? "";
        psi.Environment["PGPASSWORD"] = builder.Password ?? "";
        psi.Environment["PGSSLMODE"] = builder.SslMode == SslMode.Disable ? "disable" : "require";

        _logger.LogInformation(
            "Telemetry ingest will use database {Database} on {Host}.",
            builder.Database,
            builder.Host);
    }

    private string ResolveRepoRoot()
    {
        // Docker layout: API + tools both live under /app.
        if (File.Exists(Path.Combine(_env.ContentRootPath, "tools", "ingest_all_telemetry.py")))
        {
            return _env.ContentRootPath;
        }

        // ContentRootPath is src/F1Dashboard.Api when running locally.
        var candidate = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", ".."));
        if (File.Exists(Path.Combine(candidate, "tools", "ingest_all_telemetry.py")))
        {
            return candidate;
        }

        // Fallback: walk up from the binary location.
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            if (File.Exists(Path.Combine(dir, "tools", "ingest_all_telemetry.py")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir) ?? dir;
        }

        return candidate;
    }
}

public sealed record TelemetryImportStartResult(
    int ProcessId,
    string LogFile,
    string? Seasons,
    bool Force);
