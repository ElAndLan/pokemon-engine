using Cgm.Core.Model;

namespace Cgm.Core.Serialization;

/// <summary>
/// Reads/writes the <c>project.cgmproj</c> root file for a project folder. Serialization itself
/// (<see cref="CgmJson"/>) is pure and disk-free; these wrappers just do the file IO.
/// </summary>
public static class ProjectFile
{
    public const string FileName = "project.cgmproj";

    public static ProjectSettings Load(string projectFolder) =>
        CgmJson.DeserializeVersioned<ProjectSettings>(File.ReadAllText(Path.Combine(projectFolder, FileName)));

    public static void Save(string projectFolder, ProjectSettings project)
    {
        Directory.CreateDirectory(projectFolder);
        File.WriteAllText(Path.Combine(projectFolder, FileName), CgmJson.Serialize(project));
    }
}
