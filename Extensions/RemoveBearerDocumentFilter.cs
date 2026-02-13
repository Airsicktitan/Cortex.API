using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Cortex.API.Extensions
{
    public class RemoveBearerDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (swaggerDoc.Components?.SecuritySchemes?.ContainsKey("Bearer") == true)
            {
                swaggerDoc.Components.SecuritySchemes.Remove("Bearer");
            }

            // Remove any top-level security requirements that reference "Bearer" (use reflection to stay compatible)
            var securityProp = swaggerDoc.GetType().GetProperty("Security");
            if (securityProp != null)
            {
                var secValue = securityProp.GetValue(swaggerDoc) as System.Collections.IEnumerable;
                if (secValue != null)
                {
                    var listType = typeof(System.Collections.Generic.List<Microsoft.OpenApi.Models.OpenApiSecurityRequirement>);
                    var newList = new System.Collections.Generic.List<Microsoft.OpenApi.Models.OpenApiSecurityRequirement>();
                    foreach (var item in secValue)
                    {
                        if (item is Microsoft.OpenApi.Models.OpenApiSecurityRequirement req)
                        {
                            if (!req.Keys.Any(k => k.Reference != null && k.Reference.Id == "Bearer"))
                            {
                                newList.Add(req);
                            }
                        }
                    }
                    securityProp.SetValue(swaggerDoc, newList);
                }
            }

            if (swaggerDoc.Paths != null)
            {
                foreach (var path in swaggerDoc.Paths.Values)
                {
                    foreach (var operation in path.Operations.Values)
                    {
                        if (operation.Security != null)
                        {
                            operation.Security = operation.Security
                                .Where(s => !s.Keys.Any(k => k.Reference != null && k.Reference.Id == "Bearer"))
                                .ToList();
                        }
                    }
                }
            }
        }
    }
}
