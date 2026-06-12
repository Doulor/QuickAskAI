using Windows.ApplicationModel.Resources;

namespace QuickAskAI;

internal static class ResourceHelper
{
    private static readonly ResourceLoader DefaultLoader = ResourceLoader.GetForViewIndependentUse("Resources");
    private static ResourceLoader? _overrideLoader;
    private static string? _cachedLanguage;

    internal static void ApplyLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language) || language == "auto")
        {
            Windows.ApplicationModel.Resources.Core.ResourceContext.SetGlobalQualifierValue("Language", string.Empty);
            _overrideLoader = null;
            _cachedLanguage = null;
            return;
        }

        if (language == _cachedLanguage)
        {
            return;
        }

        Windows.ApplicationModel.Resources.Core.ResourceContext.SetGlobalQualifierValue("Language", language);
        _overrideLoader = ResourceLoader.GetForViewIndependentUse("Resources");
        _cachedLanguage = language;
    }

    internal static string GetString(string key)
    {
        return _overrideLoader?.GetString(key)
            ?? DefaultLoader.GetString(key)
            ?? key;
    }
}
