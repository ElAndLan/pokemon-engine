using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public static class TrainerAi
{
    public static BattleAction ChooseAction(AiProfile profile, SmartAiContext context) => profile switch
    {
        AiProfile.Random => new UseMove(RandomAi.ChooseMove(context.EnemyParty[context.EnemyActive], context.Rng)),
        AiProfile.Smart => SmartAi.ChooseAction(context).Action,
        _ => new UseMove(BasicAi.ChooseMove(context.EnemyParty[context.EnemyActive],
            context.PlayerParty[context.PlayerActive], context.Chart, context.Rng)),
    };

    public static int ChooseMove(AiProfile profile, BattleCreature attacker, BattleCreature defender,
        TypeChart chart, IRng rng) => profile switch
    {
        AiProfile.Random => RandomAi.ChooseMove(attacker, rng),
        AiProfile.Smart => SmartAi.ChooseMove(attacker, defender, chart, rng),
        _ => BasicAi.ChooseMove(attacker, defender, chart, rng),
    };
}
