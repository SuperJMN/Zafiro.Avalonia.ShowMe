using Xunit;

// Avalonia controls and Dispatcher are single-threaded; disable parallel test execution to avoid cross-thread access exceptions.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
