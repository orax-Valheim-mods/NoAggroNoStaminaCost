// Skips the Mono JIT's access-modifier verification for this assembly (same effect as ticking
// "Allow unsafe code" in the project settings). Originates from the Veilheim mod template.

using System.Security.Permissions;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
