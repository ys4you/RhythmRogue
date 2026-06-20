using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Which on-screen element a lesson should point at with a coach-mark. None shows the plain
    /// teaching card; the others dim the screen and spotlight that HUD element while the card is on
    /// screen, so the player is shown where to look, not just told.
    /// </summary>
    public enum OnboardingHighlight
    {
        None,
        Shield,
        Relics
    }

    /// <summary>
    /// A hand-authored, linear teaching path: the onboarding. Unlike a normal <see cref="Area"/>,
    /// which builds a branching procedural map from enemy pools, a sequence is a fixed straight
    /// line of lesson nodes. One enemy (so one song) is shared across every fight, each node has
    /// its own authored chart and teaching card, and a node can hand the player a relic when it is
    /// cleared. The last node is the finale (treated as the boss so the run completes normally).
    ///
    /// An <see cref="Area"/> points at one of these through <see cref="Area.onboarding"/>; when set,
    /// the map generator builds this scripted path instead of a procedural map, and battles run in
    /// practice mode if requested. Keeping all of it here keeps Area about normal procedural areas.
    ///
    /// SOLID: single responsibility is "describe a scripted teaching path". It holds data only; the
    /// generator turns it into a map and the battle systems read the per-node fields off the nodes.
    /// </summary>
    [CreateAssetMenu(fileName = "OnboardingSequence", menuName = "RhythmRogue/Data/Onboarding Sequence")]
    public class OnboardingSequence : ScriptableObject
    {
        [Tooltip("The enemy (and therefore the song) every lesson fight uses. Pin one whose " +
                 "bpmModifier is 1.0 so the song plays at the tempo the charts were authored at.")]
        public EnemyData enemy;

        [Tooltip("If true, lesson fights cannot be lost: each ends in a win shortly after its last " +
                 "note, and the reward screen is skipped so the only relics are the ones nodes hand " +
                 "out below. Leave on for a first-timer onboarding.")]
        public bool practiceMode = true;

        [Tooltip("The lesson nodes, played start to finish as one straight path. The last entry is " +
                 "the finale (treated as the boss so the run completes). Each node bundles its own " +
                 "chart, teaching text, and optional reward relic.")]
        public LessonNode[] nodes;

        /// <summary>
        /// One stop on the path: a fight with its chart, its teaching card, and an optional relic
        /// reward for clearing it. Bundling the three together keeps them aligned (no parallel
        /// arrays to keep in sync) and makes a node readable at a glance in the Inspector.
        /// </summary>
        [System.Serializable]
        public class LessonNode
        {
            [Tooltip("Hand-authored chart (a JSON TextAsset) for this fight. Its bpm must match the " +
                     "sequence enemy's song. May include an enemyNotes array (e.g. the shield lesson).")]
            public TextAsset chart;

            [Tooltip("Teaching text shown before this fight. The fight waits on a keypress, then the " +
                     "song starts. Leave empty for no card on this node.")]
            [TextArea(2, 5)] public string lesson;

            [Tooltip("Optional relic granted when this node is cleared. Most nodes leave this empty; " +
                     "the onboarding hands one out after the shield lesson so the next node explains it.")]
            public RelicData rewardRelic;

            [Tooltip("Point this lesson at an on-screen element with a coach-mark (dim + spotlight + " +
                     "ring) while the card is up. Set the shield lesson to Shield and the relic lesson " +
                     "to Relics; leave the rest None.")]
            public OnboardingHighlight highlight = OnboardingHighlight.None;
        }
    }
}
