# YTest.MTP.XUnit2

This package provides Microsoft.Testing.Platform support for xunit 2 test projects.

The general recommendation is to migrate from xunit 2 to xunit.v3 which already supports MTP. However, if you are stuck with xunit 2 (e.g, too hard to migrate to xunit.v3), and you want to migrate from VSTest to Microsoft.Testing.Platform, this package is for you.

## Supported features

- The VSTest `--filter`.
    - The VSTest `--filter` syntax is actually not supported by xunit.v3 under MTP. However, it's supported by this package for xunit 2 users. This is done to ease migration.
- Reporting test results (passed, failed, skipped).
- Discovering tests, along with traits.
- `TestMethodIdentifierProperty` is supported, but not for parameterized tests yet (known limitation).
- `xunit.runner.json` is supported.
- Reporting TRX files via Microsoft.Testing.Extensions.TrxReport (using `--report-trx`) is supported.

## Known limitations

- testconfig.json isn't supported. It can be supported in the future similar to https://github.com/xunit/xunit/commit/4c1c66f09e19299b3496fe962a2cb005ba57bc9d.
- RunSettings isn't supported. The XML-based configuration of VSTest (RunSettings) is not supported.
    - Limited support could be added based on https://github.com/xunit/visualstudio.xunit/blob/d693866207d8c1b3269d1b7f4f62211b82ba7835/src/xunit.runner.visualstudio/Utility/RunSettings.cs.
- Source information is currently missing.
- Attachments (both test-level and session-level) are not supported.
- `TestMethodIdentifierProperty` is missing the parameter types for parameterized tests.
- MTP's `--treenode-filter` is not yet supported.
- MTP's `--maximum-failed-tests` is not yet supported.
