# C# 14 language evidence

All projects inherit `<LangVersion>14.0</LangVersion>` from the root `Directory.Build.props`. The .NET 10 SDK is required to compile the solution with that language version.

The demo initializer uses a C# 14 extension property to express the complete-empty-snapshot guard:

- `src/ApexPost.Infrastructure/Persistence/DemoDataInitializer.cs` — `ApexPostSnapshotExtensions` declares `IsCompletelyEmpty` in an `extension(ApexPostSnapshot snapshot)` block.
- The same member is consumed by `DemoDataInitializer.InitializeAsync`; it is not an illustrative-only snippet.

The extension property keeps the idempotency guard readable at its call site while checking every persisted snapshot collection, including rates and transaction history.
