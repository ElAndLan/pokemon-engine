using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation;

namespace Cgm.Creator.Tests;

public sealed class ProjectSessionTests
{
    [Fact]
    public void Open_LoadsSettingsAndEntities()
    {
        string dir = TestRepo.CopySampleToTemp("fixture-min");
        try
        {
            var session = ProjectSession.Open(dir);
            Assert.Equal("Fixture Min", session.Settings.Name);
            Assert.NotNull(session.Find<Species>(EntityId.Parse("species:leafcub")));
            Assert.False(session.IsDirty);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Put_MarksDirty_And_Save_Persists()
    {
        string dir = TestRepo.CopySampleToTemp("fixture-min");
        try
        {
            var session = ProjectSession.Open(dir);
            Species leafcub = session.Find<Species>(EntityId.Parse("species:leafcub"))!;

            session.Put(leafcub with { CatchRate = 200 });
            Assert.True(session.IsDirty);

            session.Save();
            Assert.False(session.IsDirty);

            // Reopen from disk: the change survived.
            var reopened = ProjectSession.Open(dir);
            Assert.Equal(200, reopened.Find<Species>(EntityId.Parse("species:leafcub"))!.CatchRate);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void SavedEntity_IsByteStableWithLoader()
    {
        string dir = TestRepo.CopySampleToTemp("fixture-min");
        try
        {
            // Saving an unchanged entity should match what CgmJson would write for it.
            var session = ProjectSession.Open(dir);
            Species leafcub = session.Find<Species>(EntityId.Parse("species:leafcub"))!;
            session.Put(leafcub); // no change, but marks dirty
            session.Save();

            string onDisk = File.ReadAllText(Path.Combine(dir, "data", "species", "leafcub.json"));
            Assert.Equal(CgmJson.SerializeEntity(leafcub), onDisk);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Remove_DeletesFileOnSave()
    {
        string dir = TestRepo.CopySampleToTemp("fixture-min");
        try
        {
            var session = ProjectSession.Open(dir);
            session.Remove(EntityId.Parse("item:potion"));
            session.Save();

            Assert.False(File.Exists(Path.Combine(dir, "data", "item", "potion.json")));
            Assert.False(ProjectSession.Open(dir).Contains(EntityId.Parse("item:potion")));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Add_RejectsDuplicateId()
    {
        string dir = TestRepo.CopySampleToTemp("fixture-min");
        try
        {
            var session = ProjectSession.Open(dir);
            var dup = new TypeDef { Id = EntityId.Parse("type:fire"), Name = "Dup" };
            Assert.Throws<InvalidOperationException>(() => session.Add(dup));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Snapshot_FeedsValidatorCleanForFixtureMin()
    {
        string dir = TestRepo.CopySampleToTemp("fixture-min");
        try
        {
            var session = ProjectSession.Open(dir);
            ValidationReport report = Validator.Run(session.Snapshot());
            Assert.False(report.HasErrors);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
