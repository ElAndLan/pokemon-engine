using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests.ViewModels;

public sealed class TypeChartDocumentTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");

    private static (TypeChartDocument chart, ProjectSession session, string dir) Open()
    {
        string dir = TestRepo.CopySampleToTemp("fixture-min");
        var session = ProjectSession.Open(dir);
        return (new TypeChartDocument(session), session, dir);
    }

    [Fact]
    public void Multiplier_ReflectsFixtureChart()
    {
        (TypeChartDocument chart, _, string dir) = Open();
        try
        {
            // Fixture: fire doubles grass; fire halves fire; grass halves grass.
            Assert.Equal(2, chart.Multiplier(Fire, Grass));
            Assert.Equal(0.5, chart.Multiplier(Fire, Fire));
            Assert.Equal(0.5, chart.Multiplier(Grass, Grass));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Cycle_WalksTheFullRingAndWritesSession()
    {
        (TypeChartDocument chart, ProjectSession session, string dir) = Open();
        try
        {
            // grass→grass starts at ½ in the fixture; ring is 1 → 2 → ½ → 0 → 1.
            Assert.Equal(0.5, chart.Multiplier(Grass, Grass));
            chart.Cycle(Grass, Grass); Assert.Equal(0, chart.Multiplier(Grass, Grass));
            chart.Cycle(Grass, Grass); Assert.Equal(1, chart.Multiplier(Grass, Grass));
            chart.Cycle(Grass, Grass); Assert.Equal(2, chart.Multiplier(Grass, Grass));
            chart.Cycle(Grass, Grass); Assert.Equal(0.5, chart.Multiplier(Grass, Grass));

            Assert.True(chart.IsDirty);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Cycle_MovesDefenderBetweenListsExclusively()
    {
        (TypeChartDocument chart, ProjectSession session, string dir) = Open();
        try
        {
            // grass→fire starts at ½; one cycle → 0, so fire moves half→no, present in exactly one list.
            chart.Cycle(Grass, Fire);
            TypeDef grass = session.Find<TypeDef>(Grass)!;
            Assert.Contains(Fire, grass.NoDamageTo);
            Assert.DoesNotContain(Fire, grass.DoubleDamageTo);
            Assert.DoesNotContain(Fire, grass.HalfDamageTo);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Cycle_IsUndoable()
    {
        (TypeChartDocument chart, _, string dir) = Open();
        try
        {
            double before = chart.Multiplier(Fire, Grass);
            chart.Cycle(Fire, Grass);
            chart.Undo.Undo();
            Assert.Equal(before, chart.Multiplier(Fire, Grass));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void BuildsSquareMatrix()
    {
        (TypeChartDocument chart, _, string dir) = Open();
        try
        {
            int n = chart.TypeLabels.Count;
            Assert.Equal(2, n); // fixture has fire + grass
            Assert.All(chart.Rows, r => Assert.Equal(n, r.Cells.Count));
        }
        finally { Directory.Delete(dir, true); }
    }
}
