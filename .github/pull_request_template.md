## What does this change?

<!-- A short description, and the issue it closes if any: "Closes #12". -->

## How was it tested?

<!-- Unit tests, live webhook test, real server (CS2 / CounterStrikeSharp versions)... -->

## Checklist

- [ ] `dotnet build -c Release` has 0 warnings and `dotnet test -c Release` passes
- [ ] New logic lives in `Cs2DiscordRelay.Core` and is covered by tests
- [ ] Player-controlled text is escaped, and nothing logs the webhook URL
- [ ] README / CHANGELOG updated if the behavior or the configuration changes
