using Avalonia.Headless;
using Xunit;
using Zafiro.Avalonia.ShowMe.Tests;

// Avalonia controls and Dispatcher are single-threaded; disable parallel test execution to avoid cross-thread access exceptions.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Single headless Avalonia session for the whole assembly. Tests that need Avalonia use [AvaloniaFact] and run on its UI thread.
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
