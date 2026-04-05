# Changelog

## [2.0.0](https://github.com/mgpreston/AAPakFile/compare/v1.0.0...v2.0.0) (2026-04-05)


### ⚠ BREAKING CHANGES

* FileTableHelper.LoadRecordsAsync(Stream, ...) no longer accepts a fileTableStreamBufferSize parameter. Callers that passed this argument explicitly must remove it; callers relying on the default value are unaffected by the parameter removal itself.

### Features

* make buffer sizes and pipe block size configurable ([8c1e801](https://github.com/mgpreston/AAPakFile/commit/8c1e80175cd8b7dab5a824b6e3910e0e99f00116))


### Bug Fixes

* remove unused fileTableStreamBufferSize from LoadRecordsAsync(Stream) overload ([057ae9a](https://github.com/mgpreston/AAPakFile/commit/057ae9acdd2f9bb498fe9d7d31dc1d3a2649961a))

## 1.0.0 (2026-04-04)


### Features

* initial implementation ([0bb26d4](https://github.com/mgpreston/AAPakFile/commit/0bb26d4f12a033f20dee3afb5d8cc27405944f3d))
