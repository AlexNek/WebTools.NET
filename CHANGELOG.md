# Changelog

All notable changes to this project will be documented in this file. Date format: YYYY-MM-DD

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

