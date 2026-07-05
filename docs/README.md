# TK Packages — Design Docs Index

`docs/specs` and `docs/plans` are the committed, referenceable home for all package design docs going forward, superseding the old gitignored `.superpowers/` location.

- **specs** — design records / feature specs (the "what" and "why").
- **plans** — step-by-step implementation plans (the "how").

Some packages have only a plan: for those, the plan *is* the design record (a separate retroactive spec was intentionally not written).

## Per-package docs

| Package | Spec | Plan |
| --- | --- | --- |
| core / toolbar | — (plan is the design record) | [`plans/2026-07-03-tk-packages-v1.md`](plans/2026-07-03-tk-packages-v1.md) |
| iap | — (plan is the design record) | [`plans/2026-07-04-tk-iap-v1.md`](plans/2026-07-04-tk-iap-v1.md) |
| ads | [`specs/2026-07-04-tk-ads-design.md`](specs/2026-07-04-tk-ads-design.md) | [`plans/2026-07-04-tk-ads-v1.md`](plans/2026-07-04-tk-ads-v1.md) |
| remoteconfig | [`specs/2026-07-05-tk-remoteconfig-design.md`](specs/2026-07-05-tk-remoteconfig-design.md) | [`plans/2026-07-05-tk-remoteconfig-v1.md`](plans/2026-07-05-tk-remoteconfig-v1.md) |
| maintenance / refresh pass | — | [`plans/2026-07-05-tk-refresh-pass.md`](plans/2026-07-05-tk-refresh-pass.md) |

## Dependency versions

**`NEWTONSOFT_TARGET` = `3.2.2`** (`com.unity.nuget.newtonsoft-json`, wraps Json.NET 13.0.2).

Discovered 2026-07-05 from Unity's default package registry (`https://packages.unity.com/com.unity.nuget.newtonsoft-json`): `3.2.2` is both `dist-tags.latest` and the highest published stable (X.Y.Z, no preview/pre/beta) version — the only newer entries are `2.0.x-preview` builds, and the 3.x line tops out at `3.2.2`. The host `packages-lock.json` independently resolves the same `3.2.2` from that registry.

**Decision: keep `3.2.2`.** Since the newest stable equals the currently-pinned version across core + remoteconfig, no bump is needed; the Newtonsoft-alignment task (refresh-pass Task 2) is a no-op. All packages that declare or transitively rely on Newtonsoft must use exactly `3.2.2` (one version everywhere) — a version split would break resolution in a multi-TK-package project.
