namespace Ogooreck.API;

internal enum RecordingRole
{
    Published,
    Appended
}

internal sealed record MessageWording(RecordingRole Role, string Check, string CheckNothing, string Item, string Verb, string Title);
