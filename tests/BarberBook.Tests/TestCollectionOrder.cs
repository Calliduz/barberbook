using Xunit;

// Disable parallel test execution across test classes to prevent race conditions on shared database fixtures
[assembly: CollectionBehavior(DisableTestParallelization = true)]

