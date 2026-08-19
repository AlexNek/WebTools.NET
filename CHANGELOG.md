# Changelog

All notable changes to this project will be documented in this file. Date format: YYYY-MM-DD

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `BrowserAgent` — stateful browser agent that lets an LLM autonomously navigate, interact with, and extract information from web pages across multiple turns. Supports Navigate, Click, Fill, FillForm (compound multi-field), Select, Submit, ScrollDown/Up, WaitFor, Back, and Snapshot actions.
- `PageSnapshot` model returned after every action with URL, title, content (formatted via `EContentFormat`), interactive elements list, status code, error, scroll hint, and optional screenshot
- `BrowserAgentOptions` for configuring max actions, max duration, content format, screenshot opt-in, and cookie/auth persistence via Playwright storage state
- `InteractiveElement` model representing clickable/fillable page elements with 1-based ephemeral indexing
- `EBrowserActionType` enum and `BrowserAction`/`FormFieldValue` records for the action vocabulary
- New `IBrowserInteraction` methods: `GetTitleAsync`, `GoBackAsync`, `ScrollAsync`, `SelectOptionAsync`, `SubmitFormAsync`, `WaitForSelectorAsync`, `ScreenshotAsync`, `SaveStorageStateAsync`, `LoadStorageStateAsync`

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

