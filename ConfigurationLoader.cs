using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Reflection;
using HandlebarsDotNet;
namespace OAuthServer;

public static class ConfigurationLoader
{
    /// <summary>
    /// Placeholder string for using the default callback Uri
    /// </summary>
    public const string DefaultCallbackUri = "%OAUTH_CALLBACK_URI%";

    /// <summary>
    /// Placeholder string for using the default callback Uri
    /// </summary>
    public const string Hostname = "%HOSTNAME%";

    /// <summary>
    /// The environment key for the application name
    /// </summary>
    private const string AppNameEnvKey = "APPNAME";

    /// <summary>
    /// The environment key for the hostname
    /// </summary>
    private const string HostNameEnvKey = "HOSTNAME";

    /// <summary>
    /// The environment key for the display
    /// </summary>
    private const string DisplayNameEnvKey = "DISPLAYNAME";

    /// <summary>
    /// The environment key for the services
    /// </summary>
    private const string ServicesEnvKey = "SERVICES";

    /// <summary>
    /// The environment key for the secrets file
    /// </summary>
    private const string SecretsFileEnvKey = "SECRETS";

    /// <summary>
    /// The environment key for the passphrase to decrypt the secrets file
    /// </summary>
    private const string SecretsPassphraseKey = "SECRETS_PASSPHRASE";

    /// <summary>
    /// The environment key for the config file
    /// </summary>
    private const string ConfigFileEnvKey = "CONFIGFILE";

    /// <summary>
    /// The environment key for the storage destination
    /// </summary>
    private const string StorageStringEnvKey = "STORAGE";

    /// <summary>
    /// URL for the privacy policy, if not using the default
    /// </summary>
    private const string PrivacyPolicyEnvKey = "PRIVACY_POLICY_URL";

    /// <summary>
    /// The environment key for the Seq logging Url
    /// </summary>
    private const string SeqUrlEnvKey = "SEQ_URL";

    /// <summary>
    /// The environment key for the Seq API key
    /// </summary>
    private const string SeqApiKeyEnvKey = "SEQ_APIKEY";

    /// <summary>
    /// The service configuration resource filename
    /// </summary>
    private const string ConfigResourceFilename = "config.json";

    /// <summary>
    /// The values expected in the secrets file or env
    /// </summary>
    private static readonly ImmutableArray<string> SecretKeysNames = new string[] {
        "GCS_CLIENT_ID",
        "GCS_CLIENT_SECRET",
        "WL_CLIENT_ID",
        "WL_CLIENT_SECRET",
        "MSGRAPH_CLIENT_ID",
        "MSGRAPH_CLIENT_SECRET",
        "BOX_CLIENT_ID",
        "BOX_CLIENT_SECRET",
        "DROPBOX_CLIENT_ID",
        "DROPBOX_CLIENT_SECRET",
        "JOTTA_CLIENT_SECRET",
    }.ToImmutableArray();

    /// <summary>
    /// The deserializer options to use when reading settings
    /// </summary>
    /// <returns></returns>
    private static readonly System.Text.Json.JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Returns the manifest resource stream with the given name, or null if no stream was found
    /// </summary>
    /// <param name="name">The stream to locate</param>
    /// <param name="assembly">The optional assembly</param>
    /// <returns>The stream matching the name, or <c>null</c></returns>
    private static Stream? GetResourceStreamByName(string name, Assembly? assembly = null)
    {
        var resourcename = (assembly ?? typeof(Program).Assembly).GetManifestResourceNames()
            .FirstOrDefault(x =>
                x.Equals(name, StringComparison.OrdinalIgnoreCase)
                || x.EndsWith("." + name, StringComparison.OrdinalIgnoreCase)); // Handle namespace.folder.name

        if (resourcename == null)
            return null;

        return typeof(Program).Assembly.GetManifestResourceStream(resourcename);
    }

    /// <summary>
    /// Deserializes a resource stream
    /// </summary>
    /// <param name="name">The stream to locate</param>
    /// <param name="assembly">The optional assembly</param>
    /// <typeparam name="T">The datatype to deserialize</typeparam>
    /// <returns>The deserialized object</returns>
    private static T DeserializeResourceStream<T>(string name, Assembly? assembly = null)
    {
        using var stream = GetResourceStreamByName(name)
            ?? throw new Exception($"Missing {name} file in assembly");

        return DeserializeStream<T>(stream, name);
    }

    /// <summary>
    /// Reads an embedded resource as a string
    /// </summary>
    /// <param name="name">The stream to locate</param>
    /// <param name="assembly">The optional assembly</param>
    /// <returns>The stream contents</returns>
    private static string GetResourceStreamAsString(string name, Assembly? assembly = null)
    {
        using var stream = new StreamReader(GetResourceStreamByName(name)
            ?? throw new Exception($"Missing {name} file in assembly"));

        return stream.ReadToEnd();
    }

    /// <summary>
    /// Deserializes a file
    /// </summary>
    /// <param name="stream">Path to the file to deserialize</param>
    /// <typeparam name="T">The datatype to deserialize</typeparam>
    /// <returns>The deserialized object</returns>
    private static T DeserializeFile<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return DeserializeStream<T>(stream, path);
    }

    /// <summary>
    /// Deserializes a stream
    /// </summary>
    /// <param name="stream">The stream to deserialize</param>
    /// <param name="name">The name or path to add to the error mesasge</param>
    /// <typeparam name="T">The datatype to deserialize</typeparam>
    /// <returns>The deserialized object</returns>
    private static T DeserializeStream<T>(Stream stream, string name)
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(
            stream,
            DeserializerOptions
        ) ?? throw new Exception($"Failed to deserialize {name}");
    }

    /// <summary>
    /// The field names not settable by the default object
    /// </summary>
    private static readonly HashSet<string> NonDefaultValueFields = new HashSet<string>() {
        nameof(ServiceConfiguration.Id),
        nameof(ServiceConfiguration.ClientId),
        nameof(ServiceConfiguration.ClientSecret)
     };

    /// <summary>
    /// Translates placeholders into actual values
    /// </summary>
    /// <param name="name">The property name</param>
    /// <param name="record">The record to use</param>
    /// <param name="defaults">The defaults to use if there is no value</param>
    /// <param name="translates">A lookup with the strings that should be translated</param>
    /// <returns>The resulting string</returns>
    private static object? Translate(string name, ServiceRecord record, ServiceDefault? defaults, IReadOnlyDictionary<string, string> translates)
    {
        var sourceprop = typeof(ServiceRecord).GetProperty(name) ?? throw new Exception($"The property {name} is not found on type {nameof(ServiceRecord)}");
        var sourceproptype = Nullable.GetUnderlyingType(sourceprop.PropertyType) ?? sourceprop.PropertyType;

        var targetfield = typeof(ServiceConfiguration).GetProperty(name) ?? throw new Exception($"The property {name} is not found on type {nameof(ServiceConfiguration)}");
        var targetproptype = Nullable.GetUnderlyingType(targetfield.PropertyType) ?? targetfield.PropertyType;

        if (sourceproptype != targetproptype)
            throw new Exception($"Field {name} on {nameof(ServiceRecord)} has type {sourceproptype.Name} but the property has type {targetproptype.Name} on {nameof(ServiceConfiguration)}");

        var sourcevalue = sourceprop.GetValue(record);
        if (sourcevalue != null)
            sourcevalue = Convert.ChangeType(sourcevalue, sourceproptype);

        if (!NonDefaultValueFields.Contains(name) && defaults != null && sourcevalue == null)
        {
            var defaultprop = typeof(ServiceDefault).GetProperty(name) ?? throw new Exception($"The property {name} is not found on type {nameof(ServiceDefault)}");
            var defaultproptype = Nullable.GetUnderlyingType(defaultprop.PropertyType) ?? defaultprop.PropertyType;

            if (sourceproptype != defaultproptype)
                throw new Exception($"Field {name} on {nameof(ServiceRecord)} has type {sourceproptype.Name} but the property has type {defaultproptype.Name} on {nameof(ServiceDefault)}");

            var targetvalue = defaultprop.GetValue(defaults);
            if (targetvalue != null)
                sourcevalue = Convert.ChangeType(targetvalue, sourceproptype);
        }

        if (sourcevalue != null && sourcevalue is string svs)
        {
            if (translates.TryGetValue(svs, out var translated) && translated != null)
                sourcevalue = translated;

            sourcevalue = Environment.ExpandEnvironmentVariables((string)sourcevalue);
        }

        if (sourcevalue != null)
            return sourcevalue;

        if (Nullable.GetUnderlyingType(targetfield.PropertyType) != null || !targetfield.PropertyType.IsValueType)
            return null;

        return Activator.CreateInstance(targetproptype);
    }

    /// <summary>
    /// The application configuration from the commandline
    /// </summary>
    /// <param name="Hostname">The hostname to use</param>
    /// <param name="AppName">The name of the application</param>
    /// <param name="DisplayName">The display name of the application</param>
    /// <param name="Services">The services to enable</param>
    /// <param name="Secrets">The secrets file</param>
    /// <param name="SecretsPassphrase">The passphrase to decrypt the secrets file</param>
    /// <param name="ConfigFile">The configuration file</param>
    /// <param name="Storage">The storage destination</param>
    /// <param name="PrivacyPolicy">The URL to the privacy policy</param>
    /// <param name="SeqUrl">The Seq logging URL</param>
    /// <param name="SeqApiKey">The Seq API key</param>
    /// <param name="ListenPorts">The port to listen on</param>
    private sealed record CommandLineArguments(
        string? Hostname,
        string? AppName,
        string? DisplayName,
        string? Services,
        string? Secrets,
        string? SecretsPassphrase,
        string? ConfigFile,
        string? Storage,
        string? PrivacyPolicy,
        string? SeqUrl,
        string? SeqApiKey,
        int? ListenPorts,
        string? ListenUrls,
        string? CertificatePath,
        string? CertificatePassword
    );

    /// <summary>
    /// The commandline binder does not support underscores in the names
    /// </summary>
    private static readonly Dictionary<string, string> EnvNameMap = new()
    {
        { nameof(CommandLineArguments.SecretsPassphrase), SecretsPassphraseKey },
        { nameof(CommandLineArguments.PrivacyPolicy), PrivacyPolicyEnvKey },
        { nameof(CommandLineArguments.SeqUrl), SeqUrlEnvKey },
        { nameof(CommandLineArguments.SeqApiKey), SeqApiKeyEnvKey },
        { nameof(CommandLineArguments.ListenPorts), "ASPNETCORE_HTTP_PORTS" },
        { nameof(CommandLineArguments.ListenUrls), "ASPNETCORE_URLS" },
        { nameof(CommandLineArguments.CertificatePath), "ASPNETCORE_Kestrel__Certificates__Default__Path" },
        { nameof(CommandLineArguments.CertificatePassword), "ASPNETCORE_Kestrel__Certificates__Default__Password" }
    };

    /// <summary>
    /// Creates a command to run the OAuth server
    /// </summary>
    /// <returns></returns>
    private static Command CreateCommand()
    {
        var command = new Command("run", "Starts the OAuth server")
        {
            new Option<string>(["--hostname"], description: "The hostname to listen on", getDefaultValue: () => "localhost"),
            new Option<string>(["--appname"],  description: "The name of the application", getDefaultValue: () => "Duplicati"),
            new Option<string>(["--displayname"],  description: "The display name of the application", getDefaultValue: () => "Duplicati OAuth Handler"),
            new Option<string>(["--services"],  description: "The services to enable"),
            new Option<string>(["--secrets"],  description: "The secrets file"),
            new Option<string>(["--secrets-passphrase"],  description: "The passphrase to decrypt the secrets file"),
            new Option<string>(["--configfile"],  description: "The configuration file"),
            new Option<string>(["--storage"],  description: "The storage destination", getDefaultValue: () => "file://./tokens?pathmapped=true"),
            new Option<string>(["--privacy-policy"],  description: "The URL to the privacy policy"),
            new Option<string>(["--seq-url"],  description: "The Seq logging URL"),
            new Option<string>(["--seq-apikey"],  description: "The Seq API key"),
            new Option<int?>(["--listen-ports"],  description: "The port to listen on", getDefaultValue: () => null),
            new Option<string>(["--listen-urls"],  description: "The URL to listen on", getDefaultValue: () => "http://localhost:8080"),
            new Option<string>(["--certificate-path"],  description: "The path to the certificate file"),
            new Option<string>(["--certificate-password"],  description: "The password for the certificate file")
        };

        command.Handler = CommandHandler.Create<CommandLineArguments>(SetEnvFromCommandlineArgs);

        return command;
    }

    /// <summary>
    /// Sets the environment variables from the command line arguments
    /// </summary>
    /// <param name="args">The parsed arguments
    private static void SetEnvFromCommandlineArgs(CommandLineArguments args)
    {
        if (!string.IsNullOrWhiteSpace(args.Storage))
        {
            if (args.Storage.IndexOf("://") < 0)
                args = args with { Storage = $"file://{args.Storage}?pathmapped=true" };

            if (args.Storage.StartsWith("file://"))
            {
                var parts = args.Storage.Substring("file://".Length).Split('?', 2);
                if (!Path.IsPathRooted(parts[0]))
                    parts[0] = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", parts[0]);
                var path = Path.GetFullPath(parts[0]);
                args = args with { Storage = $"file://{path}?{parts.Skip(1).FirstOrDefault()}" };
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
        }

        args.GetType().GetProperties().ToList().ForEach(x =>
        {
            var value = x.GetValue(args);
            if (value != null)
            {
                var name = EnvNameMap.GetValueOrDefault(x.Name, x.Name).ToUpperInvariant();
                Environment.SetEnvironmentVariable(name, value.ToString());
            }
        });

    }

    /// <summary>
    /// Loads the application settings from the command line arguments
    /// </summary>
    /// <param name="args">The command line arguments</param>
    /// <returns>The application configuration</returns>
    public static ApplicationConfiguration LoadApplicationConfiguration(string[] args)
    {
        var root = new RootCommand
        {
            CreateCommand()
        };

        var res = root.Invoke(args);
        if (res != 0)
            Environment.Exit(res);

        return LoadApplicationConfiguration();
    }

    /// <summary>
    /// Loads the application settings from the embedded settings file
    /// </summary>
    /// <returns>The application configuration</returns>
    private static ApplicationConfiguration LoadApplicationConfiguration()
    {
        // Slightly backwards to set the environment variables from the command line arguments,
        // but this enables us to be be more Docker compatible
        var settings = new ApplicationConfiguration(
            Environment.GetEnvironmentVariable(HostNameEnvKey) ?? string.Empty,
            Environment.GetEnvironmentVariable(AppNameEnvKey) ?? string.Empty,
            Environment.GetEnvironmentVariable(DisplayNameEnvKey) ?? string.Empty,
            Environment.GetEnvironmentVariable(ServicesEnvKey) ?? string.Empty,

            ExpandEnvPath(Environment.GetEnvironmentVariable(SecretsFileEnvKey)) ?? string.Empty,
            Environment.GetEnvironmentVariable(SecretsPassphraseKey) ?? string.Empty,
            ExpandEnvPath(Environment.GetEnvironmentVariable(ConfigFileEnvKey)) ?? string.Empty,
            Environment.GetEnvironmentVariable(StorageStringEnvKey) ?? string.Empty,
            Environment.GetEnvironmentVariable(PrivacyPolicyEnvKey) ?? string.Empty,

            Environment.GetEnvironmentVariable(SeqUrlEnvKey) ?? string.Empty,
            Environment.GetEnvironmentVariable(SeqApiKeyEnvKey) ?? string.Empty
        );

        if (string.IsNullOrWhiteSpace(settings.Hostname))
            throw new InvalidDataException($"Missing the hostname, please set then environment variable {HostNameEnvKey}");
        if (string.IsNullOrWhiteSpace(settings.AppName))
            throw new InvalidDataException($"Missing the appname, please set then environment variable {AppNameEnvKey}");

        if (string.IsNullOrWhiteSpace(settings.DisplayName))
            settings = settings with { DisplayName = $"{settings.AppName} OAuth Handler" };

        return settings;
    }

    /// <summary>
    /// Expands environment variables and expands the path of the <paramref name="setting"/> value
    /// </summary>
    /// <param name="setting">The value to expand to a path</param>
    /// <returns>The expanded path</returns>
    private static string? ExpandEnvPath(string? setting)
        => string.IsNullOrEmpty(setting) || setting.StartsWith("base64:")
                ? setting
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(setting));

    // Internal record for reading the config file; all except id and serviceId are nullable properties
    private sealed record ServiceRecord(
        string Id,
        string ServiceId,
        string? Name,
        string? ClientId,
        string? ClientSecret,
        string? AuthUrl,
        string? LoginUrl,
        string? Scope,
        string? RedirectUri,
        string? ExtraUrl,
        string? ServiceLink,
        string? DeAuthLink,
        string? BrandImage,
        string? Notes,
        bool? Hidden,
        bool? NoStateForTokenRequest,
        bool? NoRedirectUriForRefreshRequest,
        bool? CliToken,
        bool? PreferV2,
        bool? AccessTokenOnly,
        bool? UseHostnameFromCallback,
        string? AdditionalElements
    );

    /// <summary>
    /// Loads the services from the embedded configuration file
    /// </summary>
    /// <param name="domainname">The external domain name</param>
    /// <returns>The services in the system</returns>
    public static IEnumerable<ServiceConfiguration> LoadServices(ApplicationConfiguration configuration)
    {
        // Set up clearing of secrets so we can just look for empty secrets
        var translationvalues = SecretKeysNames.ToDictionary(x => x, x => string.Empty);

        translationvalues[DefaultCallbackUri] = $"https://{configuration.Hostname}/logged-in";
        translationvalues[Hostname] = configuration.Hostname;

        if (!string.IsNullOrWhiteSpace(configuration.SecretsFilePath))
        {
            using Stream secretsData = new MemoryStream();
            string name;
            if (configuration.SecretsFilePath.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
            {
                name = "secrets env data";
                secretsData.Write(Convert.FromBase64String(configuration.SecretsFilePath.Substring("base64:".Length)));
            }
            else
            {
                if (!File.Exists(configuration.SecretsFilePath))
                    throw new InvalidDataException($"Secrets file specified, but not found: {Path.GetFullPath(configuration.SecretsFilePath)}");

                name = configuration.SecretsFilePath;
                using var fs = File.OpenRead(configuration.SecretsFilePath);
                fs.CopyTo(secretsData);
            }

            secretsData.Position = 0;

            var decryptedStream = secretsData;
            using var ms = new MemoryStream();
            if (!string.IsNullOrWhiteSpace(configuration.SecretsPassphrase))
            {
                SharpAESCrypt.SharpAESCrypt.Decrypt(configuration.SecretsPassphrase, secretsData, ms);
                ms.Position = 0;
                decryptedStream = ms;
            }

            var secrets = DeserializeStream<Dictionary<string, string>>(decryptedStream, name);
            foreach (var kp in secrets)
                translationvalues[$"%{kp.Key}%"] = kp.Value;
        }

        var serviceDefaults = DefaultConfigurations.AllServices;
        var serviceRecords = DeserializeResourceStream<IEnumerable<ServiceRecord>>(ConfigResourceFilename);
        if (!string.IsNullOrWhiteSpace(configuration.ConfigFilePath))
        {
            using Stream configData = new MemoryStream();
            string name;
            if (configuration.ConfigFilePath.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
            {
                name = "config env data";
                configData.Write(Convert.FromBase64String(configuration.ConfigFilePath.Substring("base64:".Length)));
            }
            else
            {
                if (!File.Exists(configuration.ConfigFilePath))
                    throw new InvalidDataException($"Config file specified, but not found: {Path.GetFullPath(configuration.ConfigFilePath)}");

                name = configuration.ConfigFilePath;
                using var fs = File.OpenRead(configuration.ConfigFilePath);
                fs.CopyTo(configData);
            }

            serviceRecords = DeserializeStream<IEnumerable<ServiceRecord>>(configData, name)
                .Concat(serviceRecords)
                .DistinctBy(x => x.Id);
        }

        var enabledServiceIds = configuration.EnabledServiceIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return serviceRecords
            .Where(config => enabledServiceIds.Count == 0 || enabledServiceIds.Contains(config.Id ?? string.Empty) || enabledServiceIds.Contains(config.ServiceId ?? string.Empty))
            .Select(config =>
            {
                var defaultValues = serviceDefaults.GetValueOrDefault(config.ServiceId);

                var constructor = typeof(ServiceConfiguration)
                    .GetConstructors()
                    .Where(x => x.GetParameters().Count() > 0)
                    .OrderByDescending(x => x.GetParameters().Count())
                    .FirstOrDefault()
                    ?? throw new Exception($"Missing constructor in type {nameof(ServiceConfiguration)}");

                var fieldnames = constructor.GetParameters().Select(x => x.Name ?? throw new Exception("Unnamed parameter in constructor"));
                var arguments = fieldnames.Select(x => Translate(x, config, defaultValues, translationvalues)).ToArray();
                var result = (ServiceConfiguration)constructor.Invoke(arguments);

                if (string.IsNullOrWhiteSpace(result.Id))
                    throw new Exception("Found entry with empty Id");

                return result;
            })
            .Where(config => !string.IsNullOrWhiteSpace(config.ClientId) && !string.IsNullOrWhiteSpace(config.ClientSecret));
    }

    /// <summary>
    /// Compiles the embedded resource templates and sets up render functions
    /// </summary>
    /// <param name="configuration">The configuration</param>
    /// <returns>The renderers</returns>
    public static TemplateRenderers LoadRenderers(ApplicationConfiguration configuration)
    {
        var indexTemplate = Handlebars.Compile(GetResourceStreamAsString("index.html.handlebars"));
        var loggedInTemplate = Handlebars.Compile(GetResourceStreamAsString("logged-in.html.handlebars"));
        var cliFileTemplate = Handlebars.Compile(GetResourceStreamAsString("cli-token.html.handlebars"));
        var policyTemplate = Handlebars.Compile(GetResourceStreamAsString("privacy-policy.html.handlebars"));
        var revokeTemplate = Handlebars.Compile(GetResourceStreamAsString("revoke.html.handlebars"));
        var revokedTemplate = Handlebars.Compile(GetResourceStreamAsString("revoked.html.handlebars"));

        return new TemplateRenderers(
            data => indexTemplate(new
            {
                RedirectId = data.RedirectId,
                Providers = data.Providers,
                AppName = configuration.AppName,
                LongAppName = configuration.DisplayName,
                MoreThanOne = data.Providers.Count > 1 // Workaround for missing "x > 1" in handlebars
            }),

            data => loggedInTemplate(new
            {
                AppName = configuration.AppName,
                LongAppName = configuration.DisplayName,
                AuthId = data.AuthId,
                Service = data.Service,
                DeAuthLink = data.DeAuthLink ?? string.Empty,
                AdditionalData = data.AdditionalData ?? []
            }),

            data => cliFileTemplate(new
            {
                AppName = configuration.AppName,
                LongAppName = configuration.DisplayName,
                Id = data.Id,
                Service = data.Service,
                FetchToken = data.FetchToken
            }),

            policyTemplate(new
            {
                AppName = configuration.AppName,
                LongAppName = configuration.DisplayName
            }),

            revokeTemplate(new
            {
                AppName = configuration.AppName,
                LongAppName = configuration.DisplayName
            }),

            data => revokedTemplate(new
            {
                AppName = configuration.AppName,
                LongAppName = configuration.DisplayName,
                Result = data.Result
            })
        );
    }
}