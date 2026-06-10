namespace Bishop.Tests;

[CollectionDefinition("EnvVar", DisableParallelization = true)]
public sealed class EnvVarCollection;

// Serializes all CLI tests that redirect Console.Out / Console.Error.
// Console is process-global state — parallel redirection causes output to land in the wrong writer.
[CollectionDefinition("ConsoleTests", DisableParallelization = true)]
public sealed class ConsoleTestsCollection;
