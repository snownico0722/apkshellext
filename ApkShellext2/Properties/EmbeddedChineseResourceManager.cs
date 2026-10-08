using System;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace ApkShellext2.Properties {
    /// <summary>
    /// Read simplified-Chinese strings from the main assembly rather than a
    /// separate zh-CN satellite DLL. Other cultures still use the standard
    /// .NET ResourceManager satellite-assembly lookup and fallback.
    /// </summary>
    internal sealed class EmbeddedChineseResourceManager : ResourceManager {
        private readonly ResourceManager chinese;

        internal EmbeddedChineseResourceManager(string baseName, Assembly assembly)
            : base(baseName, assembly) {
            chinese = new ResourceManager(
                "ApkShellext2.Properties.Resources_zh_CN_embedded", assembly);
        }

        private static bool IsSimplifiedChinese(CultureInfo culture) {
            string name = (culture ?? CultureInfo.CurrentUICulture).Name;
            return name.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("zh-SG", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("zh-Hans-", StringComparison.OrdinalIgnoreCase);
        }

        public override string GetString(string name, CultureInfo culture) {
            if (IsSimplifiedChinese(culture)) {
                string translated = chinese.GetString(name, CultureInfo.InvariantCulture);
                if (translated != null) return translated;
            }
            return base.GetString(name, culture);
        }

        public override object GetObject(string name, CultureInfo culture) {
            if (IsSimplifiedChinese(culture)) {
                object translated = chinese.GetObject(name, CultureInfo.InvariantCulture);
                if (translated != null) return translated;
            }
            return base.GetObject(name, culture);
        }
    }
}
