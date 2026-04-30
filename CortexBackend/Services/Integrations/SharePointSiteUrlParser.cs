namespace Cortex.API.Services.Integrations;

/// <summary>
/// Parses SharePoint list view URLs into hostname and server-relative site path (e.g. /sites/support).
/// </summary>
public static class SharePointSiteUrlParser
{
    /// <summary>
    /// Extracts hostname and site path from a SharePoint list or library page URL.
    /// </summary>
    /// <param name="listPageUrl">Example: https://tenant.sharepoint.com/sites/support/Lists/MyList</param>
    public static bool TryParseListPageUrl(string? listPageUrl, out string hostname, out string siteRelativePath, out string? error)
    {
        hostname = string.Empty;
        siteRelativePath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(listPageUrl))
        {
            error = "ExternalUrl is required for SharePoint list sources.";
            return false;
        }

        if (!Uri.TryCreate(listPageUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "ExternalUrl must be a valid http(s) URL.";
            return false;
        }

        hostname = uri.Host;
        var path = uri.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrEmpty(path))
        {
            error = "Could not determine site path from ExternalUrl.";
            return false;
        }

        // Prefer path up to /Lists/ or /lists/
        var listsIdx = path.IndexOf("/lists/", StringComparison.OrdinalIgnoreCase);
        if (listsIdx > 0)
        {
            siteRelativePath = path[..listsIdx];
        }
        else
        {
            // Sites: .../sites/name/... or /teams/name/...
            var sitesIdx = path.IndexOf("/sites/", StringComparison.OrdinalIgnoreCase);
            var teamsIdx = path.IndexOf("/teams/", StringComparison.OrdinalIgnoreCase);
            int start;
            if (sitesIdx >= 0)
            {
                start = sitesIdx;
            }
            else if (teamsIdx >= 0)
            {
                start = teamsIdx;
            }
            else
            {
                error = "ExternalUrl must include a site path such as /sites/... or /lists/....";
                return false;
            }

            var remainder = path[start..];
            var segments = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                error = "Could not determine site path from ExternalUrl.";
                return false;
            }

            // /sites/{name} or /teams/{name}
            siteRelativePath = "/" + segments[0] + "/" + segments[1];
        }

        if (string.IsNullOrEmpty(siteRelativePath) || !siteRelativePath.StartsWith("/", StringComparison.Ordinal))
        {
            siteRelativePath = "/" + siteRelativePath.TrimStart('/');
        }

        return true;
    }
}
