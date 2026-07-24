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
| analytics | [`specs/2026-07-05-tk-analytics-design.md`](specs/2026-07-05-tk-analytics-design.md) | [`plans/2026-07-05-tk-analytics-v1.md`](plans/2026-07-05-tk-analytics-v1.md) |
| notification | [`specs/2026-07-05-tk-notification-design.md`](specs/2026-07-05-tk-notification-design.md) | [`plans/2026-07-05-tk-notification-v1.md`](plans/2026-07-05-tk-notification-v1.md) |
| localization | [`specs/2026-07-05-tk-localization-design.md`](specs/2026-07-05-tk-localization-design.md) | [`plans/2026-07-05-tk-localization-v1.md`](plans/2026-07-05-tk-localization-v1.md) |
| audio | [`specs/2026-07-07-tk-audio-design.md`](specs/2026-07-07-tk-audio-design.md) | [`plans/2026-07-07-tk-audio-v1.md`](plans/2026-07-07-tk-audio-v1.md) |
| core 0.3.0 (tab bar) | — (plan is the design record) | [`plans/2026-07-07-tk-core-tabbar-v0.3.0.md`](plans/2026-07-07-tk-core-tabbar-v0.3.0.md) |
| core 0.4.0 (animated tab presenter) | [`specs/2026-07-07-tk-core-animated-tab-presenter-design.md`](specs/2026-07-07-tk-core-animated-tab-presenter-design.md) | [`plans/2026-07-07-tk-core-v0.4.0-animated-presenter.md`](plans/2026-07-07-tk-core-v0.4.0-animated-presenter.md) |
| audio 0.2.0 (SFX control + editor authoring) | [`specs/2026-07-08-tk-audio-v0.2-design.md`](specs/2026-07-08-tk-audio-v0.2-design.md) | [`plans/2026-07-08-tk-audio-v0.2.md`](plans/2026-07-08-tk-audio-v0.2.md) |
| audio 0.3.0 (music/settings polish) | [`specs/2026-07-08-tk-audio-v0.3-design.md`](specs/2026-07-08-tk-audio-v0.3-design.md) | [`plans/2026-07-08-tk-audio-v0.3.md`](plans/2026-07-08-tk-audio-v0.3.md) |
| haptics | [`specs/2026-07-08-tk-haptics-design.md`](specs/2026-07-08-tk-haptics-design.md) | [`plans/2026-07-08-tk-haptics-v1.md`](plans/2026-07-08-tk-haptics-v1.md) |
| core 0.5.0 (startup settings) | [`specs/2026-07-22-tk-core-v0.5-startup-settings-design.md`](specs/2026-07-22-tk-core-v0.5-startup-settings-design.md) | [`plans/2026-07-22-tk-core-v0.5.md`](plans/2026-07-22-tk-core-v0.5.md) |
| haptics 0.1.1 (.androidlib / VIBRATE permission) | — (plan is the design record) | [`plans/2026-07-22-tk-haptics-v0.1.1.md`](plans/2026-07-22-tk-haptics-v0.1.1.md) |
| core 0.6.0 (transition curtain) | [`specs/2026-07-23-tk-core-v0.6-transition-curtain-design.md`](specs/2026-07-23-tk-core-v0.6-transition-curtain-design.md) | [`plans/2026-07-23-tk-core-v0.6.md`](plans/2026-07-23-tk-core-v0.6.md) |
| haptics 0.2.0 (usage classification) | [`specs/2026-07-23-tk-haptics-v0.2-design.md`](specs/2026-07-23-tk-haptics-v0.2-design.md) | [`plans/2026-07-23-tk-haptics-v0.2.md`](plans/2026-07-23-tk-haptics-v0.2.md) |
| core 0.7.0 (pre-covered boot) | [`specs/2026-07-24-tk-core-v0.7-pre-covered-boot-design.md`](specs/2026-07-24-tk-core-v0.7-pre-covered-boot-design.md) | [`plans/2026-07-24-tk-core-v0.7.md`](plans/2026-07-24-tk-core-v0.7.md) |

## Dependency versions

**`NEWTONSOFT_TARGET` = `3.2.2`** (`com.unity.nuget.newtonsoft-json`, wraps Json.NET 13.0.2).

Discovered 2026-07-05 from Unity's default package registry (`https://packages.unity.com/com.unity.nuget.newtonsoft-json`): `3.2.2` is both `dist-tags.latest` and the highest published stable (X.Y.Z, no preview/pre/beta) version — the only newer entries are `2.0.x-preview` builds, and the 3.x line tops out at `3.2.2`. The host `packages-lock.json` independently resolves the same `3.2.2` from that registry.

**Decision: keep `3.2.2`.** Since the newest stable equals the currently-pinned version across core + remoteconfig, no bump is needed; the Newtonsoft-alignment task (refresh-pass Task 2) is a no-op. All packages that declare or transitively rely on Newtonsoft must use exactly `3.2.2` (one version everywhere) — a version split would break resolution in a multi-TK-package project.
