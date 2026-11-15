namespace OppsView.Api.Domain;

/// <summary>
/// Who made a change: the user typing it in, OppsView reading it from an
/// email, or OppsView acting on a reply deadline. Shown in the UI ("from
/// email", "no reply by the deadline") so users can tell what was automatic.
/// </summary>
public enum ApplicationSource
{
    Manual,
    Email,
    /// <summary>The employer said "no reply by X means unsuccessful", and X passed.</summary>
    NoReply,
}
