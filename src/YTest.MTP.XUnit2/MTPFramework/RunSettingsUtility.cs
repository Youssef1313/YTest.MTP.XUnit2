using System;
using System.Collections;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace YTest.MTP.XUnit2;

// Copy from https://github.com/xunit/xunit/blob/4ade48a7e65aa916a20b11d38da0ec127454bf80/src/xunit.v3.runner.common/Utility/RunSettingsUtility.cs

internal static class RunSettingsUtility
{
    private static bool? s_collectSourceInformation;

    public static bool CollectSourceInformation
    {
        get
        {
            if (!s_collectSourceInformation.HasValue)
            {
                try
                {
                    var runSettings = Environment.GetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS");
                    if (runSettings is not null)
                    {
                        var doc = XDocument.Parse(runSettings);
                        if (doc.Root?.XPathEvaluate("/RunSettings/RunConfiguration/CollectSourceInformation") is IEnumerable enumerable)
                            if (enumerable.OfType<XElement>().FirstOrDefault() is XElement element)
                                s_collectSourceInformation = bool.Parse(element.Value);
                    }
                }
                catch { }

                s_collectSourceInformation ??= false;
            }

            return s_collectSourceInformation.Value;
        }
    }
}
