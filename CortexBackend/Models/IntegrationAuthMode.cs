namespace Cortex.API.Models;

public enum IntegrationAuthMode
{
    Manual = 0,
    OAuth = 1,
    AppRegistration = 2,
    ApiToken = 3,
    OAuthClientCredentials = 4,

    /// <summary>Reference catalogs only — no outbound authenticated connector.</summary>
    ReferenceMetadata = 5,
}
