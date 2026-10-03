# Contributing

Issues and pull requests are welcome, especially:

- fixes to the SDK, docs or examples;
- clients for other languages (Java and Node are the most requested);
- places where the methodology is unclear.

Before you open a pull request:

1. `cd sdk/dotnet && dotnet test` passes.
2. Any example JSON still validates against its schema (the tests check this).
3. No real data: examples use made-up names, numbers, addresses and coordinates.

The scoring rules and their thresholds are not part of this repository and won't be added to it; see [docs/methodology.md](docs/methodology.md) for why.

By contributing you agree that your contribution is licensed under the Apache 2.0 license.
