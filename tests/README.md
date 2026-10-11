# Cooked-item regression checks

Run `dotnet run --project tests/RegressionTests.csproj` with the .NET 10 SDK.

The harness compiles the production modifier and description formatters against a minimal
managed game/Unity model. It checks raw-to-cooked-to-burnt changes on the same item, disabled
effect components, inactive child branches, inactive inventory roots, live poison duration,
petrify effects, and English/Chinese descriptions. It requires no proprietary game files.

These checks do not execute Unity lifecycle callbacks or the game's cooking implementation.
For in-game verification, compare a raw Green Crispberry with the same item cooked once:
the cooked tooltip should retain its hunger/stamina modifiers and omit disabled poison and
the raw poison description. Repeat while held and after dropping it, and check a pre-cooked
food and an overcooked food (which can gain poison again). The existing 0.25-second content
refresh should update the same item without needing to switch targets.
