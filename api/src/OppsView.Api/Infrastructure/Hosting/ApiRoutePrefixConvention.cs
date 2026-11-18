using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace OppsView.Api.Infrastructure.Hosting;

/// <summary>
/// Puts every controller under one path prefix, e.g. "api": /applications
/// becomes /api/applications. Used when the API also serves the website from
/// the same address (Hosting:ApiPrefix), so API routes like /reminders can't
/// clash with the website's pages of the same name.
/// </summary>
/// <remarks>
/// An MVC "application model convention" runs once at startup and rewrites
/// the routes found on the controllers, so no controller has to change.
/// </remarks>
public sealed class ApiRoutePrefixConvention(string prefix) : IApplicationModelConvention
{
    private readonly AttributeRouteModel _prefix = new(new Microsoft.AspNetCore.Mvc.RouteAttribute(prefix.Trim('/')));

    public void Apply(ApplicationModel application)
    {
        foreach (var selector in application.Controllers.SelectMany(c => c.Selectors))
        {
            selector.AttributeRouteModel = selector.AttributeRouteModel is null
                ? _prefix
                : AttributeRouteModel.CombineAttributeRouteModel(_prefix, selector.AttributeRouteModel);
        }
    }
}
