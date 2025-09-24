# Octane Tag Writing Test

A .NET 8 suite for exercising RFID tag writing workflows with the Impinj Octane SDK. The solution implements multiple strategies to measure performance, robustness and verification behavior while keeping infrastructure details outside of source control.

## Enterprise publication readiness

Follow these practices before mirroring the repository into an enterprise GitHub organization:

- **Configuration template only.** The repository ships `config.example.json` with placeholder hostnames and product identifiers. Copy it to `config.json` locally and populate environment-specific values without committing them. The `.gitignore` file already excludes `config.json`, `reader_settings.json` and the runtime `reader_settings/` directory.
- **Fictional test data.** EPC/TID lists and the constants in `TagUtils.Tests` use clearly fictional identifiers annotated in comments. Regenerate additional samples with the same conventions if more coverage is needed.
- **Neutral documentation.** Use placeholders such as `detector.example.com` (already reflected in CLI and Docker snippets) instead of real IPs or hostnames when describing deployments.
- **Corporate licensing.** Replace the root `LICENSE` contents with the organization-approved text before publishing internally.
- **Automation alignment.** Review `.github/workflows/dotnet.yml` and adapt triggers, runners or quality gates to match corporate CI requirements if they differ from the provided defaults.

## Project structure

```
OctaneTagWritingTest/
├── ApplicationConfig.cs          # Strongly-typed application configuration
├── Helpers/                      # EPC list utilities, tag operations, logging helpers
├── Infrastructure/               # Dependency registration and plumbing
├── JobStrategies/                # Strategy implementations (JobStrategy0..9)
├── Properties/                   # Launch settings and assembly metadata
├── ReaderSettings*.cs            # Reader configuration management
├── Program.cs                    # Application entry point
├── epc_list.txt / tid_list.txt   # Fictional sample identifiers for testing only
└── config.example.json           # Placeholder configuration template
```

## Configuration management

1. Copy `config.example.json` to `config.json` in the same directory.
2. Fill in reader hostnames, GTIN/SKU information and any authentication secrets locally.
3. Keep the populated file out of version control (it is ignored by default).
4. Runtime adjustments to individual readers are persisted in the `reader_settings/` directory, which is also ignored.

If the enterprise environment mandates a secrets manager or environment variables, document the alternative bootstrap process here before publishing.

## Sample data

- `epc_list.txt` and `tid_list.txt` contain synthetic values prefixed or patterned to make their fictional nature obvious.
- Unit tests under `TagUtils.Tests` declare fictional GTIN/TID examples with inline comments to prevent confusion with production identifiers.
- Update accompanying comments if you introduce new samples so downstream teams understand they are non-production.

## Usage

```bash
# Run with a sanitized configuration file
OctaneTagWritingTest.exe --config config.json

# Start interactive configuration mode
OctaneTagWritingTest.exe --interactive
```

CLI parameters allow overriding individual settings (see `--help` output). Strategies can be launched individually through the interactive menu.

## Docker

```bash
# Build a production image
docker build -t octane-tag-writing-test .

# Run with placeholder hostnames (replace locally)
docker run octane-tag-writing-test detector.example.com writer.example.com verifier.example.com
```

Avoid embedding customer network information in documentation or scripts that remain under version control.

## Logging and outputs

Each strategy produces structured CSV logs that capture EPC transitions, verification results and timing metrics. Logs are written beneath the working directory in timestamped folders for traceability.

## License

Update the root `LICENSE` file with the company-approved terms prior to internal publication.
