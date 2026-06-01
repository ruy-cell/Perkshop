# Contributing

## Local development

1. Clone the repository.
2. Ensure V Rising/BepInEx/interop dependencies are available in the expected local paths used by `PerkShop.csproj`.
3. Build with Visual Studio or:

```bash
dotnet build PerkShop.csproj -c Release
```

## Pull requests

- Keep command names explicit and documented.
- Avoid changing existing config keys unless a migration path is included.
- Test on a dedicated server before submitting gameplay-affecting changes.

## Coding notes

- Player commands should stay minimal and readable in V Rising chat.
- Admin commands should distinguish purchased-style grants from admin-only grants.
- Keep Bloodcraft compatibility safeguards for stat carrier usage.
