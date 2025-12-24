# YTest.MTP.XUnit2

This package provides Microsoft.Testing.Platform support for xunit 2 test projects.

The general recommendation is to migrate from xunit 2 to xunit.v3 which already supports MTP. However, if you are stuck with xunit 2 (e.g, too hard to migrate to xunit.v3), and you want to migrate from VSTest to Microsoft.Testing.Platform, this package is for you.

## Supported features

- The VSTest `--filter`.
    - The VSTest `--filter` syntax is actually not supported by xunit.v3 under MTP. However, it's supported by this package for xunit 2 users. This is done to ease migration.
- Reporting test results (passed, failed, skipped).
- Discovering tests, along with traits.
- `TestMethodIdentifierProperty` is supported.
- `xunit.runner.json` is supported.
- Reporting TRX files via Microsoft.Testing.Extensions.TrxReport (using `--report-trx`) is supported.
- Source information are reported, except when IDE explicitly requests to not calculate it (for perf reasons).
- MTP's `--treenode-filter` is supported.
- MTP's `--maximum-failed-tests` is supported.

## Known limitations

There are known limitations on the current support of MTP for xunit 2 which is provided by this package. If impacted by these, consider reacting with thumbs up to the issue. If you found other limitations and/or bugs, consider opening a new issue for it.

- testconfig.json isn't supported. It can be supported in the future similar to https://github.com/xunit/xunit/commit/4c1c66f09e19299b3496fe962a2cb005ba57bc9d.
    - Tracking issue: https://github.com/Youssef1313/YTest.MTP.XUnit2/issues/1
- RunSettings isn't supported. The XML-based configuration of VSTest (RunSettings) is not supported.
    - Limited support could be added based on https://github.com/xunit/visualstudio.xunit/blob/d693866207d8c1b3269d1b7f4f62211b82ba7835/src/xunit.runner.visualstudio/Utility/RunSettings.cs.
    - Tracking issue: https://github.com/Youssef1313/YTest.MTP.XUnit2/issues/2
- Attachments (both test-level and session-level) are not supported.
    - Tracking issue: https://github.com/Youssef1313/YTest.MTP.XUnit2/issues/4
- `TestMethodIdentifierProperty` for parameters that represent generic types might not work well.
    - Tracking issue: https://github.com/Youssef1313/YTest.MTP.XUnit2/issues/5

## How to use

To add Microsoft.Testing.Platform support to your xunit 2 test projects, all you need is to add a `PackageReference` to `YTest.MTP.XUnit2`, and you have MTP support!

There are additional concerns that are general for any VSTest to MTP migration.

1. If using .NET 10 SDK or later (recommended), update `global.json` to specify Microsoft.Testing.Platform as the test runner.
1. Update any CI YML files or scripts to use the right command-line options for MTP.
1. If you don't need VSTest support anymore, you can also remove the package references of `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`
1. Enjoy running xunit 2 on Microsoft.Testing.Platform!
