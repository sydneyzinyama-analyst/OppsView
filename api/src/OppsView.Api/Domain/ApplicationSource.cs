namespace OppsView.Api.Domain;

/// <summary>
/// Who made a change: the user typing it in, or OppsView reading it from an
/// email. Shown in the UI ("from email") so users can tell what was automatic.
/// </summary>
public enum ApplicationSource
{
    Manual,
    Email,
}
