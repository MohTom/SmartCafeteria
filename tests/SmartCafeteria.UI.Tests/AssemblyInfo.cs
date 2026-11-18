using Xunit;

// FlaUI tests launch real app instances against a shared database and window
// manager; running test classes in parallel causes them to interfere. Run serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
