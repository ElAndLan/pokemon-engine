using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests.ViewModels;

public sealed class EditorDocumentTests
{
    private static (ProjectSession session, string dir) OpenFixture()
    {
        string dir = TestRepo.CopySampleToTemp("fixture-min");
        return (ProjectSession.Open(dir), dir);
    }

    [Fact]
    public void MoveEdit_UpdatesSession_MarksDirty()
    {
        (ProjectSession session, string dir) = OpenFixture();
        try
        {
            var doc = new MoveDocument(session, session.Find<Move>(EntityId.Parse("move:ember"))!);
            Assert.False(doc.IsDirty);

            doc.Power = 60;
            Assert.Equal(60, doc.Power);
            Assert.Equal(60, session.Find<Move>(EntityId.Parse("move:ember"))!.Power);
            Assert.True(doc.IsDirty);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MoveEdit_UndoRedo_RestoresAndReapplies()
    {
        (ProjectSession session, string dir) = OpenFixture();
        try
        {
            var doc = new MoveDocument(session, session.Find<Move>(EntityId.Parse("move:ember"))!);
            int? original = doc.Power;

            doc.Power = 99;
            doc.Undo.Undo();
            Assert.Equal(original, doc.Power);
            Assert.Equal(original, session.Find<Move>(EntityId.Parse("move:ember"))!.Power);

            doc.Undo.Redo();
            Assert.Equal(99, doc.Power);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MoveEdit_SettingSameValue_DoesNotDirty()
    {
        (ProjectSession session, string dir) = OpenFixture();
        try
        {
            var doc = new MoveDocument(session, session.Find<Move>(EntityId.Parse("move:ember"))!);
            doc.Power = doc.Power; // no-op
            Assert.False(doc.IsDirty);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MoveDocument_ExposesAvailableTypesFromProject()
    {
        (ProjectSession session, string dir) = OpenFixture();
        try
        {
            var doc = new MoveDocument(session, session.Find<Move>(EntityId.Parse("move:ember"))!);
            Assert.Contains(EntityId.Parse("type:fire"), doc.AvailableTypes);
            Assert.Contains(EntityId.Parse("type:grass"), doc.AvailableTypes);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ItemEdit_UndoRedo_Works()
    {
        (ProjectSession session, string dir) = OpenFixture();
        try
        {
            var doc = new ItemDocument(session, session.Find<Item>(EntityId.Parse("item:potion"))!);
            doc.Price = 500;
            Assert.True(doc.IsDirty);
            doc.Undo.Undo();
            Assert.Equal(200, doc.Price);
            Assert.Contains("medicine", doc.AvailablePockets);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ItemEdit_BattleEffectsJson_UpdatesHeldEffects()
    {
        (ProjectSession session, string dir) = OpenFixture();
        try
        {
            var doc = new ItemDocument(session, session.Find<Item>(EntityId.Parse("item:potion"))!);

            doc.Holdable = true;
            doc.BattleEffectsJson = """[{"op":"weatherDurationExtend","params":{"turns":2}}]""";

            Item item = session.Find<Item>(EntityId.Parse("item:potion"))!;
            Assert.True(item.Holdable);
            Assert.Equal("weatherDurationExtend", item.BattleEffects.Single().Op);
            Assert.True(doc.IsDirty);
            Assert.Equal("", doc.JsonError);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void AbilityEdit_HooksJson_UpdatesSession()
    {
        (ProjectSession session, string dir) = OpenFixture();
        try
        {
            var ability = new Ability { Id = EntityId.Parse("ability:rain_call"), Name = "Rain Call" };
            session.Add(ability);
            var doc = new AbilityDocument(session, ability);

            doc.HooksJson = """[{"hook":"onSwitchIn","effects":[{"op":"weatherSummon","params":{"weather":"rain"}}]}]""";

            Ability updated = session.Find<Ability>(EntityId.Parse("ability:rain_call"))!;
            Assert.Equal(AbilityHookPoint.OnSwitchIn, updated.Hooks.Single().Hook);
            Assert.Equal("weatherSummon", updated.Hooks.Single().Effects.Single().Op);
            Assert.True(doc.IsDirty);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void SpeciesEdit_AbilitySlotsAndFormsJson_UpdateSession()
    {
        (ProjectSession session, string dir) = OpenFixture();
        try
        {
            var doc = new SpeciesDocument(session, session.Find<Species>(EntityId.Parse("species:leafcub"))!);

            doc.AbilitiesText = "ability:leaf_guard, ability:rain_call";
            doc.HiddenAbilityText = "ability:hidden_leaf";
            doc.FormsJson = """
                [{"formId":"rain","activation":"condition","typeOverrides":["type:grass"],"sprites":{"front":"sprite:rain_front","back":"sprite:rain_back","icon":"sprite:rain_icon"},"condition":{"weather":"rain"}}]
                """;

            Species species = session.Find<Species>(EntityId.Parse("species:leafcub"))!;
            Assert.Equal([EntityId.Parse("ability:leaf_guard"), EntityId.Parse("ability:rain_call")], species.Abilities);
            Assert.Equal(EntityId.Parse("ability:hidden_leaf"), species.HiddenAbility);
            Assert.Equal("rain", species.Forms.Single().FormId);
            Assert.True(doc.IsDirty);
            Assert.Equal("", doc.EditError);
        }
        finally { Directory.Delete(dir, true); }
    }
}
