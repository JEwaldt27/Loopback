namespace Server.Services;

/// <summary>
/// Resolves where the app's *mutable* data files live (the shared device library and the
/// user account store).
///
/// By default that's the content root — the app's own directory — which is how the
/// systemd/scp deploy has always worked and stays unchanged.
///
/// Containers need the data somewhere else: a Docker volume mounted over the app
/// directory would hide the application itself, so the image sets LINEFLOW_DATA_DIR=/data
/// and mounts the volume there instead. Set that variable (env var or appsettings
/// "DataDir") to relocate the data anywhere; leave it unset for the original behavior.
///
/// On first run against an empty data directory, the device library shipped with the
/// build is copied in as a seed, so a fresh container starts with the stock device list
/// rather than an empty palette. users.json is deliberately NOT seeded — a new install
/// should land on the first-run "create admin account" screen.
/// </summary>
public class DataPaths
{
    public string Root { get; }
    public string DevicesFile => Path.Combine(Root, "devices.json");
    public string UsersFile => Path.Combine(Root, "users.json");

    public DataPaths(IWebHostEnvironment env, IConfiguration config, ILogger<DataPaths> logger)
    {
        var configured = config["LINEFLOW_DATA_DIR"] ?? config["DataDir"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            Root = env.ContentRootPath;
            return;
        }

        Root = Path.GetFullPath(configured);
        Directory.CreateDirectory(Root);

        // Seed the device library from the copy published alongside the binaries. Note this
        // uses AppContext.BaseDirectory (where Server.dll actually sits), NOT ContentRootPath
        // — the latter follows the working directory, so it silently finds nothing when the
        // app is launched from elsewhere, leaving a new install with an empty palette.
        var seed = Path.Combine(AppContext.BaseDirectory, "devices.json");
        if (!File.Exists(DevicesFile) && File.Exists(seed))
        {
            File.Copy(seed, DevicesFile);
            logger.LogInformation("Seeded device library at {Path} from the built-in copy.", DevicesFile);
        }

        logger.LogInformation("Using data directory {Root}", Root);
    }
}
