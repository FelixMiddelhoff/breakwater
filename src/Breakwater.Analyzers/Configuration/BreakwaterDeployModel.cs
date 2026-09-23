namespace Breakwater.Analyzers.Configuration;

/// <summary>
/// <c>breakwater_deploy_model</c> from <c>.editorconfig</c>. Default <c>Rolling</c> (old and new
/// app versions run side by side while the migration executes, so a dropped column/table/rename
/// can break the still-running old version). <c>DowntimeOk</c> means the old app version is not
/// running while the migration executes, so those same operations are downgraded to Info.
/// </summary>
internal enum BreakwaterDeployModel
{
    Rolling,
    DowntimeOk,
}
