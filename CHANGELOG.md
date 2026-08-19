# Changelog

All notable changes to this project will be documented in this file. Date format: YYYY-MM-DD

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `BrowserSession` — caller-agnostic stateful browser-session capability for navigating, interacting with, and extracting information from pages across multiple turns. It receives an externally created `IBrowserSession`, never creates, disposes, or replaces it, and supports Navigate, Click, Fill, FillForm, Select, Submit, ScrollDown/Up, WaitFor, Back, and Snapshot operations with ephemeral DOM element indexes, viewport-aware scrolling, serialized operations, cached terminal snapshots, lifecycle-aware deadline recovery, coordinated persistence, configurable session limits, and explicit per-workflow composition.
- `BrowserSnapshot` model returned after startup and each operation with URL, title, formatted content, interactive elements, HTTP status/error information, scroll hint, and optional screenshot.
- `BrowserSessionOptions` for configuring maximum operations, maximum duration within supported timer bounds, content format, screenshot opt-in, Playwright storage-state persistence, and session viewport settings.
- `IBrowserSessionFactory` and `BrowserSessionFactory` for creating a fresh externally owned browser session per independent workflow.
- **Interactive extraction** — browser-session state includes visible, enabled interactive elements including fragment links, with deterministic, escaped selectors.
- `InteractiveElement` model representing visible clickable/fillable page elements with deterministic 1-based indexing and executable selectors.
- `EBrowserOperationType` enum and `BrowserOperation`/`FormFieldValue` records for the structured operation vocabulary.
- `IBrowserSession` for composite browser-session capabilities, with smaller capability interfaces for element extraction, history, forms, status, storage, screenshots, viewport, and waiting; the existing `IBrowserInteraction` contract remains focused on direct navigation, clicking, and filling.
- `WebSearchService`, `WebNavigationService`, and `GeoRegionService` naming for caller-agnostic web services; these types do not represent decision-making agents.

### Changed

- Introduced caller-agnostic browser sessions and services:
  `BrowserSession`, `IBrowserSession`, `BrowserOperation`, `BrowserSnapshot`,
  `BrowserSessionOptions`, `WebSearchService`, `WebNavigationService`, and
  `GeoRegionService`.
  Retained obsolete forwarding shims and legacy `AddBrowserServices` overloads so
  existing applications can migrate without an immediate source-breaking
  upgrade. New browser sessions use explicit external ownership.

### Deprecated

The following obsolete names are retained for migration:

| Deprecated API | Replacement |
| --- | --- |
| `BrowserAgent` | `BrowserSession` |
| `IBrowserAgentInteraction` | `IBrowserSession` |
| `IBrowserAgentSessionFactory` | `IBrowserSessionFactory` |
| `BrowserAction` | `BrowserOperation` |
| `EBrowserActionType` | `EBrowserOperationType` |
| `PageSnapshot` | `BrowserSnapshot` |
| `BrowserAgentOptions` | `BrowserSessionOptions` |
| `BrowserAgentSessionFactory` | `BrowserSessionFactory` |
| `WebSearchAgent` | `WebSearchService` |
| `WebNavigationAgent` | `WebNavigationService` |
| `GeoRegionAgent` | `GeoRegionService` |

### Fixed

- Browser-session navigation and operation handling preserve observed status and current page state across partial snapshot failures and deadline recovery, enforce the session deadline across startup, operation dispatch, and in-flight snapshot work, serialize browser operations, lifecycle reset, and disposal through a shared gate so page/context cleanup cannot race an in-flight call, preserve cancellation and consistent not-started state for canceled or failed initialization, use a shared configured viewport for scrolling, validate compound form fields before mutation, and degrade optional screenshot failures without discarding successful page state.
- Interactive-element extraction excludes hidden, disabled, readonly, and disabled-fieldset controls, preserves traversal order, produces escaped deterministic selectors, and verifies that the final selector resolves uniquely through Playwright.
- Browser content fetching shares lifecycle and response handling across Playwright and CloakBrowser, shares navigation/retry logic between reachability and content fetches, detects challenge pages independently of HTTP status, preserves cancellation until underlying browser operations settle, waits for active operations before disposal, classifies redirected error pages consistently, and cleans up partial resources.
- Web navigation validates link limits, preserves cancellation, handles malformed browser URLs safely, and resolves relative links against the final redirected page URL.

## [1.2.0] - 2026-08-18

### Added

- `FetchAsAsync` method on `IWebContentFetcher` with `EContentFormat` parameter for Markdown and Html output modes
- `EContentFormat` enum: `PlainText`, `Markdown`, `Html`
- `ESanitizeLevel` enum: `Strict`, `Minimal`, `None` — caller controls which noise tags are stripped

### Changed

- `FetchAsync` internally delegates to `FetchAsAsync` with `PlainText` format (no behavioral change)
- `HtmlUtils.Truncate` no longer appends a suffix — returns exactly `maxLen` characters of content

## [1.1.0] - 2026-08-18

### Changed

- `IWebContentFetcher.FetchAsync` now returns full content by default (no truncation); pass the new optional `maxContentLength` parameter to limit output to a specific character count

## [1.0.0] - 2026-08-13

### Added

- URL reachability checking via `WebAccessService` (plain HTTP, redirect tracking)
- Web search providers: `DuckDuckGoSearchProvider` (HTTP), `PlaywrightSearchProvider` (browser), `CloakBrowserSearchProvider` (stealth browser)
- Browser-based content fetching: `PlaywrightContentFetcher`, `CloakBrowserContentFetcher`
- Browser interaction abstraction: `PlaywrightSession`, `CloakBrowserSession`
- `WebSearchAgent` with automatic fallback query generation
- `WebNavigationAgent` for autonomous link extraction and navigation
- `GeoRegionAgent` for IP-based region detection with locale fallback
- Dependency injection extensions: `AddWebToolsCore()`, `AddBrowserServices()`
- NuGet packaging with GitVersion, SourceLink, and symbol packages
- GitHub Actions workflow for CI build, test, and NuGet publish on tag
- Demo project showing all library features
- Unit tests with xUnit, FluentAssertions, and NSubstitute
- Developer manual published as a documentation site via MkDocs Material and GitHub Pages

[Unreleased]: https://github.com/AlexNek/WebTools.NET/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/AlexNek/WebTools.NET/releases/tag/v1.2.0
[1.1.0]: https://github.com/AlexNek/WebTools.NET/releases/tag/v1.1.0
[1.0.0]: https://github.com/AlexNek/WebTools.NET/releases/tag/v1.0.0